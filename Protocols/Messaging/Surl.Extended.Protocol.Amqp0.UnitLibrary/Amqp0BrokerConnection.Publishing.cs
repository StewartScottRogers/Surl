using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// <c>basic.publish</c>, its content frames, routing, and <c>basic.return</c> (ADR-0112 decision 6).
/// </summary>
internal sealed partial class Amqp0BrokerConnection
{
    private const byte PersistentDeliveryMode = 2;

    // The body bytes held on all channels: announced by content headers whose bodies are still
    // arriving, and published on transacted channels but not yet committed.
    private long heldContentBytes;

    private void BeginPublish(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        arguments.ReadShort();
        var exchangeName = arguments.ReadShortString();
        var routingKey = arguments.ReadShortString();
        var bits = arguments.ReadBits(2);
        var exchange = exchangeName.Length == 0 ? null : exchanges.Find(exchangeName) ?? throw NoExchange();
        channel.Publish = new(exchange, routingKey, mandatory: bits[0], immediate: bits[1]);
    }

    // A content frame: its faults name basic.publish, the method whose content it is.
    private void AnswerContentFrame(Amqp0Channel channel, Amqp0Frame frame)
    {
        try
        {
            TakeContent(channel, frame);
        }
        catch (Amqp0SyntaxException)
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.SyntaxError, "SYNTAX_ERROR - malformed content header", Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Publish);
        }
        catch (Amqp0ConnectionException exception)
        {
            throw exception.Naming(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Publish);
        }
        catch (Amqp0ChannelException exception)
        {
            CloseChannel(channel, exception.ReplyCode, exception.Message, Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Publish);
        }
    }

    private void TakeContent(Amqp0Channel channel, Amqp0Frame frame)
    {
        var publish = channel.Publish
            ?? throw new Amqp0ConnectionException(Amqp0ReplyCode.UnexpectedFrame, "UNEXPECTED_FRAME - content without basic.publish");
        if (frame.Type == Amqp0FrameType.ContentHeader)
        {
            TakeContentHeader(publish, frame);
        }
        else if (publish.Header is null || !publish.TakeBody(frame.Payload.Span))
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.UnexpectedFrame, "UNEXPECTED_FRAME - body frame out of sequence");
        }

        if (publish.IsComplete)
        {
            channel.Publish = null;
            TakeWholePublish(channel, publish);
        }
    }

    // A transacted channel buffers the publish, its bytes still held; otherwise it is routed now.
    private void TakeWholePublish(Amqp0Channel channel, Amqp0PendingPublish publish)
    {
        if (channel.Transaction is { } transaction)
        {
            transaction.AddPublish(publish);
            return;
        }

        heldContentBytes -= publish.Body.Length;
        if (channel.Confirming)
        {
            RouteConfirmed(channel, publish);
        }
        else
        {
            Route(channel, publish);
        }
    }

    private void TakeContentHeader(Amqp0PendingPublish publish, Amqp0Frame frame)
    {
        if (publish.Header is not null)
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.UnexpectedFrame, "UNEXPECTED_FRAME - second content header");
        }

        var header = Amqp0ContentHeader.Read(frame.Payload);
        if (header.ClassId != Amqp0BasicMethodId.ClassId)
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.UnexpectedFrame, "UNEXPECTED_FRAME - content header of another class");
        }

        if (header.BodySize > (ulong)bus.MaxMessageBytes)
        {
            throw new Amqp0ChannelException(Amqp0ReplyCode.ContentTooLarge, "CONTENT_TOO_LARGE - body above the limit");
        }

        // ADR-0112 decision 8: the content partly read, and the content transactions buffer, on all
        // channels together stays within --max-filesize.
        if (heldContentBytes + (long)header.BodySize > bus.MaxMessageBytes)
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.ResourceError, "RESOURCE_ERROR - content in progress above the limit");
        }

        heldContentBytes += (long)header.BodySize;
        publish.TakeHeader(header);
    }

    private void Route(Amqp0Channel channel, Amqp0PendingPublish publish)
    {
        var header = publish.Header!;
        var properties = Amqp0BasicProperties.Read(header.PropertyFlags, header.PropertyList);
        var topic = AmqpTopicExchangeNames.ToCanonicalTopic(publish.RoutingKey);
        var message = Amqp0BusMessages.ToBusMessage(
            topic.Name ?? publish.RoutingKey, ExchangeNameOf(publish), publish.RoutingKey, header, properties, publish.Body, context.TimeProvider.GetUtcNow());
        var persistent = properties.DeliveryMode == PersistentDeliveryMode;
        var (routed, taken) = publish.Exchange?.Name == Amqp0ExchangeTable.TopicSpace
            ? PublishOnTopicSpace(topic, message, publish.Immediate, persistent)
            : PublishToQueues(QueuesFor(publish), message, publish.Immediate, persistent);
        if (!routed)
        {
            ReturnOrDrop(channel, publish, publish.Mandatory || publish.Immediate);
        }
        else if (publish.Immediate && !taken)
        {
            Return(channel, publish, Amqp0ReplyCode.NoConsumers, "NO_CONSUMERS");
        }
    }

    private static string ExchangeNameOf(Amqp0PendingPublish publish) => publish.Exchange?.Name ?? string.Empty;

    private List<string> QueuesFor(Amqp0PendingPublish publish) =>
        publish.Exchange is { } exchange ? exchanges.Route(exchange, publish.RoutingKey) : [publish.RoutingKey];

    // amq.topic is the bus's topic space: routed when a queue was chosen, room or not.
    private (bool Routed, bool Taken) PublishOnTopicSpace(TopicNameMapping topic, BusMessage message, bool immediate, bool persistent)
    {
        if (!topic.IsMapped)
        {
            return (false, false);
        }

        var published = immediate ? Bus.PublishToConsumedQueues(message) : Bus.Publish(message);
        Refuse(published.Outcome);
        if (published.FullQueueCount > 0)
        {
            RefuseFullQueue(persistent);
        }

        return (published.QueuedCount + published.FullQueueCount + published.UnconsumedQueueCount > 0, published.QueuedCount > 0);
    }

    private (bool Routed, bool Taken) PublishToQueues(List<string> queues, BusMessage message, bool immediate, bool persistent)
    {
        var (routed, taken) = (false, false);
        foreach (var queue in queues)
        {
            var (queueRouted, queueTaken) = QueueOutcome(bus.PublishToQueue(queue, message, AccountName, immediate), persistent);
            routed |= queueRouted;
            taken |= queueTaken;
        }

        return (routed, taken);
    }

    private (bool Routed, bool Taken) QueueOutcome(MessageBusOutcome outcome, bool persistent)
    {
        switch (outcome)
        {
            case MessageBusOutcome.Succeeded:
                return (true, true);
            case MessageBusOutcome.UnknownQueue:
                return (false, false);
            case MessageBusOutcome.NoReadyConsumer:
                return (true, false);
            case MessageBusOutcome.QueueFull:
                RefuseFullQueue(persistent);
                return (true, false);
            default:
                throw Refusal(outcome);
        }
    }

    // A full queue does not take a transient message; a persistent one may not be discarded (basic rule 02).
    private void RefuseFullQueue(bool persistent)
    {
        if (persistent)
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.ResourceError, "RESOURCE_ERROR - queue full");
        }

        context.Log.Note("A full queue did not take a transient message.");
    }

    private void ReturnOrDrop(Amqp0Channel channel, Amqp0PendingPublish publish, bool returned)
    {
        if (returned)
        {
            Return(channel, publish, Amqp0ReplyCode.NoRoute, "NO_ROUTE");
        }
        else
        {
            context.Log.Note("An unroutable message was dropped.");
        }
    }

    private void Return(Amqp0Channel channel, Amqp0PendingPublish publish, Amqp0ReplyCode replyCode, string replyText)
    {
        var header = publish.Header!;
        var exchangeName = ExchangeNameOf(publish);
        var method = MethodOf(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Return, writer =>
        {
            writer.WriteShort((ushort)replyCode);
            writer.WriteShortString(replyText);
            writer.WriteShortString(exchangeName);
            writer.WriteShortString(publish.RoutingKey);
        });
        Send(EncodeContent(channel.Number, method, new(exchangeName, publish.RoutingKey, header.PropertyFlags, header.PropertyList, publish.Body)));
    }
}
