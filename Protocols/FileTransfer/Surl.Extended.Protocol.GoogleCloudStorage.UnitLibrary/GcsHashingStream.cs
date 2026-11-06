using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The write-only stream an object's bytes are copied into: each write goes through the object's
/// hashes and, when an upload session is given, is appended to it until the session says the
/// upload is past <c>--max-filesize</c>, after which the rest is only hashed.
/// </summary>
/// <param name="hashes">The hashes every byte goes through.</param>
/// <param name="session">The upload session the bytes are appended to, or <see langword="null"/>
/// when the bytes are only hashed.</param>
internal sealed class GcsHashingStream(GcsObjectHashes hashes, ContentUploadSession? session) : Stream
{
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
    /// Not supported: bytes are copied asynchronously, never by a blocking write.
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
        hashes.Append(buffer.Span);
        if (session is null || IsTooLarge)
        {
            return;
        }

        IsTooLarge = await session.WriteAtAsync(session.Length, buffer, cancellationToken) == ContentUploadResult.TooLarge;
    }
}
