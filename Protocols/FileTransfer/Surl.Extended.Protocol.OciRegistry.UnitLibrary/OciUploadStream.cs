namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The write-only stream an upload's bytes are copied into: each write is appended to the upload
/// session, until the session says the upload is past <c>--max-filesize</c>, after which the rest
/// is dropped.
/// </summary>
/// <param name="session">The upload session the bytes are appended to.</param>
internal sealed class OciUploadStream(OciUploadSession session) : Stream
{
    /// <summary>
    /// Whether the upload went past the store's upload limit; the session has then discarded it.
    /// </summary>
    public bool IsTooLarge { get; private set; }

    /// <summary>
    /// How many bytes were written to the stream, dropped ones included.
    /// </summary>
    public long BytesWritten { get; private set; }

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>
    /// Not supported: the bytes are copied asynchronously, never by a blocking write.
    /// </summary>
    /// <param name="buffer">Unused.</param>
    /// <param name="offset">Unused.</param>
    /// <param name="count">Unused.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        BytesWritten += buffer.Length;
        if (!IsTooLarge)
        {
            IsTooLarge = !await session.AppendAsync(buffer, cancellationToken);
        }
    }
}
