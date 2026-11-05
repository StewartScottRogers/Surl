using System.Buffers.Binary;

namespace Surl.Dtls;

/// <summary>
/// The 12-byte header of a DTLS handshake fragment (RFC 6347 section 4.2.2): the message type,
/// the whole message's length, its <c>message_seq</c>, and where this fragment sits in it.
/// </summary>
/// <param name="MessageType">The message the fragment belongs to.</param>
/// <param name="Length">The whole message body's length, a 24-bit number.</param>
/// <param name="MessageSeq">The message's sequence number within the handshake.</param>
/// <param name="FragmentOffset">Where the fragment starts in the body, a 24-bit number.</param>
/// <param name="FragmentLength">How many body bytes the fragment carries, a 24-bit number.</param>
public readonly record struct DtlsHandshakeHeader(
    DtlsHandshakeType MessageType, int Length, ushort MessageSeq, int FragmentOffset, int FragmentLength)
{
    /// <summary>The header's length on the wire: 12 bytes.</summary>
    public const int Size = 12;

    /// <summary>The largest 24-bit number, 2^24 - 1, which bounds every length in the header.</summary>
    public const int MaximumLength = (1 << 24) - 1;

    /// <summary>
    /// Reads a header from the start of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The bytes the header starts.</param>
    /// <param name="header">The header read, or <see langword="default"/> when there are fewer than 12 bytes.</param>
    /// <returns><see langword="true"/> when 12 bytes were there to read.</returns>
    public static bool TryRead(ReadOnlySpan<byte> source, out DtlsHandshakeHeader header)
    {
        if (source.Length < Size)
        {
            header = default;
            return false;
        }

        header = new DtlsHandshakeHeader(
            (DtlsHandshakeType)source[0],
            ReadUInt24(source[1..]),
            BinaryPrimitives.ReadUInt16BigEndian(source[4..]),
            ReadUInt24(source[6..]),
            ReadUInt24(source[9..]));
        return true;
    }

    /// <summary>
    /// Writes the header to the first 12 bytes of <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">At least 12 bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">A length or offset is not a 24-bit number.</exception>
    public void Write(Span<byte> destination)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, Size);

        destination[0] = (byte)MessageType;
        WriteUInt24(destination[1..], Length);
        BinaryPrimitives.WriteUInt16BigEndian(destination[4..], MessageSeq);
        WriteUInt24(destination[6..], FragmentOffset);
        WriteUInt24(destination[9..], FragmentLength);
    }

    internal static int ReadUInt24(ReadOnlySpan<byte> source) => (source[0] << 16) | (source[1] << 8) | source[2];

    internal static void WriteUInt24(Span<byte> destination, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumLength);

        destination[0] = (byte)(value >> 16);
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)value;
    }
}
