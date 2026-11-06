using System.Buffers.Binary;

namespace Surl.Compression;

/// <summary>
/// Encodes one LZ4 frame as ADR-0105 decision 4 specifies: version 01, independent blocks, no block
/// or content checksum, no content size, a block maximum of 64 KiB, the header checksum byte from
/// <see cref="XxHash32"/>, each block from a greedy hash-table matcher in the LZ4 block format, and a
/// block the matcher does not shrink stored uncompressed (the high bit of its size).
/// </summary>
public static class Lz4FrameEncoder
{
    /// <summary>How much input each block holds: the 64 KiB block maximum the descriptor declares.</summary>
    public const int BlockInputLength = 64 * 1024;

    private const byte Flags = 0x60;
    private const byte BlockDescriptor64KiB = 0x40;
    private const uint StoredBlockBit = 0x80000000;
    private const int LastLiteralsLength = 5;
    private const int LastMatchStartDistance = 12;
    private const int LengthNibbleExtended = 15;

    /// <summary>The LZ4 frame holding <paramref name="input"/>.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> input)
    {
        byte[] data = input.ToArray();
        List<byte> frame = [];
        AppendUInt32(frame, Lz4FrameDecoder.MagicNumber);
        frame.AddRange([Flags, BlockDescriptor64KiB, (byte)(XxHash32.Hash([Flags, BlockDescriptor64KiB]) >> 8)]);
        GreedyMatchFinder finder = new(data);
        for (int start = 0; start < data.Length; start += BlockInputLength)
        {
            int end = Math.Min(start + BlockInputLength, data.Length);
            List<byte> block = EncodeBlock(data, finder, start, end);
            bool stored = block.Count >= end - start;
            AppendUInt32(frame, stored ? (uint)(end - start) | StoredBlockBit : (uint)block.Count);
            frame.AddRange(stored ? data.AsSpan(start, end - start) : block.ToArray());
        }

        AppendUInt32(frame, 0);
        return [.. frame];
    }

    /// <summary>
    /// The block for <c>data[start..end]</c>: matches only within the block, none starting in its
    /// last 12 bytes and none covering its last 5, which stay literals, as the block format requires.
    /// </summary>
    private static List<byte> EncodeBlock(byte[] data, GreedyMatchFinder finder, int start, int end)
    {
        List<byte> block = [];
        int literalStart = start;
        int position = start;
        while (position + LastMatchStartDistance <= end)
        {
            (int offset, int length) = finder.FindAndRemember(position, start, end - LastLiteralsLength - position);
            if (length == 0)
            {
                position++;
                continue;
            }

            int matchLengthLessMinimum = length - GreedyMatchFinder.MinimumMatchLength;
            WriteTokenAndLiterals(block, data.AsSpan(literalStart, position - literalStart), matchLengthLessMinimum);
            block.AddRange([(byte)offset, (byte)(offset >> 8)]);
            WriteLengthExtension(block, matchLengthLessMinimum);
            position += length;
            literalStart = position;
        }

        WriteTokenAndLiterals(block, data.AsSpan(literalStart, end - literalStart), 0);
        return block;
    }

    private static void WriteTokenAndLiterals(List<byte> block, ReadOnlySpan<byte> literals, int matchLengthLessMinimum)
    {
        block.Add((byte)((Math.Min(literals.Length, LengthNibbleExtended) << 4) | Math.Min(matchLengthLessMinimum, LengthNibbleExtended)));
        WriteLengthExtension(block, literals.Length);
        block.AddRange(literals);
    }

    /// <summary>The bytes after a nibble of 15: 255 while at least 255 remains, then the rest.</summary>
    private static void WriteLengthExtension(List<byte> block, int length)
    {
        if (length < LengthNibbleExtended)
        {
            return;
        }

        int rest = length - LengthNibbleExtended;
        for (; rest >= byte.MaxValue; rest -= byte.MaxValue)
        {
            block.Add(byte.MaxValue);
        }

        block.Add((byte)rest);
    }

    private static void AppendUInt32(List<byte> output, uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        output.AddRange(bytes);
    }
}
