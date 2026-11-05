namespace Surl.Quic;

/// <summary>
/// A QUIC version 1 1-RTT packet (RFC 9000 section 17.3.1): the short header, its packet number,
/// and the rest of the datagram as its payload. The short header does not carry its connection
/// ID's length; the reader is told it. In the clear the reserved bits are 0 and the key phase and
/// packet number are readable; header protection (BL-374) hides them.
/// </summary>
/// <param name="DestinationConnectionId">The Destination Connection ID, at most 20 bytes.</param>
/// <param name="SpinBit">The latency spin bit (RFC 9000 section 17.4).</param>
/// <param name="KeyPhase">The key phase bit (RFC 9001 section 6).</param>
/// <param name="PacketNumberLength">How many bytes the packet number takes on the wire, 1 to 4.</param>
/// <param name="PacketNumber">The packet number's low bytes as sent.</param>
/// <param name="Payload">Everything after the packet number: the frames in the clear, or the ciphertext and its AEAD tag.</param>
public sealed record QuicShortHeaderPacket(
    ReadOnlyMemory<byte> DestinationConnectionId,
    bool SpinBit,
    bool KeyPhase,
    int PacketNumberLength,
    uint PacketNumber,
    ReadOnlyMemory<byte> Payload) : QuicPacket
{
    /// <inheritdoc/>
    public override ReadOnlyMemory<byte> DestinationConnectionId { get; } = DestinationConnectionId;

    /// <summary>Returns the packet's bytes: the short header, the packet number and the payload.</summary>
    /// <returns>The packet's encoding.</returns>
    /// <exception cref="ArgumentException">A field holds a value RFC 9000 does not allow.</exception>
    public byte[] ToBytes()
    {
        QuicConnectionIds.RequireValidLength(DestinationConnectionId, nameof(DestinationConnectionId));
        QuicPacketHeaderBits.RequireValidPacketNumber(PacketNumberLength, PacketNumber);
        var writer = new QuicWriter();
        writer.WriteByte((byte)(QuicPacketHeaderBits.FixedBit
            | (SpinBit ? QuicPacketHeaderBits.ShortSpinBit : 0)
            | (KeyPhase ? QuicPacketHeaderBits.ShortKeyPhaseBit : 0)
            | QuicPacketHeaderBits.PacketNumberLengthBits(PacketNumberLength)));
        writer.WriteBytes(DestinationConnectionId.Span);
        writer.WritePacketNumber(PacketNumber, PacketNumberLength);
        writer.WriteBytes(Payload.Span);
        return writer.ToArray();
    }

    internal static QuicShortHeaderPacket ReadFrom(QuicReader reader, byte firstByte, int destinationConnectionIdLength)
    {
        QuicPacketHeaderBits.RequireFixedBit(reader, firstByte);
        QuicPacketHeaderBits.RequireZeroReservedBits(reader, firstByte, QuicPacketHeaderBits.ShortReservedBits);
        var destinationConnectionId = reader.ReadBytes(destinationConnectionIdLength);
        var packetNumberLength = QuicPacketHeaderBits.PacketNumberLength(firstByte);
        var packetNumber = reader.ReadPacketNumber(packetNumberLength);
        return new QuicShortHeaderPacket(
            destinationConnectionId,
            (firstByte & QuicPacketHeaderBits.ShortSpinBit) != 0,
            (firstByte & QuicPacketHeaderBits.ShortKeyPhaseBit) != 0,
            packetNumberLength,
            packetNumber,
            reader.ReadRemaining());
    }
}
