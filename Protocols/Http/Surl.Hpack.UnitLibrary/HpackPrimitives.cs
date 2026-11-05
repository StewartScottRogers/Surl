using System.Text;

namespace Surl.Hpack;

/// <summary>
/// HPACK's two primitive types: prefixed integers (RFC 7541 section 5.1) and string literals
/// (RFC 7541 section 5.2). The readers raise <see cref="HpackDecodingException" />, which
/// <see cref="HpackDecoder.Decode" /> turns into its result.
/// </summary>
internal static class HpackPrimitives
{
    /// <summary>The shift of the last continuation byte a 31-bit integer can need.</summary>
    private const int LargestContinuationShift = 28;

    /// <summary>
    /// Appends <paramref name="value" /> as an integer with an N-bit prefix, the first byte carrying
    /// <paramref name="firstByteFlags" /> in the bits above the prefix.
    /// </summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="value">The integer, zero or more.</param>
    /// <param name="prefixBits">The prefix size N, 1 to 8.</param>
    /// <param name="firstByteFlags">The bits above the prefix in the first byte.</param>
    public static void WriteInteger(List<byte> output, int value, int prefixBits, byte firstByteFlags)
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
    /// <param name="block">The header block.</param>
    /// <param name="position">Where the integer starts; on return, where it ended.</param>
    /// <param name="prefixBits">The prefix size N, 1 to 8.</param>
    /// <returns>The integer.</returns>
    public static int ReadInteger(ReadOnlySpan<byte> block, ref int position, int prefixBits)
    {
        var prefixLimit = (1 << prefixBits) - 1;
        var value = (long)(ReadIntegerByte(block, ref position) & prefixLimit);
        if (value < prefixLimit)
        {
            return (int)value;
        }

        for (var shift = 0; ; shift += 7)
        {
            HpackDecodingException.ThrowIf(shift > LargestContinuationShift, HpackDecodingError.IntegerOverflow);
            var octet = ReadIntegerByte(block, ref position);
            value += (long)(octet & 0x7F) << shift;
            HpackDecodingException.ThrowIf(value > int.MaxValue, HpackDecodingError.IntegerOverflow);
            if ((octet & 0x80) == 0)
            {
                return (int)value;
            }
        }
    }

    /// <summary>Appends a string literal, Huffman-coded or not as <paramref name="coding" /> says.</summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="text">The string; each character is one byte.</param>
    /// <param name="coding">When to Huffman-code it.</param>
    public static void WriteString(List<byte> output, string text, HpackStringCoding coding)
    {
        var raw = Encoding.Latin1.GetBytes(text);
        if (IsHuffmanChosen(raw, coding))
        {
            var coded = HpackHuffman.Encode(raw);
            WriteInteger(output, coded.Length, 7, 0x80);
            output.AddRange(coded);
            return;
        }

        WriteInteger(output, raw.Length, 7, 0x00);
        output.AddRange(raw);
    }

    /// <summary>
    /// Reads a string literal starting at <paramref name="position" />, and moves
    /// <paramref name="position" /> past it.
    /// </summary>
    /// <param name="block">The header block.</param>
    /// <param name="position">Where the literal starts; on return, where it ended.</param>
    /// <returns>The string; each character is one byte.</returns>
    public static string ReadString(ReadOnlySpan<byte> block, ref int position)
    {
        HpackDecodingException.ThrowIf(position >= block.Length, HpackDecodingError.TruncatedString);
        var isHuffmanCoded = (block[position] & 0x80) != 0;
        var length = ReadInteger(block, ref position, 7);
        HpackDecodingException.ThrowIf(length > block.Length - position, HpackDecodingError.TruncatedString);
        var bytes = block.Slice(position, length);
        position += length;
        if (!isHuffmanCoded)
        {
            return Encoding.Latin1.GetString(bytes);
        }

        var isDecoded = HpackHuffman.TryDecode(bytes, out var decoded, out var error);
        HpackDecodingException.ThrowIf(!isDecoded, error);
        return Encoding.Latin1.GetString(decoded);
    }

    private static bool IsHuffmanChosen(byte[] raw, HpackStringCoding coding) => coding switch
    {
        HpackStringCoding.Huffman => true,
        HpackStringCoding.Raw => false,
        _ => HpackHuffman.GetEncodedLength(raw) < raw.Length,
    };

    private static byte ReadIntegerByte(ReadOnlySpan<byte> block, ref int position)
    {
        HpackDecodingException.ThrowIf(position >= block.Length, HpackDecodingError.TruncatedInteger);
        return block[position++];
    }
}
