namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The open connection: its frames, the client's <c>close</c>, and the broker's own
/// <c>connection.close</c> (ADR-0112 decision 7).
/// </summary>
internal sealed partial class Amqp0BrokerConnection
{
    private async Task ServeOpenedAsync()
    {
        while (true)
        {
            var read = await ReadFrameAsync(context.CancellationToken);
            if (read is null)
            {
                return;
            }

            if (read.Frame is not { } frame)
            {
                await CloseForUnreadFrameAsync(read);
                return;
            }

            if (!await AnswerFrameAsync(frame))
            {
                return;
            }
        }
    }

    private async ValueTask CloseForUnreadFrameAsync(Amqp0FrameReadResult read)
    {
        if (read.RefusalReplyCode is { } replyCode)
        {
            await CloseAsync(replyCode, "FRAME_ERROR - malformed frame", 0, 0);
            return;
        }

        context.Log.Note($"The client closed the connection without connection.close ({read.Outcome}).");
    }

    // Answers one frame; returns whether the connection stays open.
    private async ValueTask<bool> AnswerFrameAsync(Amqp0Frame frame)
    {
        if (frame.Type == Amqp0FrameType.Heartbeat)
        {
            return true;
        }

        if (frame.Channel != 0)
        {
            return await CloseForUnopenedChannelAsync(frame);
        }

        if (frame.Type != Amqp0FrameType.Method)
        {
            return await CloseFalseAsync(Amqp0ReplyCode.UnexpectedFrame, "UNEXPECTED_FRAME - content frame on channel 0", 0, 0);
        }

        return ReadMethodOrNull(frame) is { } method
            ? await AnswerConnectionMethodAsync(method)
            : await CloseFalseAsync(Amqp0ReplyCode.SyntaxError, "SYNTAX_ERROR - malformed method", 0, 0);
    }

    // No channel is open: channel.open is BL-506's.
    private ValueTask<bool> CloseForUnopenedChannelAsync(Amqp0Frame frame)
    {
        var method = frame.Type == Amqp0FrameType.Method ? ReadMethodOrNull(frame) : null;
        return CloseFalseAsync(Amqp0ReplyCode.ChannelError, "CHANNEL_ERROR - channel not open", method?.ClassId ?? 0, method?.MethodId ?? 0);
    }

    private async ValueTask<bool> AnswerConnectionMethodAsync(Amqp0MethodPayload method)
    {
        if (IsConnectionMethod(method, Amqp0ConnectionMethodId.Close))
        {
            context.Log.Note("The client sent connection.close; answered close-ok.");
            await WriteMethodAsync(CloseOkMethod, context.CancellationToken);
            return false;
        }

        if (IsConnectionMethod(method, Amqp0ConnectionMethodId.CloseOk))
        {
            context.Log.Note("The client sent connection.close-ok unasked; closed.");
            return false;
        }

        return await CloseFalseAsync(Amqp0ReplyCode.CommandInvalid, "COMMAND_INVALID - method not valid on channel 0", method.ClassId, method.MethodId);
    }

    private async ValueTask<bool> CloseFalseAsync(Amqp0ReplyCode replyCode, string replyText, ushort classId, ushort methodId)
    {
        await CloseAsync(replyCode, replyText, classId, methodId);
        return false;
    }

    // Sends connection.close, then discards every frame until close-ok or close, for one second at most.
    private async ValueTask CloseAsync(Amqp0ReplyCode replyCode, string replyText, ushort classId, ushort methodId)
    {
        context.Log.Note($"Closed the connection with {(ushort)replyCode} ({replyText}).");
        var close = new Amqp0ConnectionClose((ushort)replyCode, replyText, classId, methodId);
        await WriteMethodAsync(close.ToMethod(), context.CancellationToken);

        using var wait = new CancellationTokenSource(CloseOkWait, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, wait.Token);
        try
        {
            while (await ReadFrameAsync(cancellation.Token) is { Frame: { } frame } && !await EndsCloseAsync(frame))
            {
            }
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("No connection.close-ok arrived within one second.");
        }
    }

    // close-ok ends the close; so does the client's own close, answered close-ok.
    private async ValueTask<bool> EndsCloseAsync(Amqp0Frame frame)
    {
        if (frame.Type != Amqp0FrameType.Method || frame.Channel != 0 || ReadMethodOrNull(frame) is not { } method)
        {
            return false;
        }

        if (IsConnectionMethod(method, Amqp0ConnectionMethodId.Close))
        {
            await WriteMethodAsync(CloseOkMethod, context.CancellationToken);
            return true;
        }

        return IsConnectionMethod(method, Amqp0ConnectionMethodId.CloseOk);
    }

    private static Amqp0MethodPayload CloseOkMethod { get; } = new(Amqp0ConnectionMethodId.ClassId, Amqp0ConnectionMethodId.CloseOk, ReadOnlyMemory<byte>.Empty);

    private static bool IsConnectionMethod(Amqp0MethodPayload method, ushort methodId) =>
        method.ClassId == Amqp0ConnectionMethodId.ClassId && method.MethodId == methodId;
}
