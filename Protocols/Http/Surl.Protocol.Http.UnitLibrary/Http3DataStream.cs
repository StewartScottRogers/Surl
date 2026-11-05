namespace Surl.Protocol.Http;

/// <summary>
/// A write-only <see cref="Stream"/> over one HTTP/3 request stream: each write goes out as one
/// <c>DATA</c> frame (RFC 9114 section 7.2.1) through the given callback, so a content-store file
/// can be copied straight onto the stream. The stream's <c>FIN</c> is the answer writer's to send.
/// </summary>
/// <param name="writeDataFrameAsync">Sends one <c>DATA</c> frame holding the bytes given.</param>
internal sealed class Http3DataStream(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writeDataFrameAsync) : Stream
{
    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException("An HTTP/3 stream has no length.");

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException("An HTTP/3 stream has no position.");
        set => throw new NotSupportedException("An HTTP/3 stream has no position.");
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        writeDataFrameAsync(buffer, cancellationToken);

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
        throw new NotSupportedException("Write to an HTTP/3 stream asynchronously.");

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The stream is write-only.");

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("An HTTP/3 stream cannot seek.");

    /// <inheritdoc />
    public override void SetLength(long value) =>
        throw new NotSupportedException("An HTTP/3 stream has no length.");
}
