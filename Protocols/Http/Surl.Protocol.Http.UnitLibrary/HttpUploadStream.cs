using System.Security.Cryptography;
using Surl.Content;

namespace Surl.Protocol.Http;

/// <summary>
/// The write-only stream a <c>PUT</c> body is copied into (ADR-0133, decision 2): each write is
/// hashed into the body's SHA-256 when a login binds the body, and appended to the content
/// store's upload session when there is one, until the session says the upload is past
/// <c>--max-filesize</c>; after that, or with no session, the rest is hashed and dropped.
/// </summary>
/// <param name="session">The upload session the bytes are appended to, or <see langword="null"/>
/// when the upload could not be opened and the body is only read to be judged.</param>
/// <param name="bodySha256">The SHA-256 every byte of the body goes through, or
/// <see langword="null"/> when no login binds the body.</param>
internal sealed class HttpUploadStream(ContentUploadSession? session, IncrementalHash? bodySha256) : Stream
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
        bodySha256?.AppendData(buffer.Span);
        if (session is null || IsTooLarge)
        {
            return;
        }

        IsTooLarge = await session.WriteAtAsync(session.Length, buffer, cancellationToken) == ContentUploadResult.TooLarge;
    }
}
