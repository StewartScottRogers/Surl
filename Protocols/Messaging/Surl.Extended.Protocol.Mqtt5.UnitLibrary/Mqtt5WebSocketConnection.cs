using System.Net;
using Surl.Protocol.Abstractions;
using Surl.WebSocket;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The connection the broker reads and writes on <c>mqtt5+ws</c> once the WebSocket upgrade is
/// accepted: the client's binary frames read as one byte stream, and each write sent as one
/// binary frame (MQTT 5.0 chapter 6, ADR-0093 decision 7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Reading.</b> The payloads of binary frames and their continuation frames are handed over in
/// order with no regard to where a frame ends, so a packet may span frames and a frame may hold
/// several packets (<c>[MQTT-6.0.0-2]</c>). A ping is answered with a pong carrying its payload;
/// a pong is ignored. Each frame, header included, is held to
/// <see cref="ExchangeLimits.MaxMessageBytes"/>.
/// </para>
/// <para>
/// <b>Ending.</b> Reading returns 0 - the end of the stream, which the broker treats as a network
/// loss - after a close is sent or the connection ends: a text frame is answered with close
/// <see cref="UnsupportedDataCloseCode"/> (<c>[MQTT-6.0.0-1]</c>); an unmasked frame, an
/// <c>RSV</c> bit set, a continuation frame with no fragmented message to continue, a binary frame
/// inside one, and any frame RFC 6455 makes invalid are answered with close
/// <see cref="ProtocolErrorCloseCode"/>; a frame over the limit with close
/// <see cref="MessageTooBigCloseCode"/>; and a client's close is answered with a close echoing its
/// status code. Once a close is sent, the broker's writes are dropped: RFC 6455 section 5.5.1
/// allows no data frame after it.
/// </para>
/// <para>
/// Writes, the broker's and the pongs and closes sent while reading, go one at a time.
/// <see cref="CompleteWritesAsync"/> sends close <see cref="NormalClosureCloseCode"/> unless a
/// close was already sent, then half-closes the transport. The connection carries no TLS:
/// <see cref="UpgradeToTlsAsync"/> throws. Disposing it releases nothing: the serving engine
/// disposes the transport.
/// </para>
/// </remarks>
internal sealed class Mqtt5WebSocketConnection : IConnection
{
    /// <summary>Close code 1000, Normal Closure (RFC 6455 section 7.4.1).</summary>
    public const ushort NormalClosureCloseCode = 1000;

    /// <summary>Close code 1002, Protocol Error (RFC 6455 section 7.4.1).</summary>
    public const ushort ProtocolErrorCloseCode = 1002;

    /// <summary>Close code 1003, unsupported data (RFC 6455 section 7.4.1; ADR-0093 decision 7).</summary>
    public const ushort UnsupportedDataCloseCode = 1003;

    /// <summary>Close code 1009, Message Too Big (RFC 6455 section 7.4.1).</summary>
    public const ushort MessageTooBigCloseCode = 1009;

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly WebSocketFrameReader frameReader;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private byte[] payload = [];
    private int payloadOffset;
    private bool continuesFragmentedMessage;
    private bool readingEnded;

    /// <summary>
    /// Creates the framed connection over <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The transport, already past the <c>101</c>.</param>
    /// <param name="readAsync">Reads the bytes after the upgrade request, those the HTTP reader buffered first.</param>
    /// <param name="context">The exchange's limits, log and cancellation.</param>
    public Mqtt5WebSocketConnection(IConnection connection, Func<Memory<byte>, CancellationToken, ValueTask<int>> readAsync, ExchangeContext context)
    {
        this.connection = connection;
        this.context = context;
        frameReader = new WebSocketFrameReader(readAsync, context.Limits.MaxMessageBytes);
    }

    /// <summary>Whether a close frame has been sent; nothing more is written after one.</summary>
    public bool CloseSent { get; private set; }

    /// <inheritdoc/>
    public EndPoint LocalEndPoint => connection.LocalEndPoint;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

    /// <inheritdoc/>
    public TlsSession? TlsSession => connection.TlsSession;

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (buffer.IsEmpty)
        {
            throw new ArgumentException("A read needs a buffer of at least one byte.", nameof(buffer));
        }

        while (payloadOffset == payload.Length)
        {
            if (readingEnded)
            {
                return 0;
            }

            await ReadNextFrameAsync(cancellationToken);
        }

        var count = Math.Min(buffer.Length, payload.Length - payloadOffset);
        payload.AsSpan(payloadOffset, count).CopyTo(buffer.Span);
        payloadOffset += count;
        return count;
    }

    /// <summary>
    /// Sends <paramref name="bytes"/> in one binary frame, unmasked and unfragmented; nothing once
    /// a close has been sent.
    /// </summary>
    /// <param name="bytes">The bytes to send.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes once the frame has been handed to the transport.</returns>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        WriteFrameAsync(WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Binary, bytes.Span), isClose: false, cancellationToken);

    /// <summary>
    /// Sends close <see cref="NormalClosureCloseCode"/> unless a close was already sent, then
    /// half-closes the transport.
    /// </summary>
    /// <param name="cancellationToken">Cuts the close off.</param>
    /// <returns>A task that completes once the transport is half-closed.</returns>
    public async ValueTask CompleteWritesAsync(CancellationToken cancellationToken)
    {
        await SendCloseAsync(WebSocketFrameEncoder.EncodeClose(NormalClosureCloseCode, string.Empty), cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public void Abort() => connection.Abort();

    /// <summary>
    /// Always throws: <c>mqtt5+ws</c> has no TLS (ADR-0093 decision 7).
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="InvalidOperationException">Always.</exception>
    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("mqtt5+ws carries no TLS (ADR-0093 decision 7).");

    /// <summary>
    /// Releases nothing: the serving engine disposes the transport.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // The echoed status code of a client's close: its first two payload bytes, or none.
    private static byte[] CloseEchoing(WebSocketFrame close) =>
        WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Close, close.Payload.AsSpan(0, Math.Min(close.Payload.Length, 2)));

    private static (ushort Code, string Why)? FindRefusal(WebSocketFrame frame, bool continuesFragmentedMessage)
    {
        if (frame.Opcode == WebSocketOpcode.Text)
        {
            return (UnsupportedDataCloseCode, "a text frame arrived ([MQTT-6.0.0-1])");
        }

        if (!frame.Masked || frame.Rsv1 || frame.Rsv2 || frame.Rsv3)
        {
            return (ProtocolErrorCloseCode, "a frame was unmasked or set an RSV bit (RFC 6455 section 5.2)");
        }

        return FindFragmentationRefusal(frame.Opcode, continuesFragmentedMessage);
    }

    private static (ushort Code, string Why)? FindFragmentationRefusal(WebSocketOpcode opcode, bool continuesFragmentedMessage) =>
        (opcode, continuesFragmentedMessage) switch
        {
            (WebSocketOpcode.Continuation, false) => (ProtocolErrorCloseCode, "a continuation frame had no message to continue (RFC 6455 section 5.4)"),
            (WebSocketOpcode.Binary, true) => (ProtocolErrorCloseCode, "a binary frame arrived inside a fragmented message (RFC 6455 section 5.4)"),
            _ => null,
        };

    private async ValueTask ReadNextFrameAsync(CancellationToken cancellationToken)
    {
        var result = await frameReader.ReadFrameAsync(cancellationToken);
        if (result.Frame is not { } frame)
        {
            await EndForFrameNotReadAsync(result.Outcome, cancellationToken);
            return;
        }

        if (FindRefusal(frame, continuesFragmentedMessage) is { } refusal)
        {
            await EndWithCloseAsync(refusal.Code, refusal.Why, cancellationToken);
            return;
        }

        await AcceptFrameAsync(frame, cancellationToken);
    }

    private async ValueTask AcceptFrameAsync(WebSocketFrame frame, CancellationToken cancellationToken)
    {
        switch (frame.Opcode)
        {
            case WebSocketOpcode.Ping:
                await WriteFrameAsync(WebSocketFrameEncoder.EncodeFrame(fin: true, WebSocketOpcode.Pong, frame.Payload), isClose: false, cancellationToken);
                break;
            case WebSocketOpcode.Pong:
                break;
            case WebSocketOpcode.Close:
                context.Log.Note("The client closed the WebSocket; answered with close.");
                readingEnded = true;
                await SendCloseAsync(CloseEchoing(frame), cancellationToken);
                break;
            default:
                payload = frame.Payload;
                payloadOffset = 0;
                continuesFragmentedMessage = !frame.Fin;
                break;
        }
    }

    private async ValueTask EndForFrameNotReadAsync(WebSocketFrameReadOutcome outcome, CancellationToken cancellationToken)
    {
        switch (outcome)
        {
            case WebSocketFrameReadOutcome.ConnectionClosed or WebSocketFrameReadOutcome.ConnectionClosedMidFrame:
                context.Log.Note($"The client closed the connection: {outcome}.");
                readingEnded = true;
                break;
            case WebSocketFrameReadOutcome.FrameTooLarge:
                await EndWithCloseAsync(MessageTooBigCloseCode, "a frame was over the message limit", cancellationToken);
                break;
            default:
                await EndWithCloseAsync(ProtocolErrorCloseCode, $"an invalid frame arrived ({outcome})", cancellationToken);
                break;
        }
    }

    private async ValueTask EndWithCloseAsync(ushort code, string why, CancellationToken cancellationToken)
    {
        context.Log.Note($"WebSocket closed with {code}: {why}.");
        readingEnded = true;
        await SendCloseAsync(WebSocketFrameEncoder.EncodeClose(code, string.Empty), cancellationToken);
    }

    private ValueTask SendCloseAsync(byte[] close, CancellationToken cancellationToken) =>
        WriteFrameAsync(close, isClose: true, cancellationToken);

    private async ValueTask WriteFrameAsync(byte[] frame, bool isClose, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            if (!CloseSent)
            {
                CloseSent = isClose;
                await connection.WriteAsync(frame, cancellationToken);
            }
        }
        finally
        {
            writeGate.Release();
        }
    }
}
