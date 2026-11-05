using System.Security.Cryptography;
using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The write-only stream a Put Blob or append body is copied into: each write is hashed into the
/// body's MD5 and written into the content store's upload session after the bytes before it, from
/// the offset the stream starts at, until the session says the upload is past
/// <c>--max-filesize</c>, after which the rest is hashed and dropped.
/// </summary>
/// <param name="session">The upload session the bytes are written to.</param>
/// <param name="bodyMd5">The hash every byte of the body goes through.</param>
/// <param name="firstOffset">Where in the upload the body's first byte lands.</param>
internal sealed class AzureUploadStream(ContentUploadSession session, IncrementalHash bodyMd5, long firstOffset) : Stream
{
    /// <summary>
    /// A stream that appends the body at the upload's end.
    /// </summary>
    /// <param name="session">The upload session the bytes are appended to.</param>
    /// <param name="bodyMd5">The hash every byte of the body goes through.</param>
    public AzureUploadStream(ContentUploadSession session, IncrementalHash bodyMd5)
        : this(session, bodyMd5, session.Length)
    {
    }

    /// <summary>
    /// How many bytes of the body were written into the session.
    /// </summary>
    public long BytesWritten { get; private set; }

    /// <summary>
    /// Whether the upload went past the store's upload limit; the session has then discarded it.
    /// </summary>
    public bool IsTooLarge { get; private set; }

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
    /// Not supported: the body is copied asynchronously, never by a blocking write.
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
        bodyMd5.AppendData(buffer.Span);
        if (IsTooLarge)
        {
            return;
        }

        IsTooLarge = await session.WriteAtAsync(firstOffset + BytesWritten, buffer, cancellationToken) == ContentUploadResult.TooLarge;
        BytesWritten += IsTooLarge ? 0 : buffer.Length;
    }
}
