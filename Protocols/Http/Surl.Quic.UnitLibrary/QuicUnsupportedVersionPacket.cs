namespace Surl.Quic;

/// <summary>
/// A long header packet of a version other than 0 and 1, read only as far as RFC 8999's
/// version-independent fields go: its version and its connection IDs, up to 255 bytes each. The
/// server answers one of at least 1200 bytes with a <see cref="QuicVersionNegotiationPacket"/>
/// (ADR-0082 decision 4). The rest of the datagram is not interpreted.
/// </summary>
/// <param name="Version">The version the packet names.</param>
/// <param name="DestinationConnectionId">The Destination Connection ID, at most 255 bytes.</param>
/// <param name="SourceConnectionId">The Source Connection ID, at most 255 bytes.</param>
public sealed record QuicUnsupportedVersionPacket(
    uint Version,
    ReadOnlyMemory<byte> DestinationConnectionId,
    ReadOnlyMemory<byte> SourceConnectionId) : QuicPacket
{
    /// <inheritdoc/>
    public override ReadOnlyMemory<byte> DestinationConnectionId { get; } = DestinationConnectionId;
}
