namespace Surl.Quic;

/// <summary>
/// A QUIC version 1 Initial, 0-RTT or Handshake packet (RFC 9000 sections 17.2.2 to 17.2.4):
/// the long header with its packet number, and the payload its Length field covers. In the clear
/// the reserved bits are 0 and the packet number is readable; header protection (BL-374) hides both.
/// </summary>
/// <param name="Type">Initial, 0-RTT or Handshake.</param>
/// <param name="DestinationConnectionId">The Destination Connection ID, at most 20 bytes.</param>
/// <param name="SourceConnectionId">The Source Connection ID, at most 20 bytes.</param>
/// <param name="Token">The Initial packet's token; empty on every other type.</param>
/// <param name="PacketNumberLength">How many bytes the packet number takes on the wire, 1 to 4.</param>
/// <param name="PacketNumber">The packet number's low bytes as sent.</param>
/// <param name="Payload">What follows the packet number up to the end the Length field gives: the frames in the clear, or the ciphertext and its AEAD tag.</param>
public sealed record QuicLongHeaderPacket(
    QuicPacketType Type,
    ReadOnlyMemory<byte> DestinationConnectionId,
    ReadOnlyMemory<byte> SourceConnectionId,
    ReadOnlyMemory<byte> Token,
    int PacketNumberLength,
    uint PacketNumber,
    ReadOnlyMemory<byte> Payload) : QuicPacket
{
    /// <inheritdoc/>
    public override ReadOnlyMemory<byte> DestinationConnectionId { get; } = DestinationConnectionId;

    /// <summary>Returns the packet's bytes: the long header, the packet number and the payload.</summary>
    /// <returns>The packet's encoding.</returns>
    /// <exception cref="ArgumentException">A field holds a value RFC 9000 does not allow.</exception>
    public byte[] ToBytes()
    {
        RequireValidFields();
        var writer = new QuicWriter();
        writer.WriteByte((byte)(QuicPacketHeaderBits.HeaderForm | QuicPacketHeaderBits.FixedBit
            | ((int)Type << QuicPacketHeaderBits.LongPacketTypeShift)
            | QuicPacketHeaderBits.PacketNumberLengthBits(PacketNumberLength)));
        writer.WriteUInt32(QuicVersions.Version1);
        writer.WriteByte((byte)DestinationConnectionId.Length);
        writer.WriteBytes(DestinationConnectionId.Span);
        writer.WriteByte((byte)SourceConnectionId.Length);
        writer.WriteBytes(SourceConnectionId.Span);
        if (Type == QuicPacketType.Initial)
        {
            writer.WriteLengthPrefixedBytes(Token.Span);
        }

        writer.WriteVariableLengthInteger((ulong)(PacketNumberLength + Payload.Length));
        writer.WritePacketNumber(PacketNumber, PacketNumberLength);
        writer.WriteBytes(Payload.Span);
        return writer.ToArray();
    }

    internal static QuicLongHeaderPacket ReadFrom(QuicReader reader, byte firstByte, ReadOnlyMemory<byte> destinationConnectionId, ReadOnlyMemory<byte> sourceConnectionId)
    {
        QuicPacketHeaderBits.RequireZeroReservedBits(reader, firstByte, QuicPacketHeaderBits.LongReservedBits);
        var type = (QuicPacketType)((firstByte & QuicPacketHeaderBits.LongPacketTypeMask) >> QuicPacketHeaderBits.LongPacketTypeShift);
        var token = type == QuicPacketType.Initial ? reader.ReadLengthPrefixedBytes() : ReadOnlyMemory<byte>.Empty;
        var length = reader.ReadVariableLengthInteger();
        var packetNumberLength = QuicPacketHeaderBits.PacketNumberLength(firstByte);
        if (length < (ulong)packetNumberLength)
        {
            throw reader.Fault(QuicTransportErrorCode.ProtocolViolation, $"The Length field, {length}, is shorter than the {packetNumberLength}-byte packet number.");
        }

        var packetNumber = reader.ReadPacketNumber(packetNumberLength);
        var payload = reader.ReadBytes(length - (ulong)packetNumberLength);
        return new QuicLongHeaderPacket(type, destinationConnectionId, sourceConnectionId, token, packetNumberLength, packetNumber, payload);
    }

    private void RequireValidFields()
    {
        if (Type is not (QuicPacketType.Initial or QuicPacketType.ZeroRtt or QuicPacketType.Handshake))
        {
            throw new ArgumentOutOfRangeException(nameof(Type), Type, "A long header packet with a packet number is Initial, 0-RTT or Handshake.");
        }

        if (Type != QuicPacketType.Initial && !Token.IsEmpty)
        {
            throw new ArgumentException("Only an Initial packet carries a token.", nameof(Token));
        }

        QuicConnectionIds.RequireValidLength(DestinationConnectionId, nameof(DestinationConnectionId));
        QuicConnectionIds.RequireValidLength(SourceConnectionId, nameof(SourceConnectionId));
        QuicPacketHeaderBits.RequireValidPacketNumber(PacketNumberLength, PacketNumber);
    }
}
