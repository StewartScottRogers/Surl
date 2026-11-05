namespace Surl.Http2;

/// <summary>
/// A write-only <see cref="Stream"/> that sends what is written to it as <c>DATA</c> frames on
/// one HTTP/2 stream, each at most 16384 bytes (RFC 9113's initial <c>SETTINGS_MAX_FRAME_SIZE</c>)
/// and never more than the client's flow-control windows allow (RFC 9113 section 6.9). The frame
/// that completes the body's announced length carries <c>END_STREAM</c>.
/// <see cref="Http2RequestStream.CreateResponseBodyStream"/> creates one.
/// </summary>
public sealed class Http2DataStream : Stream
{
    private readonly Http2FrameWriter frames;
    private readonly Http2SendWindows windows;
    private readonly int streamId;
    private readonly CancellationToken streamToken;
    private readonly CancellationToken connectionToken;
    private long remaining;

    /// <summary>
    /// Creates the body stream of <paramref name="streamId"/>.
    /// </summary>
    /// <param name="frames">Writes the frames.</param>
    /// <param name="windows">The connection's send windows.</param>
    /// <param name="streamId">The stream.</param>
    /// <param name="bodyLength">The body's length; <see cref="long.MaxValue"/> sends no <c>END_STREAM</c>.</param>
    /// <param name="streamToken">Cuts the wait for window off when the stream or connection ends.</param>
    /// <param name="connectionToken">Cuts a frame's write off; only the connection's end does.</param>
    internal Http2DataStream(
        Http2FrameWriter frames,
        Http2SendWindows windows,
        int streamId,
        long bodyLength,
        CancellationToken streamToken,
        CancellationToken connectionToken)
    {
        this.frames = frames;
        this.windows = windows;
        this.streamId = streamId;
        remaining = bodyLength;
        this.streamToken = streamToken;
        this.connectionToken = connectionToken;
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException("An HTTP/2 stream has no length.");

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException("An HTTP/2 stream has no position.");
        set => throw new NotSupportedException("An HTTP/2 stream has no position.");
    }

    /// <summary>
    /// Whether a write failed: the stream or connection ended, or no window was left to send in.
    /// A failure while it is <see langword="false"/> is the source's, not the stream's.
    /// </summary>
    public bool HasFailedWrite { get; private set; }

    /// <summary>
    /// Sends <paramref name="buffer"/> as <c>DATA</c> frames, waiting for window as needed.
    /// </summary>
    /// <param name="buffer">The bytes, no more than the body has left.</param>
    /// <param name="cancellationToken">Not used: <c>streamToken</c> and <c>connectionToken</c> govern the write.</param>
    /// <returns>A task that completes when every byte is sent.</returns>
    /// <exception cref="IOException">The client can grant no more window: it closed the connection.</exception>
    /// <exception cref="OperationCanceledException">The stream or the connection ended.</exception>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            while (!buffer.IsEmpty)
            {
                var room = await windows.TakeAsync(streamId, Math.Min(buffer.Length, Http2FrameWriter.MaxPayloadLength), streamToken);
                if (room == 0)
                {
                    throw new IOException($"The client closed the connection before granting stream {streamId} the window its DATA needs.");
                }

                remaining -= room;
                await frames.WriteAsync(new Http2DataFrame(streamId, buffer[..room].ToArray(), remaining == 0), connectionToken);
                buffer = buffer[room..];
            }
        }
        catch
        {
            HasFailedWrite = true;
            throw;
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Write to an HTTP/2 stream asynchronously.");

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The stream is write-only.");

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("An HTTP/2 stream cannot seek.");

    /// <inheritdoc />
    public override void SetLength(long value) =>
        throw new NotSupportedException("An HTTP/2 stream has no length.");
}
