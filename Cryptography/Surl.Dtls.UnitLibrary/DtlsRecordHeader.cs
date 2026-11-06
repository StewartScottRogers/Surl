using System.Buffers.Binary;

namespace Surl.Dtls;

/// <summary>
/// The 13-byte header of a DTLS record (RFC 6347 section 4.1): content type, version, 16-bit
/// epoch, 48-bit sequence number and the length of the fragment that follows.
/// </summary>
/// <param name="ContentType">What the fragment carries.</param>
/// <param name="Version">The protocol version on the wire, <see cref="Dtls12Version"/> for DTLS 1.2.</param>
/// <param name="Epoch">The cipher state the record belongs to.</param>
/// <param name="SequenceNumber">The record's 48-bit sequence number within its epoch.</param>
/// <param name="Length">The length of the fragment after the header.</param>
public readonly record struct DtlsRecordHeader(
    DtlsContentType ContentType, ushort Version, ushort Epoch, long SequenceNumber, ushort Length)
{
    /// <summary>The header's length on the wire: 13 bytes.</summary>
    public const int Size = 13;

    /// <summary>DTLS 1.2's version on the wire, {254, 253}.</summary>
    public const ushort Dtls12Version = 0xFEFD;

    /// <summary>The largest 48-bit sequence number, 2^48 - 1.</summary>
    public const long MaximumSequenceNumber = (1L << 48) - 1;

    /// <summary>
    /// Reads a header from the start of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The bytes the header starts.</param>
    /// <param name="header">The header read, or <see langword="default"/> when there are fewer than 13 bytes.</param>
    /// <returns><see langword="true"/> when 13 bytes were there to read.</returns>
    public static bool TryRead(ReadOnlySpan<byte> source, out DtlsRecordHeader header)
    {
        if (source.Length < Size)
        {
            header = default;
            return false;
        }

        var epochAndSequenceNumber = BinaryPrimitives.ReadUInt64BigEndian(source[3..]);
        header = new DtlsRecordHeader(
            (DtlsContentType)source[0],
            BinaryPrimitives.ReadUInt16BigEndian(source[1..]),
            (ushort)(epochAndSequenceNumber >> 48),
            (long)(epochAndSequenceNumber & MaximumSequenceNumber),
            BinaryPrimitives.ReadUInt16BigEndian(source[11..]));
        return true;
    }

    /// <summary>
    /// Writes the header to the first 13 bytes of <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">At least 13 bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">The sequence number is not a 48-bit number.</exception>
    public void Write(Span<byte> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(SequenceNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(SequenceNumber, MaximumSequenceNumber);
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, Size);

        destination[0] = (byte)ContentType;
        BinaryPrimitives.WriteUInt16BigEndian(destination[1..], Version);
        BinaryPrimitives.WriteUInt64BigEndian(destination[3..], EpochAndSequenceNumber(Epoch, SequenceNumber));
        BinaryPrimitives.WriteUInt16BigEndian(destination[11..], Length);
    }

    /// <summary>
    /// The 8 bytes RFC 6347 section 4.1.2.1 puts where TLS has its <c>seq_num</c>: the epoch
    /// followed by the 48-bit sequence number, as one big-endian number.
    /// </summary>
    internal static ulong EpochAndSequenceNumber(ushort epoch, long sequenceNumber) =>
        ((ulong)epoch << 48) | (ulong)sequenceNumber;
}
