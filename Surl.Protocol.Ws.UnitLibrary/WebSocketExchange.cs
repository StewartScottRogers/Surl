using System.Buffers.Binary;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

/// <summary>
/// Exchanges frames with the client once the <c>101</c> is written (ADR-0071 decisions 4 to 7):
/// sends the file or directory listing the request path names, or echoes every client message
/// under <c>--ws-echo</c>, answers every client frame, and closes.
/// </summary>
/// <remarks>
/// <para>
/// One frame read is always pending while the exchange is open, so client frames are answered
/// between the frames surl sends: a <c>PING</c> with a <c>PONG</c> carrying its payload, a
/// <c>CLOSE</c> with a <c>CLOSE</c> echoing its code (the message being sent stops after the
/// current frame), and a frame or message RFC 6455 makes invalid with a <c>CLOSE</c> carrying
/// <c>1002</c>, <c>1007</c> or <c>1009</c>. A <c>PONG</c> is ignored, and a data message is
/// discarded unless the exchange echoes. While a message is echoed, the next client frame is
/// answered once the echo is sent.
/// </para>
/// <para>
/// A message surl sends is split into frames of at most <see cref="MaxFramePayloadBytes"/>
/// payload bytes: a file as binary, a listing as text (each entry's name, <c>/</c> after a
/// directory, then LF), an echo with the client's opcode. A file is read one frame's bytes at a
/// time; one that cannot be read is answered <c>CLOSE</c> <c>1011</c>. After the entry comes an
/// empty <c>CLOSE</c>, since upstream curl writes a <c>CLOSE</c>'s code into its output.
/// </para>
/// <para>
/// After its <c>CLOSE</c> surl half-closes and lingers for at most one second, never waiting for a
/// <c>CLOSE</c> answer. An exchange cancelled for a limit - its idle timeout or maximum duration -
/// is answered <c>CLOSE</c> <c>1001</c>, written and lingered on ADR-0059's own deadlines; one
/// cancelled at shutdown ends with nothing more written.
/// </para>
/// </remarks>
internal sealed class WebSocketExchange : IDisposable
{
    /// <summary>The most payload bytes one frame surl sends holds (ADR-0071 decision 4).</summary>
    public const int MaxFramePayloadBytes = 65536;

    /// <summary>
    /// How long the <c>CLOSE</c> <c>1001</c> after a limit may take to write (ADR-0059 decision 3).
    /// </summary>
    public static readonly TimeSpan LimitCloseWriteDeadline = TimeSpan.FromSeconds(1);

    // The longest header a client frame can have: two bytes, a 64-bit length and a masking key.
    // The frame reader counts it in its limit; --max-message bounds the payload alone.
    private const int MaxClientFrameHeaderBytes = 14;

    private const ushort GoingAway = 1001;
    private const ushort ProtocolError = 1002;
    private const ushort InvalidPayload = 1007;
    private const ushort MessageTooBig = 1009;
    private const ushort InternalError = 1011;

    private static readonly byte[] EmptyCloseFrame = WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Close, []);

    private static readonly byte[] GoingAwayCloseFrame = WebSocketFrameEncoder.EncodeClose(GoingAway, "");

    // Why each frame RFC 6455 makes invalid whatever the server wants is answered 1002.
    private static readonly Dictionary<WebSocketFrameReadOutcome, string> ProtocolErrorsInFrames = new()
    {
        [WebSocketFrameReadOutcome.ConnectionClosedMidFrame] = "the client closed the connection partway through a frame",
        [WebSocketFrameReadOutcome.ReservedOpcode] = "a client frame has a reserved opcode",
        [WebSocketFrameReadOutcome.FragmentedControlFrame] = "a client control frame is fragmented",
        [WebSocketFrameReadOutcome.ControlFramePayloadTooLong] = "a client control frame's payload is over 125 bytes",
        [WebSocketFrameReadOutcome.PayloadLengthNotMinimal] = "a client frame's payload length is not in its shortest form",
        [WebSocketFrameReadOutcome.PayloadLengthMostSignificantBitSet] = "a client frame's 64-bit payload length has its top bit set",
        [WebSocketFrameReadOutcome.ClosePayloadOneByte] = "a client CLOSE's payload is one byte",
        [WebSocketFrameReadOutcome.CloseCodeNotAllowed] = "a client CLOSE carries a code not allowed on the wire",
    };

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly ContentStore contentStore;
    private readonly WebSocketLingeringClose lingeringClose;
    private readonly WebSocketFrameReader frameReader;
    private readonly WebSocketMessageReassembler reassembler;
    private readonly CancellationTokenSource readCancellation;

    // The client frame being read; null once the client closed its end or surl stopped reading.
    private Task<WebSocketFrameReadResult>? pendingRead;

    // The CLOSE surl ends with, once it is decided.
    private byte[]? closeFrame;

    /// <summary>
    /// Creates the exchange that follows one <c>101</c>.
    /// </summary>
    /// <param name="connection">Where frames are written.</param>
    /// <param name="reader">The reader that read the upgrade request, through which frames are read, so bytes it already buffered come first.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="contentStore">Where the served file or directory is read.</param>
    /// <param name="lingeringClose">The connection's lingering close.</param>
    public WebSocketExchange(IConnection connection, HttpConnectionReader reader, ExchangeContext context, ContentStore contentStore, WebSocketLingeringClose lingeringClose)
    {
        this.connection = connection;
        this.context = context;
        this.contentStore = contentStore;
        this.lingeringClose = lingeringClose;
        var maxMessageBytes = context.Limits.MaxMessageBytes;
        frameReader = new WebSocketFrameReader(reader.ReadAsync, maxMessageBytes == 0 ? 0 : maxMessageBytes + MaxClientFrameHeaderBytes);
        reassembler = new WebSocketMessageReassembler(maxMessageBytes);
        readCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
    }

    /// <summary>
    /// Sends <paramref name="entry"/>, or echoes every client message when it is
    /// <see langword="null"/>, answering client frames throughout, then closes.
    /// </summary>
    /// <param name="entry">The file or directory to send; <see langword="null"/> under <c>--ws-echo</c>.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled at shutdown, or during the lingering close.</exception>
    public async Task RunAsync(WebSocketServedEntry? entry)
    {
        StartReading();
        try
        {
            await (entry is null ? EchoMessagesAsync() : SendEntryAsync(entry));
        }
        catch (OperationCanceledException) when (context.IsCancelledForALimit)
        {
            await StopReadingAsync();
            await SayGoodbyeAtALimitAsync();

            return;
        }
        catch (IOException failure)
        {
            context.Log.Note($"The connection failed ({failure.Message}).");
            await StopReadingAsync();

            return;
        }

        await CloseAsync();
    }

    /// <inheritdoc/>
    public void Dispose() => readCancellation.Dispose();

    private async Task EchoMessagesAsync()
    {
        while (closeFrame is null && pendingRead is { } read)
        {
            await AnswerAsync(await read, echoes: true);
        }
    }

    private async Task SendEntryAsync(WebSocketServedEntry entry)
    {
        var (opcode, length, readSlice) = entry.Status.Kind == ContentEntryKind.File
            ? (WebSocketOpcode.Binary, entry.Status.Length.GetValueOrDefault(), FileSliceReader(entry))
            : ListingMessage(entry);
        var (bytesSent, framesSent) = await SendMessageAsync(opcode, length, readSlice, answersClientFramesBetween: true);
        context.Log.Note($"Sent {bytesSent} bytes of {entry.Path} as a {(opcode == WebSocketOpcode.Binary ? "binary" : "text")} message in {framesSent} frames");
        closeFrame ??= EmptyCloseFrame;
    }

    // A listing is one text message: each entry's name, "/" after a directory, then LF.
    private (WebSocketOpcode Opcode, long Length, Func<long, int, Task<ReadOnlyMemory<byte>?>> ReadSlice) ListingMessage(WebSocketServedEntry entry)
    {
        var listing = contentStore.ListDirectory(entry.Mapping, context.CancellationToken);
        var text = Encoding.UTF8.GetBytes(string.Concat(listing.Entries.Select(listed =>
            listed.Kind == ContentEntryKind.Directory ? $"{listed.Name}/\n" : $"{listed.Name}\n")));

        return (WebSocketOpcode.Text, text.Length, InMemorySliceReader(text));
    }

    private Func<long, int, Task<ReadOnlyMemory<byte>?>> FileSliceReader(WebSocketServedEntry entry) =>
        (offset, count) => ReadFileSliceAsync(entry, offset, count);

    private static Func<long, int, Task<ReadOnlyMemory<byte>?>> InMemorySliceReader(byte[] payload) =>
        (offset, count) => Task.FromResult<ReadOnlyMemory<byte>?>(payload.AsMemory((int)offset, count));

    // One frame's bytes of the file; null, with the CLOSE 1011 decided, when it cannot be read.
    private async Task<ReadOnlyMemory<byte>?> ReadFileSliceAsync(WebSocketServedEntry entry, long offset, int count)
    {
        var fileLength = entry.Status.Length.GetValueOrDefault();
        using var slice = new MemoryStream(count);
        try
        {
            await contentStore.CopyFileBytesAsync(entry.Mapping, ContentByteRange.Select(fileLength, offset, offset + count - 1), slice, context.CancellationToken);
        }
        catch (IOException failure)
        {
            CloseWith(InternalError, $"{entry.Path} could not be read ({failure.Message})");

            return null;
        }

        return slice.GetBuffer().AsMemory(0, (int)slice.Length);
    }

    // Sends one message in frames of at most MaxFramePayloadBytes; returns the bytes and
    // frames sent, fewer when a CLOSE was decided first.
    private async Task<(long BytesSent, int FramesSent)> SendMessageAsync(
        WebSocketOpcode opcode, long length, Func<long, int, Task<ReadOnlyMemory<byte>?>> readSlice, bool answersClientFramesBetween)
    {
        long bytesSent = 0;
        var framesSent = 0;
        do
        {
            var count = (int)Math.Min(MaxFramePayloadBytes, length - bytesSent);
            if (await ReadNextFramePayloadAsync(bytesSent, count, readSlice, answersClientFramesBetween) is not { } bytes)
            {
                break;
            }

            bytesSent += count;
            var frame = WebSocketFrameEncoder.EncodeFrame(bytesSent >= length, framesSent == 0 ? opcode : WebSocketOpcode.Continuation, bytes.Span);
            await connection.WriteAsync(frame, context.CancellationToken);
            framesSent++;
        }
        while (bytesSent < length);

        return (bytesSent, framesSent);
    }

    // The next frame's payload, after answering the client frames that arrived when asked to;
    // null when a CLOSE was decided instead.
    private async Task<ReadOnlyMemory<byte>?> ReadNextFramePayloadAsync(
        long offset, int count, Func<long, int, Task<ReadOnlyMemory<byte>?>> readSlice, bool answersClientFramesBetween)
    {
        if (answersClientFramesBetween)
        {
            await AnswerArrivedFramesAsync();
        }

        if (closeFrame is not null)
        {
            return null;
        }

        return count == 0 ? ReadOnlyMemory<byte>.Empty : await readSlice(offset, count);
    }

    // Answers every client frame already read, without waiting for one.
    private async Task AnswerArrivedFramesAsync()
    {
        while (closeFrame is null && pendingRead is { IsCompleted: true } read)
        {
            await AnswerAsync(await read, echoes: false);
        }
    }

    private async Task AnswerAsync(WebSocketFrameReadResult result, bool echoes)
    {
        pendingRead = null;
        if (result.Frame is not { } frame)
        {
            AnswerFrameNotRead(result.Outcome);

            return;
        }

        if (FindHeaderViolation(frame) is { } violation)
        {
            CloseWith(ProtocolError, violation);

            return;
        }

        await AnswerReassemblyStepAsync(reassembler.Accept(frame), echoes);
        if (closeFrame is null)
        {
            StartReading();
        }
    }

    // What the frame reader leaves to the server: every client frame is masked (RFC 6455
    // section 5.1), and no RSV bit is set, since no extension was agreed (section 5.2).
    private static string? FindHeaderViolation(WebSocketFrame frame)
    {
        if (!frame.Masked)
        {
            return "a client frame is not masked";
        }

        return frame.Rsv1 || frame.Rsv2 || frame.Rsv3 ? "a client frame sets a reserved bit" : null;
    }

    private void AnswerFrameNotRead(WebSocketFrameReadOutcome outcome)
    {
        switch (outcome)
        {
            case WebSocketFrameReadOutcome.ConnectionClosed:
                context.Log.Note("The client closed the connection without a CLOSE.");
                break;
            case WebSocketFrameReadOutcome.FrameTooLarge:
                CloseWith(MessageTooBig, $"a client frame is past --max-message {context.Limits.MaxMessageBytes}");
                break;
            case WebSocketFrameReadOutcome.CloseReasonNotUtf8:
                CloseWith(InvalidPayload, "a client CLOSE's reason is not valid UTF-8");
                break;
            default:
                CloseWith(ProtocolError, ProtocolErrorsInFrames[outcome]);
                break;
        }
    }

    private Task AnswerReassemblyStepAsync(WebSocketReassemblyStep step, bool echoes)
    {
        switch (step.Outcome)
        {
            case WebSocketReassemblyOutcome.FragmentHeld:
                return Task.CompletedTask;
            case WebSocketReassemblyOutcome.MessageComplete:
                return echoes ? EchoAsync(step.Message!) : Task.CompletedTask;
            case WebSocketReassemblyOutcome.ControlFrame:
                return AnswerControlFrameAsync(step.ControlFrame!);
            case WebSocketReassemblyOutcome.ContinuationWithoutMessage:
                CloseWith(ProtocolError, "a client continuation frame arrived with no message started");
                break;
            case WebSocketReassemblyOutcome.DataFrameInsideMessage:
                CloseWith(ProtocolError, "a client data frame arrived inside an unfinished message");
                break;
            case WebSocketReassemblyOutcome.MessageTooLarge:
                CloseWith(MessageTooBig, $"a client message is past --max-message {context.Limits.MaxMessageBytes}");
                break;
            default:
                CloseWith(InvalidPayload, "a client text message is not valid UTF-8");
                break;
        }

        return Task.CompletedTask;
    }

    private async Task EchoAsync(WebSocketMessage message) =>
        await SendMessageAsync(message.Opcode, message.Payload.Length, InMemorySliceReader(message.Payload), answersClientFramesBetween: false);

    private async Task AnswerControlFrameAsync(WebSocketFrame frame)
    {
        if (frame.Opcode == WebSocketOpcode.Ping)
        {
            await connection.WriteAsync(WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Pong, frame.Payload), context.CancellationToken);
            context.Log.Note($"Answered PING with PONG, {frame.Payload.Length} bytes");
        }
        else if (frame.Opcode == WebSocketOpcode.Close)
        {
            AnswerClientClose(frame.Payload);
        }
    }

    // The frame reader has checked the code and the reason; the answer echoes the code alone.
    private void AnswerClientClose(byte[] payload)
    {
        if (payload.Length == 0)
        {
            context.Log.Note("Client closed with no code");
            closeFrame = EmptyCloseFrame;

            return;
        }

        var code = BinaryPrimitives.ReadUInt16BigEndian(payload);
        context.Log.Note($"Client closed with {code}");
        closeFrame = WebSocketFrameEncoder.EncodeClose(code, "");
    }

    private void CloseWith(ushort code, string why)
    {
        context.Log.Note($"Closing with {code}: {why}");
        closeFrame = WebSocketFrameEncoder.EncodeClose(code, "");
    }

    private void StartReading() => pendingRead = frameReader.ReadFrameAsync(readCancellation.Token).AsTask();

    private async Task StopReadingAsync()
    {
        await readCancellation.CancelAsync();
        if (pendingRead is { } read)
        {
            await ((Task)read).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    // Decision 5: the CLOSE, if one was decided (none when the client closed without one), the
    // sending side shut down, and a lingering read.
    private async Task CloseAsync()
    {
        if (closeFrame is { } frame)
        {
            await connection.WriteAsync(frame, context.CancellationToken);
        }

        await connection.CompleteWritesAsync(context.CancellationToken);
        await StopReadingAsync();
        await lingeringClose.LingerAsync(context.CancellationToken);
    }

    // ADR-0071 decision 7 and ADR-0059: CLOSE 1001 within its own one-second deadline, which
    // only shutdown cuts short, then the lingering close on the same terms.
    private async Task SayGoodbyeAtALimitAsync()
    {
        context.Log.Note("Closing with 1001: the exchange reached its idle timeout or maximum duration");
        if (await TryWriteGoingAwayCloseAsync())
        {
            await lingeringClose.LingerAsync(context.ShutdownToken);
        }
    }

    private async Task<bool> TryWriteGoingAwayCloseAsync()
    {
        using var deadline = new CancellationTokenSource(LimitCloseWriteDeadline, context.TimeProvider);
        using var deadlineOrShutdown = CancellationTokenSource.CreateLinkedTokenSource(context.ShutdownToken, deadline.Token);
        try
        {
            await connection.WriteAsync(GoingAwayCloseFrame, deadlineOrShutdown.Token);
            await connection.CompleteWritesAsync(deadlineOrShutdown.Token);
        }
        catch (OperationCanceledException) when (!context.ShutdownToken.IsCancellationRequested)
        {
            context.Log.Note($"The CLOSE 1001 was not written within its {LimitCloseWriteDeadline.TotalSeconds}-second write deadline; the connection is closed without it.");

            return false;
        }

        return true;
    }
}
