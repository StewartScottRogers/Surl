using System.Text;
using Surl.Hpack;

namespace Surl.Qpack;

/// <summary>
/// QPACK's two primitive types: prefixed integers of up to 62 bits (RFC 9204 section 4.1.1) and N-bit
/// prefix string literals with HPACK's Huffman code (RFC 9204 section 4.1.2). The readers raise
/// <see cref="QpackDecodingException" />.
/// </summary>
internal static class QpackPrimitives
{
    /// <summary>The largest integer QPACK carries: 2^62 - 1, the largest QUIC variable-length integer.</summary>
    public const long MaximumInteger = (1L << 62) - 1;

    /// <summary>The shift of the last continuation byte a 62-bit integer can need.</summary>
    private const int LargestContinuationShift = 56;

    /// <summary>
    /// How many times longer a Huffman-coded string can be than the string it decodes to, rounded up:
    /// the longest code is 30 bits, so a decoded byte never costs more than four coded bytes.
    /// </summary>
    private const int HuffmanExpansionBound = 4;

    /// <summary>
    /// Appends <paramref name="value" /> as an integer with an N-bit prefix, the first byte carrying
    /// <paramref name="firstByteFlags" /> in the bits above the prefix.
    /// </summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="value">The integer, zero or more.</param>
    /// <param name="prefixBits">The prefix size N, 1 to 8.</param>
    /// <param name="firstByteFlags">The bits above the prefix in the first byte.</param>
    public static void WriteInteger(List<byte> output, long value, int prefixBits, byte firstByteFlags)
    {
        var prefixLimit = (1 << prefixBits) - 1;
        if (value < prefixLimit)
        {
            output.Add((byte)(firstByteFlags | value));
            return;
        }

        output.Add((byte)(firstByteFlags | prefixLimit));
        var remainder = value - prefixLimit;
        while (remainder >= 0x80)
        {
            output.Add((byte)((remainder & 0x7F) | 0x80));
            remainder >>= 7;
        }

        output.Add((byte)remainder);
    }

    /// <summary>
    /// Reads an integer with an N-bit prefix starting at <paramref name="position" />, and moves
    /// <paramref name="position" /> past it.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="position">Where the integer starts; on return, where it ended.</param>
    /// <param name="prefixBits">The prefix size N, 1 to 8.</param>
    /// <returns>The integer.</returns>
    public static long ReadInteger(ReadOnlySpan<byte> bytes, ref int position, int prefixBits)
    {
        var prefixLimit = (1 << prefixBits) - 1;
        var value = (long)(ReadIntegerByte(bytes, ref position) & prefixLimit);
        if (value < prefixLimit)
        {
            return value;
        }

        for (var shift = 0; ; shift += 7)
        {
            QpackDecodingException.ThrowIf(shift > LargestContinuationShift, QpackDecodingError.IntegerOverflow);
            var octet = ReadIntegerByte(bytes, ref position);
            value += (long)(octet & 0x7F) << shift;
            QpackDecodingException.ThrowIf(value > MaximumInteger, QpackDecodingError.IntegerOverflow);
            if ((octet & 0x80) == 0)
            {
                return value;
            }
        }
    }

    /// <summary>Gets the byte at <paramref name="position" /> without moving past it.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="position">Where the byte is.</param>
    /// <returns>The byte.</returns>
    public static byte PeekByte(ReadOnlySpan<byte> bytes, int position)
    {
        QpackDecodingException.ThrowIf(position >= bytes.Length, QpackDecodingError.TruncatedInteger);
        return bytes[position];
    }

    /// <summary>
    /// Appends an N-bit prefix string literal: the H bit, the length as an (N-1)-bit prefix integer,
    /// then the bytes, Huffman-coded or not as <paramref name="coding" /> says.
    /// </summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="text">The string; each character is one byte.</param>
    /// <param name="prefixBits">The prefix size N, 2 to 8.</param>
    /// <param name="firstByteFlags">The bits above the prefix in the first byte.</param>
    /// <param name="coding">When to Huffman-code it.</param>
    public static void WriteString(
        List<byte> output, string text, int prefixBits, byte firstByteFlags, HpackStringCoding coding)
    {
        var raw = Encoding.Latin1.GetBytes(text);
        var huffmanFlag = (byte)(1 << (prefixBits - 1));
        if (IsHuffmanChosen(raw, coding))
        {
            var coded = HpackHuffman.Encode(raw);
            WriteInteger(output, coded.Length, prefixBits - 1, (byte)(firstByteFlags | huffmanFlag));
            output.AddRange(coded);
            return;
        }

        WriteInteger(output, raw.Length, prefixBits - 1, firstByteFlags);
        output.AddRange(raw);
    }

    /// <summary>
    /// Reads an N-bit prefix string literal starting at <paramref name="position" />, and moves
    /// <paramref name="position" /> past it. A literal whose declared length could not decode to
    /// <paramref name="maximumDecodedLength" /> bytes or fewer is refused before its bytes are awaited.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="position">Where the literal starts; on return, where it ended.</param>
    /// <param name="prefixBits">The prefix size N, 2 to 8.</param>
    /// <param name="maximumDecodedLength">The longest string acceptable here.</param>
    /// <returns>The string; each character is one byte.</returns>
    public static string ReadString(
        ReadOnlySpan<byte> bytes, ref int position, int prefixBits, long maximumDecodedLength)
    {
        var isHuffmanCoded = (PeekByte(bytes, position) & (1 << (prefixBits - 1))) != 0;
        var length = ReadInteger(bytes, ref position, prefixBits - 1);
        var longestAcceptable = isHuffmanCoded ? maximumDecodedLength * HuffmanExpansionBound : maximumDecodedLength;
        QpackDecodingException.ThrowIf(length > longestAcceptable, QpackDecodingError.EntryLargerThanCapacity);
        QpackDecodingException.ThrowIf(length > bytes.Length - position, QpackDecodingError.TruncatedString);
        var literal = bytes.Slice(position, (int)length);
        position += (int)length;
        return isHuffmanCoded ? DecodeHuffman(literal) : Encoding.Latin1.GetString(literal);
    }

    /// <summary>Turns an HPACK Huffman decoding failure into the QPACK one.</summary>
    /// <param name="error">The HPACK failure; only the Huffman ones occur.</param>
    /// <returns>The QPACK failure.</returns>
    public static QpackDecodingError ToQpackError(HpackDecodingError error) => error switch
    {
        HpackDecodingError.HuffmanEndOfStringSymbol => QpackDecodingError.HuffmanEndOfStringSymbol,
        HpackDecodingError.HuffmanPaddingLongerThanSevenBits => QpackDecodingError.HuffmanPaddingLongerThanSevenBits,
        _ => QpackDecodingError.HuffmanPaddingNotAllOnes,
    };

    private static string DecodeHuffman(ReadOnlySpan<byte> literal)
    {
        var isDecoded = HpackHuffman.TryDecode(literal, out var decoded, out var error);
        QpackDecodingException.ThrowIf(!isDecoded, ToQpackError(error));
        return Encoding.Latin1.GetString(decoded);
    }

    private static bool IsHuffmanChosen(byte[] raw, HpackStringCoding coding) => coding switch
    {
        HpackStringCoding.Huffman => true,
        HpackStringCoding.Raw => false,
        _ => HpackHuffman.GetEncodedLength(raw) < raw.Length,
    };

    private static byte ReadIntegerByte(ReadOnlySpan<byte> bytes, ref int position)
    {
        QpackDecodingException.ThrowIf(position >= bytes.Length, QpackDecodingError.TruncatedInteger);
        return bytes[position++];
    }
}
