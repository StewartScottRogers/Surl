namespace Surl.Extended.Protocol.WebTransport;

/// <summary><c>DATAGRAM</c> (RFC 9297 section 3.5): the value is one HTTP datagram's payload.</summary>
public sealed record WebTransportDatagramCapsule : WebTransportCapsule
{
    /// <summary>Creates the capsule.</summary>
    /// <param name="payload">The datagram's payload.</param>
    public WebTransportDatagramCapsule(ReadOnlyMemory<byte> payload)
    {
        Payload = payload;
    }

    /// <inheritdoc />
    public override WebTransportCapsuleType Type => WebTransportCapsuleType.Datagram;

    /// <summary>The datagram's payload.</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <inheritdoc />
    internal override byte[] EncodeValue() => Payload.ToArray();
}
