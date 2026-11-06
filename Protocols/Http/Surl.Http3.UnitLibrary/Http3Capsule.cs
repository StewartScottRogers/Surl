namespace Surl.Http3;

/// <summary>One capsule of the Capsule Protocol (RFC 9297 section 3.2), read from or written to a session's request stream.</summary>
/// <param name="Type">The Capsule Type, e.g. <see cref="DatagramType"/>.</param>
/// <param name="Value">The Capsule Value; its length is the Capsule Length.</param>
public sealed record Http3Capsule(ulong Type, byte[] Value)
{
    /// <summary>The <c>DATAGRAM</c> capsule type (RFC 9297 section 3.5): an HTTP datagram carried on the stream.</summary>
    public const ulong DatagramType = 0x00;
}
