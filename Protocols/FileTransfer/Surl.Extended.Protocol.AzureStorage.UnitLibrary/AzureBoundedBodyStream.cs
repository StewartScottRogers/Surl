namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The write-only memory a small body, such as a Put Block List's, is copied into: it keeps at most
/// <paramref name="maxBytes"/>, and past them it keeps nothing more and says so.
/// </summary>
/// <param name="maxBytes">The most bytes kept.</param>
internal sealed class AzureBoundedBodyStream(int maxBytes) : MemoryStream
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
