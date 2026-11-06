using System.Buffers.Binary;

namespace Surl.Http2;

/// <summary>
/// The 9-byte header every HTTP/2 frame starts with (RFC 9113 section 4.1): a 24-bit payload
/// length, the type, the flags, and a reserved bit with a 31-bit stream identifier.
/// </summary>
/// <param name="Length">The payload length, 0 to 2^24-1.</param>
/// <param name="Type">The type code, possibly one RFC 9113 does not define.</param>
/// <param name="Flags">The flags byte.</param>
/// <param name="StreamId">The stream identifier, 0 to 2^31-1.</param>
internal readonly record struct Http2FrameHeader(int Length, byte Type, byte Flags, int StreamId)
{
    /// <summary>The bytes a frame header takes on the wire.</summary>
    public const int EncodedLength = 9;

    /// <summary>The largest payload length 24 bits can say.</summary>
    public const int MaxLength = 0xFFFFFF;

    /// <summary>
    /// Reads the 9 header bytes at the start of <paramref name="bytes"/>, ignoring the reserved
    /// bit as section 4.1 requires.
    /// </summary>
    /// <param name="bytes">At least <see cref="EncodedLength"/> bytes.</param>
    /// <returns>The header.</returns>
    public static Http2FrameHeader Read(ReadOnlySpan<byte> bytes) => new(
        (bytes[0] << 16) | (bytes[1] << 8) | bytes[2],
        bytes[3],
        bytes[4],
        (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes[5..]) & 0x7FFFFFFF));

    /// <summary>
    /// Writes the 9 header bytes at the start of <paramref name="bytes"/>, with the reserved bit
    /// unset.
    /// </summary>
    /// <param name="bytes">At least <see cref="EncodedLength"/> bytes.</param>
    public void Write(Span<byte> bytes)
    {
        bytes[0] = (byte)(Length >> 16);
        bytes[1] = (byte)(Length >> 8);
        bytes[2] = (byte)Length;
        bytes[3] = Type;
        bytes[4] = Flags;
        BinaryPrimitives.WriteUInt32BigEndian(bytes[5..], (uint)StreamId);
    }
}
