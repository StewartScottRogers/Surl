namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// One capsule on a WebTransport session's CONNECT stream (RFC 9297 section 3.2): a type, a
/// length and a value. Each derived record gives one layout; <see cref="WebTransportCapsuleWriter"/>
/// writes it and <see cref="WebTransportCapsuleReader"/> reads it.
/// </summary>
public abstract record WebTransportCapsule
{
    private protected WebTransportCapsule()
    {
    }

    /// <summary>The capsule's type.</summary>
    public abstract WebTransportCapsuleType Type { get; }

    /// <summary>Returns the capsule's value: the bytes after its type and length.</summary>
    /// <returns>The value bytes.</returns>
    internal abstract byte[] EncodeValue();
}
