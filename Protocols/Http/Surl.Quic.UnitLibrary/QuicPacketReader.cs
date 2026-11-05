namespace Surl.Quic;

/// <summary>
/// Reads one QUIC packet, in the clear, from the start of a datagram (RFC 9000 section 17). A
/// datagram may carry several long header packets one after another (section 12.2): read the
/// next from where the last one's <c>packetLength</c> ends. A malformed header throws a
/// <see cref="QuicTransportException"/> with <see cref="QuicTransportErrorCode.ProtocolViolation"/>
/// and no frame type, and the caller drops the packet (sections 5.2 and 17.2): a header that ends
/// early, a version 1 connection ID longer than 20 bytes, a fixed bit of 0, reserved bits that are
/// not 0, a Length field shorter than the packet number, a Retry packet without its 16-byte tag,
/// and a Version Negotiation list that is not whole 4-byte versions.
/// </summary>
public static class QuicPacketReader
{
    /// <summary>Reads the packet at the start of <paramref name="datagram"/>.</summary>
    /// <param name="datagram">The datagram, or what is left of it after earlier coalesced packets.</param>
    /// <param name="shortHeaderDestinationConnectionIdLength">The length of the connection IDs this endpoint issued, 0 to 20; a short header does not carry it.</param>
    /// <param name="packetLength">How many bytes of <paramref name="datagram"/> the packet takes.</param>
    /// <returns>A <see cref="QuicLongHeaderPacket"/>, <see cref="QuicRetryPacket"/>, <see cref="QuicVersionNegotiationPacket"/>, <see cref="QuicUnsupportedVersionPacket"/> or <see cref="QuicShortHeaderPacket"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="shortHeaderDestinationConnectionIdLength"/> is not 0 to 20.</exception>
    /// <exception cref="QuicTransportException">The header is malformed.</exception>
    public static QuicPacket Read(ReadOnlyMemory<byte> datagram, int shortHeaderDestinationConnectionIdLength, out int packetLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shortHeaderDestinationConnectionIdLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(shortHeaderDestinationConnectionIdLength, QuicConnectionIds.MaxLength);
        var reader = new QuicReader(datagram, QuicTransportErrorCode.ProtocolViolation);
        var firstByte = reader.ReadByte();
        QuicPacket packet = (firstByte & QuicPacketHeaderBits.HeaderForm) == 0
            ? QuicShortHeaderPacket.ReadFrom(reader, firstByte, shortHeaderDestinationConnectionIdLength)
            : ReadLongHeader(reader, firstByte);
        packetLength = reader.Position;
        return packet;
    }

    private static QuicPacket ReadLongHeader(QuicReader reader, byte firstByte)
    {
        var version = reader.ReadUInt32();
        var destinationConnectionId = ReadConnectionId(reader, version);
        var sourceConnectionId = ReadConnectionId(reader, version);
        return version switch
        {
            QuicVersions.VersionNegotiation => QuicVersionNegotiationPacket.ReadFrom(reader, firstByte, destinationConnectionId, sourceConnectionId),
            QuicVersions.Version1 => ReadVersion1(reader, firstByte, destinationConnectionId, sourceConnectionId),
            _ => ReadUnsupportedVersion(reader, version, destinationConnectionId, sourceConnectionId),
        };
    }

    private static QuicPacket ReadVersion1(QuicReader reader, byte firstByte, ReadOnlyMemory<byte> destinationConnectionId, ReadOnlyMemory<byte> sourceConnectionId)
    {
        QuicPacketHeaderBits.RequireFixedBit(reader, firstByte);
        var typeBits = (firstByte & QuicPacketHeaderBits.LongPacketTypeMask) >> QuicPacketHeaderBits.LongPacketTypeShift;
        return typeBits == QuicPacketHeaderBits.RetryTypeBits
            ? QuicRetryPacket.ReadFrom(reader, firstByte, destinationConnectionId, sourceConnectionId)
            : QuicLongHeaderPacket.ReadFrom(reader, firstByte, destinationConnectionId, sourceConnectionId);
    }

    private static QuicUnsupportedVersionPacket ReadUnsupportedVersion(QuicReader reader, uint version, ReadOnlyMemory<byte> destinationConnectionId, ReadOnlyMemory<byte> sourceConnectionId)
    {
        reader.ReadRemaining();
        return new QuicUnsupportedVersionPacket(version, destinationConnectionId, sourceConnectionId);
    }

    private static ReadOnlyMemory<byte> ReadConnectionId(QuicReader reader, uint version)
    {
        var length = reader.ReadByte();
        return version == QuicVersions.Version1 && length > QuicConnectionIds.MaxLength
            ? throw reader.Fault(QuicTransportErrorCode.ProtocolViolation, $"A version 1 connection ID is {length} bytes; the most is 20.")
            : reader.ReadBytes(length);
    }
}
