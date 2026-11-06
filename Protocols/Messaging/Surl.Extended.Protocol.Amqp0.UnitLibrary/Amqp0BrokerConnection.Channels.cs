namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// Channels: <c>channel.open</c>, <c>flow</c> and <c>close</c>, the dispatch of every method on an
/// open channel, and channel and connection exceptions (ADR-0112 decisions 6 and 7).
/// </summary>
internal sealed partial class Amqp0BrokerConnection
{
    private delegate void MethodAnswer(Amqp0BrokerConnection connection, Amqp0Channel channel, Amqp0WireReader arguments);

    // The methods only a server sends, and the replies to methods the broker never sends: 503.
    private static readonly HashSet<(ushort ClassId, ushort MethodId)> ClientMayNotSend =
    [
        (Amqp0ChannelMethodId.ClassId, Amqp0ChannelMethodId.OpenOk),
        (Amqp0ChannelMethodId.ClassId, Amqp0ChannelMethodId.FlowOk),
        (Amqp0ChannelMethodId.ClassId, Amqp0ChannelMethodId.CloseOk),
        (Amqp0ExchangeMethodId.ClassId, Amqp0ExchangeMethodId.DeclareOk),
        (Amqp0ExchangeMethodId.ClassId, Amqp0ExchangeMethodId.DeleteOk),
        (Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.DeclareOk),
        (Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.BindOk),
        (Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.PurgeOk),
        (Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.DeleteOk),
        (Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.UnbindOk),
        (Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.QosOk),
        (Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.ConsumeOk),
        (Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.CancelOk),
        (Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Return),
        (Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Deliver),
        (Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.GetOk),
        (Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.GetEmpty),
        (Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.RecoverOk),
    ];

    private static readonly Dictionary<(ushort ClassId, ushort MethodId), MethodAnswer> MethodAnswers = new()
    {
        [(Amqp0ChannelMethodId.ClassId, Amqp0ChannelMethodId.Open)] = (_, _, _) =>
            throw new Amqp0ConnectionException(Amqp0ReplyCode.ChannelError, "CHANNEL_ERROR - channel already open"),
        [(Amqp0ChannelMethodId.ClassId, Amqp0ChannelMethodId.Flow)] = (connection, channel, arguments) => connection.AnswerFlow(channel, arguments),
        [(Amqp0ChannelMethodId.ClassId, Amqp0ChannelMethodId.Close)] = (connection, channel, _) => connection.AnswerChannelClose(channel),
        [(Amqp0ExchangeMethodId.ClassId, Amqp0ExchangeMethodId.Declare)] = (connection, channel, arguments) => connection.DeclareExchange(channel, arguments),
        [(Amqp0ExchangeMethodId.ClassId, Amqp0ExchangeMethodId.Delete)] = (connection, channel, arguments) => connection.DeleteExchange(channel, arguments),
        [(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.Declare)] = (connection, channel, arguments) => connection.DeclareQueue(channel, arguments),
        [(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.Bind)] = (connection, channel, arguments) => connection.BindQueue(channel, arguments, bind: true),
        [(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.Unbind)] = (connection, channel, arguments) => connection.BindQueue(channel, arguments, bind: false),
        [(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.Purge)] = (connection, channel, arguments) => connection.PurgeQueue(channel, arguments),
        [(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.Delete)] = (connection, channel, arguments) => connection.DeleteQueue(channel, arguments),
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Qos)] = (connection, channel, arguments) => connection.AnswerQos(channel, arguments),
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Consume)] = (connection, channel, arguments) => connection.Consume(channel, arguments),
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Cancel)] = (connection, channel, arguments) => connection.CancelConsumer(channel, arguments),
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Publish)] = (connection, channel, arguments) => connection.BeginPublish(channel, arguments),
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Get)] = (connection, channel, arguments) => connection.Get(channel, arguments),
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Ack)] = (connection, channel, arguments) => connection.Acknowledge(channel, arguments),
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Reject)] = (connection, channel, arguments) => connection.Reject(channel, arguments),
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Nack)] = (connection, channel, arguments) => connection.Nack(channel, arguments),
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Recover)] = (connection, channel, arguments) => connection.Recover(channel, arguments, answer: true),
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.RecoverAsync)] = (connection, channel, arguments) => connection.Recover(channel, arguments, answer: false),
    };

    private static readonly Amqp0MethodPayload ChannelOpenOk = new(Amqp0ChannelMethodId.ClassId, Amqp0ChannelMethodId.OpenOk, new byte[4]);
    private static readonly Amqp0MethodPayload ChannelCloseOk = new(Amqp0ChannelMethodId.ClassId, Amqp0ChannelMethodId.CloseOk, ReadOnlyMemory<byte>.Empty);

    private readonly Dictionary<ushort, Amqp0Channel> channels = [];

    // Answers one frame on a channel other than 0; returns whether the connection stays open.
    private async ValueTask<bool> AnswerChannelFrameAsync(Amqp0Frame frame)
    {
        try
        {
            AnswerOnChannel(frame);
            return true;
        }
        catch (Amqp0ConnectionException exception)
        {
            await CloseAsync(exception.ReplyCode, exception.Message, exception.ClassId, exception.MethodId);
            return false;
        }
    }

    private void AnswerOnChannel(Amqp0Frame frame)
    {
        if (!channels.TryGetValue(frame.Channel, out var channel))
        {
            OpenChannel(frame);
        }
        else if (channel.Closing)
        {
            AnswerClosingChannel(channel, frame);
        }
        else if (frame.Type == Amqp0FrameType.Method)
        {
            AnswerChannelMethod(channel, ReadChannelMethod(frame));
        }
        else
        {
            AnswerContentFrame(channel, frame);
        }
    }

    private static Amqp0MethodPayload ReadChannelMethod(Amqp0Frame frame)
    {
        try
        {
            return Amqp0MethodPayload.Read(frame.Payload);
        }
        catch (Amqp0SyntaxException)
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.SyntaxError, "SYNTAX_ERROR - malformed method");
        }
    }

    // channel.open on a free channel up to the negotiated channel-max; anything else is 504.
    private void OpenChannel(Amqp0Frame frame)
    {
        var method = frame.Type == Amqp0FrameType.Method ? ReadMethodOrNull(frame) : null;
        if (method is not { ClassId: Amqp0ChannelMethodId.ClassId, MethodId: Amqp0ChannelMethodId.Open })
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.ChannelError, "CHANNEL_ERROR - channel not open", method?.ClassId ?? 0, method?.MethodId ?? 0);
        }

        if (frame.Channel > channelMax)
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.ChannelError, "CHANNEL_ERROR - channel above channel-max", method.ClassId, method.MethodId);
        }

        channels.Add(frame.Channel, new(frame.Channel));
        SendMethod(frame.Channel, ChannelOpenOk);
    }

    // After the broker's channel.close only close-ok and close count; every other frame is discarded.
    private void AnswerClosingChannel(Amqp0Channel channel, Amqp0Frame frame)
    {
        if (frame.Type != Amqp0FrameType.Method || ReadMethodOrNull(frame) is not { ClassId: Amqp0ChannelMethodId.ClassId } method)
        {
            return;
        }

        if (method.MethodId == Amqp0ChannelMethodId.Close)
        {
            SendMethod(channel.Number, ChannelCloseOk);
            channels.Remove(channel.Number);
        }
        else if (method.MethodId == Amqp0ChannelMethodId.CloseOk)
        {
            channels.Remove(channel.Number);
        }
    }

    private void AnswerChannelMethod(Amqp0Channel channel, Amqp0MethodPayload method)
    {
        try
        {
            if (channel.Publish is not null)
            {
                throw new Amqp0ConnectionException(Amqp0ReplyCode.UnexpectedFrame, "UNEXPECTED_FRAME - method before the content was complete");
            }

            if (!MethodAnswers.TryGetValue((method.ClassId, method.MethodId), out var answer))
            {
                throw Unanswerable(method);
            }

            answer(this, channel, new Amqp0WireReader(method.Arguments));
        }
        catch (Amqp0SyntaxException)
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.SyntaxError, "SYNTAX_ERROR - malformed method", method.ClassId, method.MethodId);
        }
        catch (Amqp0ConnectionException exception)
        {
            throw exception.Naming(method.ClassId, method.MethodId);
        }
        catch (Amqp0ChannelException exception)
        {
            CloseChannel(channel, exception.ReplyCode, exception.Message, method.ClassId, method.MethodId);
        }
    }

    private static Amqp0ConnectionException Unanswerable(Amqp0MethodPayload method) =>
        method.ClassId == Amqp0ConnectionMethodId.ClassId || ClientMayNotSend.Contains((method.ClassId, method.MethodId))
            ? new(Amqp0ReplyCode.CommandInvalid, "COMMAND_INVALID - method not valid from a client")
            : new(Amqp0ReplyCode.NotImplemented, "NOT_IMPLEMENTED - method not served");

    // channel.flow from the client: 0 holds the channel's deliveries, 1 sends them on.
    private void AnswerFlow(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        var active = arguments.ReadBits(1)[0];
        lock (channel.Gate)
        {
            channel.FlowActive = active;
            if (active)
            {
                channel.Held.ForEach(Send);
                channel.Held.Clear();
            }
        }

        SendMethod(channel.Number, MethodOf(Amqp0ChannelMethodId.ClassId, Amqp0ChannelMethodId.FlowOk, writer => writer.WriteBits([active])));
    }

    private void AnswerChannelClose(Amqp0Channel channel)
    {
        ReleaseChannel(channel);
        channels.Remove(channel.Number);
        SendMethod(channel.Number, ChannelCloseOk);
    }

    // A channel exception: the channel's consumers end, its deliveries go back, and channel.close is sent.
    private void CloseChannel(Amqp0Channel channel, Amqp0ReplyCode replyCode, string replyText, ushort classId, ushort methodId)
    {
        context.Log.Note($"Closed channel {channel.Number} with {(ushort)replyCode} ({replyText}).");
        ReleaseChannel(channel);
        channel.Closing = true;
        var close = new Amqp0ConnectionClose((ushort)replyCode, replyText, classId, methodId).ToMethod();
        SendMethod(channel.Number, close with { ClassId = Amqp0ChannelMethodId.ClassId, MethodId = Amqp0ChannelMethodId.Close });
    }

    private void ReleaseChannel(Amqp0Channel channel)
    {
        ReleasePendingContent(channel);
        foreach (var consumer in channel.Consumers.Values)
        {
            consumer.Bus.Dispose();
        }

        channel.Stopped.ForEach(stopped => stopped.Dispose());
        channel.Consumers.Clear();
        channel.Stopped.Clear();
        foreach (var get in TakeOutstanding(channel, 0, multiple: true).Where(delivery => delivery.Consumer is null))
        {
            Bus.RejectGet(get.QueueName, get.BusDeliveryTag, requeue: true);
        }
    }

    private void SendMethod(ushort channel, Amqp0MethodPayload method) => Send(Amqp0Frame.ForMethod(channel, method).Encode());

    private static Amqp0MethodPayload MethodOf(ushort classId, ushort methodId, Action<Amqp0WireWriter> write)
    {
        var writer = new Amqp0WireWriter();
        write(writer);
        return new(classId, methodId, writer.ToArray());
    }
}
