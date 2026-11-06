namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The write-only memory a small body, such as a multipart completion's, is copied into: it
/// keeps at most <paramref name="maxBytes"/>, and past them keeps nothing more and says so.
/// </summary>
/// <param name="maxBytes">The most bytes kept.</param>
internal sealed class GcsBoundedBodyStream(int maxBytes) : MemoryStream
{
    /// <summary>
    /// Whether more than the bytes allowed were written; the rest were dropped.
    /// </summary>
    public bool IsTooLarge { get; private set; }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        IsTooLarge |= Length + buffer.Length > maxBytes;

        return IsTooLarge ? ValueTask.CompletedTask : base.WriteAsync(buffer, cancellationToken);
    }
}
