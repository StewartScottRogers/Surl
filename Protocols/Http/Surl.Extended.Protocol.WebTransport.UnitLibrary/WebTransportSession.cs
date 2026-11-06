using Surl.Http3;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// One accepted WebTransport session, served as the echo service ADR-0117 decisions 3 and 6
/// describe: each client stream's bytes come back (a bidirectional stream on itself, a
/// unidirectional one on a server unidirectional stream of the session), each HTTP datagram and
/// <c>DATAGRAM</c> capsule comes back unchanged, and the session ends with the client's
/// <c>WT_CLOSE_SESSION</c>, its FIN or reset of the CONNECT stream, or shutdown.
/// </summary>
/// <remarks>
/// <para>
/// When the session ends, every stream still open is ended with <c>WT_SESSION_GONE</c>: the server's
/// half by <c>RESET_STREAM_AT</c> keeping its stream header, the client's by <c>STOP_SENDING</c>.
/// Shutdown (the exchange cancelled) sends <c>WT_DRAIN_SESSION</c>, then <c>WT_CLOSE_SESSION</c>
/// code 0 <c>server shutting down</c> and FIN, before <c>Surl.Http3</c> sends <c>GOAWAY</c>.
/// </para>
/// <para>
/// A client reset of a stream it sends on is mirrored on the half the server writes for it, with the
/// same code, after the bytes already echoed; a client <c>STOP_SENDING</c> on the server's half
/// (which <c>Surl.Quic</c> answers with a reset) is answered with <c>STOP_SENDING</c> of the same code
/// on the client's half. Flow-control capsules and unknown capsule types are ignored (WTH3 5.1, with
/// flow control off); a capsule that breaks its layout resets the CONNECT stream
/// <c>H3_MESSAGE_ERROR</c>, as does any capsule after <c>WT_CLOSE_SESSION</c>.
/// </para>
/// </remarks>
internal sealed class WebTransportSession : IDisposable
{
    /// <summary>The most bytes one read of a client stream takes before they are echoed.</summary>
    public const int EchoChunkLength = 65536;

    /// <summary>The message of the server's own <c>WT_CLOSE_SESSION</c> at shutdown (ADR-0117 decision 6).</summary>
    public const string ShutdownMessage = "server shutting down";

    private readonly Http3ExtendedConnectSession session;
    private readonly ExchangeContext context;
    private readonly CancellationTokenSource ending = new();
    private readonly CancellationToken endingToken;
    private readonly Lock gate = new();
    private readonly List<Task> echoes = [];

    /// <summary>Creates the session's server.</summary>
    /// <param name="session">The session <c>Surl.Http3</c> opened, answered <c>200</c> already.</param>
    /// <param name="context">The exchange: its log.</param>
    public WebTransportSession(Http3ExtendedConnectSession session, ExchangeContext context)
    {
        this.session = session;
        this.context = context;
        endingToken = ending.Token;
    }

    /// <summary>Serves the session until it ends.</summary>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
    /// <returns>A task that completes when the session and its streams are over.</returns>
    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        using var linked = cancellationToken.Register(ending.Cancel);
        var accepting = AcceptStreamsAsync();
        var datagrams = EchoDatagramsAsync();
        var reading = ReadCapsulesAsync(cancellationToken);
        await reading.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await EndStreamsAsync();
        await Task.WhenAll(accepting, datagrams);
        await reading;
    }

    /// <summary>Echoes one client stream of the session until it ends or the session does.</summary>
    /// <param name="stream">The client stream, read past its header.</param>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
    /// <returns>A task that completes when the stream is over.</returns>
    public async Task ServeStreamAsync(Http3SessionStream stream, CancellationToken cancellationToken)
    {
        using var token = CancellationTokenSource.CreateLinkedTokenSource(endingToken, cancellationToken);
        var answer = stream.Stream.IsBidirectional ? stream : null;
        try
        {
            answer ??= await session.OpenUnidirectionalStreamAsync(token.Token);
            await (answer is null ? RefuseUnansweredAsync(stream, token.Token) : EchoAsync(stream, answer, token.Token));
        }
        catch (OperationCanceledException)
        {
            await EndForSessionAsync(stream, answer);
        }
    }

    /// <inheritdoc />
    public void Dispose() => ending.Dispose();

    // The bytes of a server unidirectional stream's header (0x54 and the session ID), which a reset keeps (WTH3 4.4).
    private ulong ReliableHeaderLength(Http3SessionStream answer, Http3SessionStream stream) =>
        answer == stream ? 0 : (ulong)new WebTransportStreamHeader(WebTransportStreamKind.Unidirectional, session.Id).Encode().Length;

    private async Task AcceptStreamsAsync()
    {
        try
        {
            // AcceptStreamAsync returns null only after ServeSessionAsync returns, by which time the
            // session has been ended and this loop cancelled.
            while (true)
            {
                var stream = await session.AcceptStreamAsync(endingToken);
                lock (gate)
                {
                    echoes.Add(ServeStreamAsync(stream!, CancellationToken.None));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The session has ended.
        }
    }

    private async Task EchoDatagramsAsync()
    {
        try
        {
            // ReceiveDatagramAsync returns null only once the session has ended, and the send then throws.
            while (true)
            {
                ReadOnlyMemory<byte> payload = await session.ReceiveDatagramAsync(endingToken);
                await SendDatagramAsync(payload);
            }
        }
        catch (OperationCanceledException)
        {
            // The session has ended.
        }
    }

    // RFC 9221 section 5: a datagram the client cannot take is dropped.
    private async Task SendDatagramAsync(ReadOnlyMemory<byte> payload)
    {
        try
        {
            await session.SendDatagramAsync(payload, endingToken);
        }
        catch (InvalidOperationException)
        {
            context.Log.Note($"A datagram of session {session.Id} was dropped: the client takes no DATAGRAM frames.");
        }
        catch (ArgumentOutOfRangeException)
        {
            context.Log.Note($"A datagram of {payload.Length} bytes of session {session.Id} was dropped: it is larger than the client takes.");
        }
    }

    private async Task ReadCapsulesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await session.ReadCapsuleAsync(cancellationToken) is { } capsule)
            {
                if (!await TakeCapsuleAsync(capsule, cancellationToken))
                {
                    return;
                }
            }

            await EndByClientAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await CloseForShutdownAsync();
        }
    }

    // False once the capsule has ended the session.
    private async Task<bool> TakeCapsuleAsync(Http3Capsule capsule, CancellationToken cancellationToken)
    {
        switch (WebTransportCapsuleReader.Decode((WebTransportCapsuleType)capsule.Type, capsule.Value))
        {
            case null:
                await ResetConnectStreamAsync($"a capsule of type 0x{capsule.Type:x} broke its layout", cancellationToken);
                return false;
            case WebTransportCloseSessionCapsule close:
                await CloseByClientAsync(close, cancellationToken);
                return false;
            case WebTransportDrainSessionCapsule:
                context.Log.Note($"The client sent WT_DRAIN_SESSION on session {session.Id}; it is still served.");
                return true;
            case WebTransportDatagramCapsule:
                await session.WriteCapsuleAsync(capsule, cancellationToken);
                return true;
            default:
                return true;
        }
    }

    // WTH3 6: the session's streams end, then the CONNECT stream - by FIN, unless more followed the capsule.
    private async Task CloseByClientAsync(WebTransportCloseSessionCapsule close, CancellationToken cancellationToken)
    {
        context.Log.Note($"The client closed session {session.Id} with code {close.ErrorCode}: {close.Message}");
        await EndStreamsAsync();
        if (await session.ReadCapsuleAsync(cancellationToken) is not null)
        {
            await ResetConnectStreamAsync("a capsule followed WT_CLOSE_SESSION", cancellationToken);
            return;
        }

        await FinishConnectStreamAsync(cancellationToken);
    }

    // WTH3 6: the client's FIN is WT_CLOSE_SESSION code 0; its reset ends the session too.
    private async Task EndByClientAsync(CancellationToken cancellationToken)
    {
        context.Log.Note($"The client ended session {session.Id}'s CONNECT stream.");
        await EndStreamsAsync();
        if (session.RequestStream.HasRequestBodyEndedEarly)
        {
            await session.RequestStream.ResetResponseAsync((Http3ErrorCode)WebTransportHttp3Codes.SessionGone, cancellationToken);
            return;
        }

        await FinishConnectStreamAsync(cancellationToken);
    }

    // WTH3 4.7 and 6: WT_DRAIN_SESSION, then WT_CLOSE_SESSION 0 and FIN; GOAWAY follows from Surl.Http3.
    private async Task CloseForShutdownAsync()
    {
        context.Log.Note($"Session {session.Id} was drained and closed for shutdown.");
        await session.WriteCapsuleAsync(ToHttp3Capsule(new WebTransportDrainSessionCapsule()), CancellationToken.None);
        await session.WriteCapsuleAsync(ToHttp3Capsule(new WebTransportCloseSessionCapsule(0, ShutdownMessage)), CancellationToken.None);
        await session.RequestStream.WriteDataAsync(ReadOnlyMemory<byte>.Empty, endStream: true, CancellationToken.None);
    }

    private async Task ResetConnectStreamAsync(string reason, CancellationToken cancellationToken)
    {
        await EndStreamsAsync();
        await session.RequestStream.ResetAsync(Http3ErrorCode.MessageError, reason, cancellationToken);
    }

    // A CONNECT stream Surl.Http3 has reset already (a capsule past --max-message, or cut short) takes no FIN.
    private async Task FinishConnectStreamAsync(CancellationToken cancellationToken)
    {
        try
        {
            await session.RequestStream.WriteDataAsync(ReadOnlyMemory<byte>.Empty, endStream: true, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            context.Log.Note($"Session {session.Id}'s CONNECT stream was reset already.");
        }
    }

    // Cancels every echo, which then ends its stream with WT_SESSION_GONE, and waits for them.
    private async Task EndStreamsAsync()
    {
        await ending.CancelAsync();
        Task[] running;
        lock (gate)
        {
            running = [.. echoes];
        }

        await Task.WhenAll(running);
    }

    private async Task EchoAsync(Http3SessionStream stream, Http3SessionStream answer, CancellationToken cancellationToken)
    {
        var buffer = new byte[EchoChunkLength];
        var written = ReliableHeaderLength(answer, stream);
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (await IsStoppedAsync(stream, answer, cancellationToken))
            {
                return;
            }

            await answer.WriteAsync(buffer.AsMemory(0, count), fin: false, cancellationToken);
            written += (ulong)count;
        }

        await FinishEchoAsync(stream, answer, written, cancellationToken);
    }

    // The client's FIN is answered by FIN, its reset by a reset with the same code after what was echoed.
    private static async Task FinishEchoAsync(Http3SessionStream stream, Http3SessionStream answer, ulong written, CancellationToken cancellationToken)
    {
        if (stream.Stream.ResetErrorCode is { } code)
        {
            await answer.ResetAtAsync(code, written, cancellationToken);
            return;
        }

        if (!await IsStoppedAsync(stream, answer, cancellationToken))
        {
            await answer.WriteAsync(ReadOnlyMemory<byte>.Empty, fin: true, cancellationToken);
        }
    }

    // Whether the client stopped the server's half; its code is sent back on the client's half.
    private static async Task<bool> IsStoppedAsync(Http3SessionStream stream, Http3SessionStream answer, CancellationToken cancellationToken)
    {
        if (answer.Stream.StopSendingErrorCode is not { } code)
        {
            return false;
        }

        await stream.StopSendingAsync(code, cancellationToken);
        return true;
    }

    // No server unidirectional stream is free to answer on (the client's MAX_STREAMS): the client's stream is refused.
    private async Task RefuseUnansweredAsync(Http3SessionStream stream, CancellationToken cancellationToken)
    {
        context.Log.Note($"Stream {stream.Id} of session {session.Id} was stopped with H3_REQUEST_REJECTED: the client allows no server stream to answer it on.");
        await stream.StopSendingAsync((ulong)Http3ErrorCode.RequestRejected, cancellationToken);
    }

    // WTH3 6: the server's half is reset WT_SESSION_GONE (keeping its header), the client's stopped.
    private async Task EndForSessionAsync(Http3SessionStream stream, Http3SessionStream? answer)
    {
        if (answer is not null)
        {
            await answer.ResetAtAsync(WebTransportHttp3Codes.SessionGone, ReliableHeaderLength(answer, stream), CancellationToken.None);
        }

        await stream.StopSendingAsync(WebTransportHttp3Codes.SessionGone, CancellationToken.None);
    }

    private static Http3Capsule ToHttp3Capsule(WebTransportCapsule capsule) => new((ulong)capsule.Type, capsule.EncodeValue());
}
