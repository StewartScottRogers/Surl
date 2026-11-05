using System.Numerics;

namespace Surl.Compression;

/// <summary>
/// Encodes raw Snappy (google/snappy's <c>format_description.txt</c>) with a greedy hash-table
/// matcher: the varint length, then literals and copies, each copy in the shortest form its offset
/// and length allow - a 1-byte offset for lengths 4 to 11 and offsets below 2048, a 2-byte offset
/// below 65536, a 4-byte offset beyond - and at most 64 bytes long (ADR-0105, decision 4).
/// </summary>
public static class SnappyEncoder
{
    private const int CopyLongest = 64;
    private const int OneByteOffsetLongest = 11;
    private const int OneByteOffsetLimit = 2048;
    private const int TwoByteOffsetLimit = 65536;
    private const int LiteralLengthInTagLimit = 60;

    /// <summary>The raw Snappy encoding of <paramref name="input"/>.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> input)
    {
        byte[] data = input.ToArray();
        List<byte> output = [];
        WriteVarint(output, (uint)data.Length);
        GreedyMatchFinder finder = new(data);
        int literalStart = 0;
        int position = 0;
        while (position + GreedyMatchFinder.MinimumMatchLength <= data.Length)
        {
            (int offset, int length) = finder.FindAndRemember(
                position, 0, Math.Min(CopyLongest, data.Length - position));
            if (length == 0)
            {
                position++;
                continue;
            }

            WriteLiteral(output, data.AsSpan(literalStart, position - literalStart));
            WriteCopy(output, offset, length);
            position += length;
            literalStart = position;
        }

        WriteLiteral(output, data.AsSpan(literalStart));
        return [.. output];
    }

    private static void WriteVarint(List<byte> output, uint value)
    {
        for (; value >= 0x80; value >>= 7)
        {
            output.Add((byte)(value | 0x80));
        }

        output.Add((byte)value);
    }

    private static void WriteLiteral(List<byte> output, ReadOnlySpan<byte> literal)
    {
        if (literal.IsEmpty)
        {
            return;
        }

        uint lengthLessOne = (uint)literal.Length - 1;
        if (lengthLessOne < LiteralLengthInTagLimit)
        {
            output.Add((byte)(lengthLessOne << 2));
        }
        else
        {
            int lengthBytes = LittleEndianByteCount(lengthLessOne);
            output.Add((byte)((LiteralLengthInTagLimit - 1 + lengthBytes) << 2));
            for (int index = 0; index < lengthBytes; index++)
            {
                output.Add((byte)(lengthLessOne >> (8 * index)));
            }
        }

        output.AddRange(literal);
    }

    /// <summary>How many little-endian bytes <paramref name="value"/> needs, 1 to 4.</summary>
    private static int LittleEndianByteCount(uint value) =>
        (32 - BitOperations.LeadingZeroCount(value) + 7) / 8;

    private static void WriteCopy(List<byte> output, int offset, int length)
    {
        if (length <= OneByteOffsetLongest && offset < OneByteOffsetLimit)
        {
            output.AddRange([(byte)(1 | ((length - 4) << 2) | ((offset >> 8) << 5)), (byte)offset]);
        }
        else if (offset < TwoByteOffsetLimit)
        {
            output.AddRange([(byte)(2 | ((length - 1) << 2)), (byte)offset, (byte)(offset >> 8)]);
        }
        else
        {
            output.AddRange([
                (byte)(3 | ((length - 1) << 2)), (byte)offset, (byte)(offset >> 8), (byte)(offset >> 16), (byte)(offset >> 24)]);
        }
    }
}
