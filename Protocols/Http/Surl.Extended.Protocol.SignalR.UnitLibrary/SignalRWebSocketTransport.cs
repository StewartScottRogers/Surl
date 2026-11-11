using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using Surl.WebSocket;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The WebSockets transport: reads client frames into messages (answering a Ping with a Pong), writes each
/// record as one message, and ends with a WebSocket Close (TransportProtocols.md "WebSockets"; RFC 6455;
/// ADR-0119, decisions 2 and 4).
/// </summary>
internal sealed class SignalRWebSocketTransport : ISignalRHubTransport
{
    // The longest header a client frame can have; --max-message bounds the payload alone.
    private const int MaxClientFrameHeaderBytes = 14;

    private readonly IConnection connection;
    private readonly WebSocketFrameReader frameReader;
    private readonly WebSocketMessageReassembler reassembler;
    private readonly WebSocketLingeringClose lingeringClose;
    private readonly ExchangeContext context;

    /// <summary>
    /// Creates the transport that follows one <c>101</c>.
    /// </summary>
    /// <param name="connection">Where frames are written.</param>
    /// <param name="reader">The reader that read the upgrade request, so bytes it already buffered come first.</param>
    /// <param name="context">The exchange: its limits and clock.</param>
    public SignalRWebSocketTransport(IConnection connection, HttpConnectionReader reader, ExchangeContext context)
    {
        this.connection = connection;
        this.context = context;
        var maxMessageBytes = context.Limits.MaxMessageBytes;
        frameReader = new WebSocketFrameReader(reader.ReadAsync, maxMessageBytes == 0 ? 0 : maxMessageBytes + MaxClientFrameHeaderBytes);
        reassembler = new WebSocketMessageReassembler(maxMessageBytes);
        lingeringClose = new WebSocketLingeringClose(reader, context);
    }

    /// <inheritdoc/>
    public SemaphoreSlim WriteGate { get; } = new(1, 1);

    /// <inheritdoc/>
    public bool CarriesBinary => true;

    /// <inheritdoc/>
    public void Dispose() => WriteGate.Dispose();

    /// <inheritdoc/>
    public async Task<SignalRTransportRead> ReadAsync(CancellationToken token)
    {
        while (true)
        {
            var result = await frameReader.ReadFrameAsync(token);
            var read = result.Frame is { } frame ? await AcceptFrameAsync(frame, token) : SignalRTransportRead.Ended(FrameNotRead(result.Outcome));
            if (read is not null)
            {
                return read;
            }
        }
    }

    /// <inheritdoc/>
    public Task WriteRecordAsync(byte[] record, bool isBinary, CancellationToken token) =>
        connection.WriteAsync(WebSocketFrameEncoder.EncodeFrame(true, Opcode(isBinary), record), token).AsTask();

    /// <inheritdoc/>
    public async Task WriteEndAsync(SignalRSessionEnd end, byte[]? hubClose, bool isBinary, CancellationToken token)
    {
        if (end.CloseCode is not { } code)
        {
            return;
        }

        if (end.IsFarewell)
        {
            await SayFarewellAsync(end, code, hubClose, isBinary);

            return;
        }

        await WriteCloseAsync(code, hubClose, isBinary, token);
        await lingeringClose.LingerAsync(token);
    }

    private async Task WriteCloseAsync(ushort code, byte[]? hubClose, bool isBinary, CancellationToken token)
    {
        if (hubClose is not null)
        {
            await connection.WriteAsync(WebSocketFrameEncoder.EncodeFrame(true, Opcode(isBinary), hubClose), token);
        }

        await connection.WriteAsync(WebSocketFrameEncoder.EncodeClose(code, string.Empty), token);
        await connection.CompleteWritesAsync(token);
    }

    // ADR-0119 decision 7 as its Amendment 2 reads it, after ADR-0059: the exchange's token is already
    // cancelled, so the farewell has a deadline of its own - cut short by shutdown when a limit
    // cancelled the exchange - and the lingering close follows only at a limit.
    private async Task SayFarewellAsync(SignalRSessionEnd end, ushort code, byte[]? hubClose, bool isBinary)
    {
        var isShutdown = context.ShutdownToken.IsCancellationRequested;
        using var deadline = new CancellationTokenSource(SignalRHubSession.FarewellWriteDeadline, context.TimeProvider);
        using var deadlineOrShutdown = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, isShutdown ? CancellationToken.None : context.ShutdownToken);
        try
        {
            await WriteCloseAsync(code, hubClose, isBinary, deadlineOrShutdown.Token);
        }
        catch (OperationCanceledException)
        {
            context.Log.Note($"The farewell was not written within its {SignalRHubSession.FarewellWriteDeadline.TotalSeconds}-second deadline, or shutdown cut it short; the connection is closed without it.");

            return;
        }

        if (!isShutdown)
        {
            await lingeringClose.LingerAsync(context.ShutdownToken);
        }
    }

    private static WebSocketOpcode Opcode(bool isBinary) => isBinary ? WebSocketOpcode.Binary : WebSocketOpcode.Text;

    private static SignalRSessionEnd FrameNotRead(WebSocketFrameReadOutcome outcome) => outcome switch
    {
        WebSocketFrameReadOutcome.ConnectionClosed => SignalRSessionEnd.Silent("the client closed the connection without a WebSocket Close"),
        WebSocketFrameReadOutcome.FrameTooLarge => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.MessageTooBig, "a client frame is past --max-message"),
        WebSocketFrameReadOutcome.CloseReasonNotUtf8 => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.InvalidPayload, "a client Close's reason is not valid UTF-8"),
        _ => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.ProtocolError, $"a client frame is malformed ({outcome})"),
    };

    // Every client frame is masked and sets no reserved bit, since no extension was agreed (RFC 6455 section 5).
    private static bool IsUnmaskedOrReserved(WebSocketFrame frame) => !frame.Masked || frame.Rsv1 || frame.Rsv2 || frame.Rsv3;

    private static SignalRSessionEnd ReassemblyFailure(WebSocketReassemblyOutcome outcome) => outcome switch
    {
        WebSocketReassemblyOutcome.MessageTooLarge => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.MessageTooBig, "a client message is past --max-message"),
        WebSocketReassemblyOutcome.TextNotUtf8 => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.InvalidPayload, "a client text message is not valid UTF-8"),
        _ => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.ProtocolError, $"a client frame is out of its message's order ({outcome})"),
    };

    // Null when the frame leads to no message and no end, so the next frame is read.
    private async Task<SignalRTransportRead?> AcceptFrameAsync(WebSocketFrame frame, CancellationToken token)
    {
        if (IsUnmaskedOrReserved(frame))
        {
            return SignalRTransportRead.Ended(SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.ProtocolError, "a client frame is unmasked or sets a reserved bit"));
        }

        var step = reassembler.Accept(frame);
        if (step.Outcome == WebSocketReassemblyOutcome.FragmentHeld)
        {
            return null;
        }

        if (step.Outcome == WebSocketReassemblyOutcome.MessageComplete)
        {
            return SignalRTransportRead.OfPayload(step.Message!.Payload);
        }

        return step.Outcome == WebSocketReassemblyOutcome.ControlFrame
            ? await AcceptControlFrameAsync(step.ControlFrame!, token)
            : SignalRTransportRead.Ended(ReassemblyFailure(step.Outcome));
    }

    // A WebSocket Ping is answered with a Pong and a Pong ignored; neither is a hub record, so
    // neither restarts the client timeout (ADR-0119, decision 4).
    private async Task<SignalRTransportRead?> AcceptControlFrameAsync(WebSocketFrame frame, CancellationToken token)
    {
        if (frame.Opcode == WebSocketOpcode.Close)
        {
            return SignalRTransportRead.Ended(SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.NormalClosure, "the client sent a WebSocket Close"));
        }

        if (frame.Opcode == WebSocketOpcode.Ping)
        {
            await WriteGate.WaitAsync(token);
            try
            {
                await connection.WriteAsync(WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Pong, frame.Payload), token);
            }
            finally
            {
                WriteGate.Release();
            }
        }

        return null;
    }
}
