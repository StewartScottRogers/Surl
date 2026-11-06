using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smb;

/// <summary>
/// One SMB connection from its first frame to its close: reads each NetBIOS frame, has
/// <see cref="SmbSession"/> answer each SMB message, and applies ADR-0073 decisions 5 and 7 -
/// keep answering, the head timeout, <c>--max-message</c>, and a limit told from shutdown as
/// ADR-0059 says.
/// </summary>
internal sealed class SmbExchange
{
    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly SmbSession session;
    private readonly SmbFrameReader reader;
    private SmbHeader? requestBeingAnswered;

    /// <summary>
    /// Creates the exchange of one connection.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="context">The exchange's log, clock, limits and cancellation.</param>
    /// <param name="session">The session that answers each request.</param>
    public SmbExchange(IConnection connection, ExchangeContext context, SmbSession session)
    {
        this.connection = connection;
        this.context = context;
        this.session = session;
        reader = new SmbFrameReader(connection, context.Limits.MaxMessageBytes);
    }

    /// <summary>
    /// Answers every SMB message until the client closes the connection, the session closes it,
    /// the head timeout passes, or the engine cancels the exchange.
    /// </summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    /// <exception cref="OperationCanceledException">The engine cancelled the exchange at shutdown.</exception>
    public async Task ServeAsync()
    {
        try
        {
            await AnswerEveryFrameAsync();
        }
        catch (OperationCanceledException) when (context.IsCancelledForALimit)
        {
            await CloseForALimitAsync();
        }
    }

    private async Task AnswerEveryFrameAsync()
    {
        var frame = await ReadFirstMessageAsync();
        while (frame is not null && await AnswerFrameAsync(frame))
        {
            frame = await reader.ReadFrameAsync(context.CancellationToken);
        }
    }

    // The negotiate must arrive within the head timeout (ADR-0006's first packet); a
    // keep-alive does not stop its clock. Null when the head timeout passed.
    private async Task<SmbFrameReadResult?> ReadFirstMessageAsync()
    {
        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            SmbFrameReadResult frame;
            do
            {
                frame = await reader.ReadFrameAsync(cancellation.Token);
            }
            while (frame.Outcome == SmbFrameReadOutcome.KeepAlive);

            return frame;
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("SMB connection closed: head timeout");
            return null;
        }
    }

    // Whether the connection stays open for the next frame.
    private async Task<bool> AnswerFrameAsync(SmbFrameReadResult frame) => frame.Outcome switch
    {
        SmbFrameReadOutcome.MessageRead => await AnswerMessageAsync(frame.Message),
        SmbFrameReadOutcome.KeepAlive => true,
        SmbFrameReadOutcome.MessageTooLarge => await AnswerMessageTooLargeAsync(frame),
        _ => false,
    };

    private async Task<bool> AnswerMessageAsync(byte[] message)
    {
        var decoding = SmbRequestDecoder.Decode(message);
        if (decoding.Header is not { } header)
        {
            return false;
        }

        requestBeingAnswered = header;
        var answer = decoding.Request is { } request
            ? await session.AnswerAsync(request)
            : session.AnswerFault(decoding.Fault, header);
        return await WriteAnswerAsync(answer);
    }

    // Answered ERRSRV/ERRerror once its header is in, and the session goes on, so curl's close
    // and tree disconnect are still answered (ADR-0073, decision 7).
    private async Task<bool> AnswerMessageTooLargeAsync(SmbFrameReadResult frame)
    {
        context.Log.Note($"SMB message of {frame.MessageLength} bytes is past --max-message {context.Limits.MaxMessageBytes}");
        if (frame.Message.Length < SmbHeader.Length || !frame.Message.AsSpan().StartsWith(SmbHeader.ProtocolIdentifier))
        {
            return false;
        }

        var header = SmbHeader.Read(frame.Message);
        requestBeingAnswered = header;
        return await WriteAnswerAsync(new SmbAnswer(SmbResponseEncoder.EncodeError(header, SmbStatus.ServerError)));
    }

    private async Task<bool> WriteAnswerAsync(SmbAnswer answer)
    {
        await connection.WriteAsync(answer.Response, context.CancellationToken);
        requestBeingAnswered = null;
        if (answer.ClosesConnection)
        {
            await connection.CompleteWritesAsync(context.CancellationToken);
        }

        return !answer.ClosesConnection;
    }

    // A request being answered gets ERRSRV/ERRerror within the one-second deadline, linked to
    // shutdown rather than to the cancelled exchange; a connection on which curl waits for
    // nothing is closed with nothing sent (ADR-0059; ADR-0073, decision 7).
    private async Task CloseForALimitAsync()
    {
        context.Log.Note("SMB connection closed: idle timeout or maximum duration");
        if (requestBeingAnswered is not { } header)
        {
            return;
        }

        using var deadline = new CancellationTokenSource(SmbProtocolServer.LimitReplyWriteDeadline, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.ShutdownToken, deadline.Token);
        try
        {
            await connection.WriteAsync(SmbResponseEncoder.EncodeError(header, SmbStatus.ServerError), cancellation.Token);
            await connection.CompleteWritesAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.ShutdownToken.IsCancellationRequested)
        {
            context.Log.Note("The reply was not written within the one-second write deadline; the connection was closed.");
        }
    }
}
