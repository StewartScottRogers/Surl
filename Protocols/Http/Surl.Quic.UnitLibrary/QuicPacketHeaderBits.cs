namespace Surl.Quic;

/// <summary>The bits of a QUIC packet's first byte (RFC 9000 section 17), and the checks on the fields they size.</summary>
internal static class QuicPacketHeaderBits
{
    public const byte HeaderForm = 0x80;
    public const byte FixedBit = 0x40;
    public const byte LongPacketTypeMask = 0x30;
    public const int LongPacketTypeShift = 4;
    public const byte LongReservedBits = 0x0c;
    public const byte ShortSpinBit = 0x20;
    public const byte ShortReservedBits = 0x18;
    public const byte ShortKeyPhaseBit = 0x04;
    public const byte PacketNumberLengthMask = 0x03;
    public const byte RetryUnusedMask = 0x0f;
    public const byte VersionNegotiationUnusedMask = 0x7f;
    public const byte RetryTypeBits = 0x03;

    /// <summary>Returns the packet number length the first byte's two low bits give, 1 to 4.</summary>
    public static int PacketNumberLength(byte firstByte) => (firstByte & PacketNumberLengthMask) + 1;

    /// <summary>Returns the first byte's two low bits for a packet number length of 1 to 4.</summary>
    public static byte PacketNumberLengthBits(int packetNumberLength) => (byte)(packetNumberLength - 1);

    /// <summary>Throws when a packet number to be written is not 1 to 4 bytes or does not fit its length.</summary>
    public static void RequireValidPacketNumber(int packetNumberLength, uint packetNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(packetNumberLength, 1, "PacketNumberLength");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(packetNumberLength, 4, "PacketNumberLength");
        if (packetNumberLength < 4 && packetNumber >> (8 * packetNumberLength) != 0)
        {
            throw new ArgumentOutOfRangeException("PacketNumber", packetNumber, $"The packet number does not fit in {packetNumberLength} bytes.");
        }
    }

    /// <summary>Throws the <c>PROTOCOL_VIOLATION</c> for a version 1 packet whose fixed bit is 0.</summary>
    public static void RequireFixedBit(QuicReader reader, byte firstByte)
    {
        if ((firstByte & FixedBit) == 0)
        {
            throw reader.Fault(QuicTransportErrorCode.ProtocolViolation, "The packet's fixed bit is 0.");
        }
    }

    /// <summary>Throws the <c>PROTOCOL_VIOLATION</c> RFC 9000 section 17.2 names for reserved bits that are not 0.</summary>
    public static void RequireZeroReservedBits(QuicReader reader, byte firstByte, byte reservedBits)
    {
        if ((firstByte & reservedBits) != 0)
        {
            throw reader.Fault(QuicTransportErrorCode.ProtocolViolation, "The packet's reserved bits are not 0.");
        }
    }
}
