namespace Surl.Compression;

/// <summary>
/// Decodes raw Snappy, as google/snappy's <c>format_description.txt</c> defines it: a varint of
/// the uncompressed length, then elements, each a literal (tag low bits 00, its length in the tag
/// or in the 1 to 4 bytes after it) or a copy with a 1-byte (01), 2-byte (10) or 4-byte (11)
/// little-endian offset. A copy longer than its offset repeats the bytes it has just written.
/// </summary>
public static class SnappyDecoder
{
    private const int MaximumVarintBytes = 5;

    private const int LiteralLengthInTagLimit = 60;

    /// <summary>
    /// Decodes <paramref name="input"/>, writing at most <paramref name="outputBound"/> bytes: a
    /// declared length past the bound is <see cref="DecompressionOutcome.OutputBoundPassed"/> before
    /// anything is decoded, and input that breaks the format, or decodes to a length other than the
    /// one it declares, is <see cref="DecompressionOutcome.Malformed"/>.
    /// </summary>
    public static DecompressionResult Decode(ReadOnlySpan<byte> input, int outputBound)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(outputBound);
        try
        {
            return DecompressionResult.Decompressed(DecodeOrRefuse(input, outputBound));
        }
        catch (DecompressionRefusedException refusal)
        {
            return refusal.ToResult();
        }
    }

    /// <summary>Decodes raw Snappy, or throws the refusal that stops it.</summary>
    internal static byte[] DecodeOrRefuse(ReadOnlySpan<byte> input, int outputBound)
    {
        int position = 0;
        long declaredLength = ReadVarint(input, ref position);
        if (declaredLength > outputBound)
        {
            throw new DecompressionRefusedException(
                DecompressionOutcome.OutputBoundPassed, "the declared length passes the output bound");
        }

        BoundedDecompressionOutput output = new(
            (int)declaredLength, DecompressionOutcome.Malformed, "the output is longer than its declared length");
        while (position < input.Length)
        {
            DecodeElement(input, ref position, output);
        }

        return output.Count == declaredLength
            ? output.ToArray()
            : throw DecompressionRefusedException.Malformed("the output is shorter than its declared length");
    }

    private static long ReadVarint(ReadOnlySpan<byte> input, ref int position)
    {
        long value = 0;
        for (int index = 0; index < MaximumVarintBytes; index++)
        {
            byte next = ReadByte(input, ref position);
            value |= (long)(next & 0x7F) << (7 * index);
            if (next < 0x80)
            {
                return value;
            }
        }

        throw DecompressionRefusedException.Malformed("the length varint is longer than five bytes");
    }

    private static void DecodeElement(ReadOnlySpan<byte> input, ref int position, BoundedDecompressionOutput output)
    {
        byte tag = input[position++];
        switch (tag & 3)
        {
            case 0:
                AppendLiteral(input, ref position, tag, output);
                break;
            case 1:
                output.CopyFromEarlier(((long)(tag >> 5) << 8) | ReadLittleEndian(input, ref position, 1), 4 + ((tag >> 2) & 7), 0);
                break;
            case 2:
                output.CopyFromEarlier(ReadLittleEndian(input, ref position, 2), 1 + (tag >> 2), 0);
                break;
            default:
                output.CopyFromEarlier(ReadLittleEndian(input, ref position, 4), 1 + (tag >> 2), 0);
                break;
        }
    }

    private static void AppendLiteral(ReadOnlySpan<byte> input, ref int position, byte tag, BoundedDecompressionOutput output)
    {
        int code = tag >> 2;
        long length = code < LiteralLengthInTagLimit
            ? code + 1L
            : ReadLittleEndian(input, ref position, code - LiteralLengthInTagLimit + 1) + 1;
        if (length > input.Length - position)
        {
            throw DecompressionRefusedException.Malformed("a literal is truncated");
        }

        output.Append(input.Slice(position, (int)length));
        position += (int)length;
    }

    private static long ReadLittleEndian(ReadOnlySpan<byte> input, ref int position, int count)
    {
        long value = 0;
        for (int index = 0; index < count; index++)
        {
            value |= (long)ReadByte(input, ref position) << (8 * index);
        }

        return value;
    }

    private static byte ReadByte(ReadOnlySpan<byte> input, ref int position) =>
        position < input.Length
            ? input[position++]
            : throw DecompressionRefusedException.Malformed("the input is truncated");
}
