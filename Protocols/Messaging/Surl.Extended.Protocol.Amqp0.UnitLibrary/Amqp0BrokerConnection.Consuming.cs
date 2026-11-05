using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The basic class's delivering side: <c>qos</c>, <c>consume</c>, <c>cancel</c>, <c>get</c>,
/// <c>ack</c>, <c>reject</c>, <c>nack</c> and <c>recover</c>, and the deliveries themselves
/// (ADR-0112 decision 6).
/// </summary>
internal sealed partial class Amqp0BrokerConnection
{
    /// <summary>The most deliveries a consumer holds unsettled when <c>prefetch-count</c> is 0 or larger: 1000 (decision 8).</summary>
    internal const int ConsumerWindow = 1000;

    private static readonly Amqp0MethodPayload QosOk = new(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.QosOk, ReadOnlyMemory<byte>.Empty);
    private static readonly Amqp0MethodPayload RecoverOk = new(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.RecoverOk, ReadOnlyMemory<byte>.Empty);
    private static readonly Amqp0MethodPayload GetEmpty = new(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.GetEmpty, new byte[1]);

    // prefetch-count bounds each consumer the channel makes afterwards; prefetch-size and global are read.
    private void AnswerQos(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        arguments.ReadLong();
        channel.PrefetchCount = arguments.ReadShort();
        arguments.ReadBits(1);
        SendMethod(channel.Number, QosOk);
    }

    private void Consume(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        arguments.ReadShort();
        var queue = ResolveQueue(channel, arguments.ReadShortString());
        var tag = arguments.ReadShortString();
        var bits = arguments.ReadBits(4);
        arguments.ReadFieldTable();
        tag = tag.Length == 0 ? GenerateName("amq.ctag-") : tag;
        if (channel.Consumers.ContainsKey(tag))
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.NotAllowed, "NOT_ALLOWED - consumer tag in use");
        }

        var consumer = new Amqp0Consumer(tag, noAck: bits[1]);
        var window = consumer.NoAck ? 0 : WindowOf(channel);
        var consumed = Bus.Consume(queue, delivery => Deliver(channel, consumer, delivery), window, exclusive: bits[2]);
        Refuse(consumed.Outcome);
        consumer.Bus = consumed.Consumer!;
        channel.Consumers.Add(tag, consumer);
        if (!bits[3])
        {
            SendMethod(channel.Number, TaggedMethod(Amqp0BasicMethodId.ConsumeOk, tag));
        }

        consumer.Bus.Start();
    }

    private static int WindowOf(Amqp0Channel channel) =>
        channel.PrefetchCount is 0 or > ConsumerWindow ? ConsumerWindow : channel.PrefetchCount;

    private static Amqp0MethodPayload TaggedMethod(ushort methodId, string tag) =>
        MethodOf(Amqp0BasicMethodId.ClassId, methodId, writer => writer.WriteShortString(tag));

    // The consumer's deliveries end; those outstanding stay so until settled or the channel closes.
    private void CancelConsumer(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        var tag = arguments.ReadShortString();
        var noWait = arguments.ReadBits(1)[0];
        if (channel.Consumers.Remove(tag, out var consumer))
        {
            consumer.Bus.Stop();
            channel.Stopped.Add(consumer.Bus);
        }

        if (!noWait)
        {
            SendMethod(channel.Number, TaggedMethod(Amqp0BasicMethodId.CancelOk, tag));
        }
    }

    // Called on the bus's thread: never blocks, only queues the delivery for writing.
    private void Deliver(Amqp0Channel channel, Amqp0Consumer consumer, BusQueueDelivery delivery)
    {
        var content = Amqp0BusMessages.ToOutgoingContent(delivery.Message, delivery.QueueName, context.TimeProvider.GetUtcNow());
        lock (channel.Gate)
        {
            var tag = NextDeliveryTag(channel, consumer.NoAck ? null : new(consumer.Bus, delivery.QueueName, delivery.DeliveryTag));
            var method = MethodOf(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Deliver, writer =>
            {
                writer.WriteShortString(consumer.Tag);
                writer.WriteLongLong(tag);
                writer.WriteBits([delivery.Redelivered]);
                writer.WriteShortString(content.Exchange);
                writer.WriteShortString(content.RoutingKey);
            });
            var bytes = EncodeContent(channel.Number, method, content);
            if (channel.FlowActive)
            {
                Send(bytes);
            }
            else
            {
                channel.Held.Add(bytes);
            }
        }

        if (consumer.NoAck)
        {
            consumer.Bus.Acknowledge(delivery.DeliveryTag);
        }
    }

    // Delivery tags run from 1 per channel, consumer deliveries and get-ok together. Call under the gate.
    private static ulong NextDeliveryTag(Amqp0Channel channel, Amqp0OutstandingDelivery? outstanding)
    {
        var tag = ++channel.LastDeliveryTag;
        if (outstanding is not null)
        {
            channel.Outstanding.Add(tag, outstanding);
        }

        return tag;
    }

    private void Get(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        arguments.ReadShort();
        var queue = ResolveQueue(channel, arguments.ReadShortString());
        var noAck = arguments.ReadBits(1)[0];
        var got = Bus.GetFromQueue(queue);
        Refuse(got.Outcome);
        if (got.Delivery is not { } delivery)
        {
            SendMethod(channel.Number, GetEmpty);
            return;
        }

        var content = Amqp0BusMessages.ToOutgoingContent(delivery.Message, queue, context.TimeProvider.GetUtcNow());
        lock (channel.Gate)
        {
            var tag = NextDeliveryTag(channel, noAck ? null : new(null, queue, delivery.DeliveryTag));
            var method = MethodOf(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.GetOk, writer =>
            {
                writer.WriteLongLong(tag);
                writer.WriteBits([delivery.Redelivered]);
                writer.WriteShortString(content.Exchange);
                writer.WriteShortString(content.RoutingKey);
                writer.WriteLong((uint)got.ReadyMessageCount);
            });
            Send(EncodeContent(channel.Number, method, content));
        }

        if (noAck)
        {
            Bus.AcknowledgeGet(queue, delivery.DeliveryTag);
        }
    }

    private void Acknowledge(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        var tag = arguments.ReadLongLong();
        var multiple = arguments.ReadBits(1)[0];
        Settle(TakeOutstandingOrRefuse(channel, tag, multiple), acknowledge: true, requeue: false);
    }

    private void Reject(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        var tag = arguments.ReadLongLong();
        var requeue = arguments.ReadBits(1)[0];
        Settle(TakeOutstandingOrRefuse(channel, tag, multiple: false), acknowledge: false, requeue);
    }

    private void Nack(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        var tag = arguments.ReadLongLong();
        var bits = arguments.ReadBits(2);
        Settle(TakeOutstandingOrRefuse(channel, tag, multiple: bits[0]), acknowledge: false, requeue: bits[1]);
    }

    // Every outstanding delivery of the channel goes back to its queue, marked redelivered.
    private void Recover(Amqp0Channel channel, Amqp0WireReader arguments, bool answer)
    {
        arguments.ReadBits(1);
        Settle(TakeOutstanding(channel, 0, multiple: true), acknowledge: false, requeue: true);
        if (answer)
        {
            SendMethod(channel.Number, RecoverOk);
        }
    }

    // Rule ack/exists: a tag not outstanding on this channel is 406; tag 0 with multiple is every one.
    private static List<Amqp0OutstandingDelivery> TakeOutstandingOrRefuse(Amqp0Channel channel, ulong tag, bool multiple)
    {
        lock (channel.Gate)
        {
            if (!(multiple && tag == 0) && !channel.Outstanding.ContainsKey(tag))
            {
                throw new Amqp0ChannelException(Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - unknown delivery tag");
            }

            return TakeOutstanding(channel, tag, multiple);
        }
    }

    // Removes the deliveries up to tag with multiple (all of them for tag 0), else the one tag.
    private static List<Amqp0OutstandingDelivery> TakeOutstanding(Amqp0Channel channel, ulong tag, bool multiple)
    {
        lock (channel.Gate)
        {
            var tags = multiple
                ? channel.Outstanding.Keys.Where(outstanding => tag == 0 || outstanding <= tag).ToList()
                : [tag];
            var taken = tags.Select(outstanding => channel.Outstanding[outstanding]).ToList();
            tags.ForEach(outstanding => channel.Outstanding.Remove(outstanding));
            return taken;
        }
    }

    // Settled outside the channel's gate: the bus may deliver again at once, on this thread.
    private void Settle(List<Amqp0OutstandingDelivery> deliveries, bool acknowledge, bool requeue)
    {
        foreach (var delivery in deliveries)
        {
            _ = (delivery.Consumer, acknowledge) switch
            {
                (null, true) => Bus.AcknowledgeGet(delivery.QueueName, delivery.BusDeliveryTag),
                (null, false) => Bus.RejectGet(delivery.QueueName, delivery.BusDeliveryTag, requeue),
                (_, true) => delivery.Consumer!.Acknowledge(delivery.BusDeliveryTag),
                _ => delivery.Consumer!.Reject(delivery.BusDeliveryTag, requeue),
            };
        }
    }

    // The method frame, the content header and the body in frames of at most frame-max less 8 bytes, as one write.
    private byte[] EncodeContent(ushort channel, Amqp0MethodPayload method, Amqp0OutgoingContent content)
    {
        var frames = new List<byte[]>
        {
            Amqp0Frame.ForMethod(channel, method).Encode(),
            Amqp0Frame.ForContentHeader(channel, new(Amqp0BasicMethodId.ClassId, (ulong)content.Body.Length, content.PropertyFlags, content.PropertyList)).Encode(),
        };
        var largestBody = frameMax - Amqp0Frame.OverheadBytes;
        for (var offset = 0; offset < content.Body.Length; offset += largestBody)
        {
            var body = content.Body.Slice(offset, Math.Min(largestBody, content.Body.Length - offset));
            frames.Add(new Amqp0Frame(Amqp0FrameType.ContentBody, channel, body).Encode());
        }

        return [.. frames.SelectMany(frame => frame)];
    }
}
