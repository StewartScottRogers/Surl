namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// A capsule whose type this codec gives no layout to - an unknown type, which RFC 9297 section
/// 3.2 says to skip, or <c>WT_MAX_STREAM_DATA</c> and <c>WT_STREAM_DATA_BLOCKED</c>, which HTTP/3
/// prohibits (WTH3 5.4) - kept as its type and raw value.
/// </summary>
public sealed record WebTransportUnparsedCapsule : WebTransportCapsule
{
    /// <summary>Creates the capsule.</summary>
    /// <param name="type">A type without a layout here.</param>
    /// <param name="value">The raw value.</param>
    /// <exception cref="ArgumentException"><paramref name="type"/> has its own capsule record.</exception>
    public WebTransportUnparsedCapsule(WebTransportCapsuleType type, ReadOnlyMemory<byte> value)
    {
        if (WebTransportCapsuleReader.HasLayout(type))
        {
            throw new ArgumentException($"Capsule type 0x{(ulong)type:x} has its own capsule record.", nameof(type));
        }

        Type = type;
        Value = value;
    }

    /// <inheritdoc />
    public override WebTransportCapsuleType Type { get; }

    /// <summary>The raw value.</summary>
    public ReadOnlyMemory<byte> Value { get; }

    /// <inheritdoc />
    internal override byte[] EncodeValue() => Value.ToArray();
}
