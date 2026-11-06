namespace Surl.Quic;

/// <summary>
/// A Version Negotiation packet (RFC 9000 section 17.2.1): a long header with version 0 listing
/// the versions the server supports. It belongs to no QUIC version, so its connection IDs may be
/// up to 255 bytes: the server echoes whatever the client's packet carried, the client's Source
/// Connection ID as its Destination and the client's Destination as its Source.
/// </summary>
/// <param name="DestinationConnectionId">The Destination Connection ID, at most 255 bytes.</param>
/// <param name="SourceConnectionId">The Source Connection ID, at most 255 bytes.</param>
/// <param name="SupportedVersions">The versions the server supports.</param>
/// <param name="UnusedBits">The first byte's seven low bits, which carry no meaning, 0 to 127.</param>
public sealed record QuicVersionNegotiationPacket(
    ReadOnlyMemory<byte> DestinationConnectionId,
    ReadOnlyMemory<byte> SourceConnectionId,
    IReadOnlyList<uint> SupportedVersions,
    byte UnusedBits) : QuicPacket
{
    /// <inheritdoc/>
    public override ReadOnlyMemory<byte> DestinationConnectionId { get; } = DestinationConnectionId;

    /// <summary>Returns the packet's bytes.</summary>
    /// <returns>The packet's encoding.</returns>
    /// <exception cref="ArgumentException">A field holds a value RFC 9000 does not allow.</exception>
    public byte[] ToBytes()
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(DestinationConnectionId.Length, byte.MaxValue, nameof(DestinationConnectionId));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(SourceConnectionId.Length, byte.MaxValue, nameof(SourceConnectionId));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(UnusedBits, QuicPacketHeaderBits.VersionNegotiationUnusedMask);
        var writer = new QuicWriter();
        writer.WriteByte((byte)(QuicPacketHeaderBits.HeaderForm | UnusedBits));
        writer.WriteUInt32(QuicVersions.VersionNegotiation);
        writer.WriteByte((byte)DestinationConnectionId.Length);
        writer.WriteBytes(DestinationConnectionId.Span);
        writer.WriteByte((byte)SourceConnectionId.Length);
        writer.WriteBytes(SourceConnectionId.Span);
        foreach (var version in SupportedVersions)
        {
            writer.WriteUInt32(version);
        }

        return writer.ToArray();
    }

    internal static QuicVersionNegotiationPacket ReadFrom(QuicReader reader, byte firstByte, ReadOnlyMemory<byte> destinationConnectionId, ReadOnlyMemory<byte> sourceConnectionId)
    {
        if (reader.Remaining % 4 != 0)
        {
            throw reader.Fault(QuicTransportErrorCode.ProtocolViolation, "A Version Negotiation packet's version list is not a whole number of 4-byte versions.");
        }

        var versions = new List<uint>();
        while (!reader.IsAtEnd)
        {
            versions.Add(reader.ReadUInt32());
        }

        return new QuicVersionNegotiationPacket(destinationConnectionId, sourceConnectionId, versions, (byte)(firstByte & QuicPacketHeaderBits.VersionNegotiationUnusedMask));
    }
}
