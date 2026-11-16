namespace Surl.Protocol.Abstractions;

/// <summary>
/// The default <see cref="ExchangeContext.DecryptedBytes"/>: discards every byte it is given.
/// </summary>
public sealed class DiscardingDecryptedBytesSink : IDecryptedBytesSink
{
    private DiscardingDecryptedBytesSink()
    {
    }

    /// <summary>
    /// The one instance.
    /// </summary>
    public static DiscardingDecryptedBytesSink Instance { get; } = new();

    /// <inheritdoc/>
    /// <remarks>Discards the bytes.</remarks>
    public void Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc/>
    /// <remarks>Discards the bytes.</remarks>
    public void Sent(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes)
    {
    }
}
