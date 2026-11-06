using System.Numerics;

namespace Surl.Compression;

/// <summary>
/// Writes zstd literals sections (RFC 8878, section 3.1.1.3.1) and Huffman tree descriptions
/// (section 4.2.1). Prefix codes are assigned as section 4.2.1 says: the last symbol's weight
/// completes a power of two, then codes go by weight, lowest first, and within a weight by symbol
/// value, each symbol of weight W taking 2^(W-1) of the table's cells.
/// </summary>
internal static class ZstdHuffmanLiteralsWriter
{
    /// <summary>Raw literals, with the shortest header that holds their size.</summary>
    public static byte[] RawLiterals(byte[] literals) => [.. UncompressedHeader(0, literals.Length), .. literals];

    /// <summary>RLE literals: <paramref name="value"/> <paramref name="count"/> times.</summary>
    public static byte[] RleLiterals(byte value, int count) => [.. UncompressedHeader(1, count), value];

    /// <summary>A Raw or RLE literals header: size format 0 (5 bits), 1 (12 bits) or 3 (20 bits).</summary>
    public static byte[] UncompressedHeader(int type, int size)
    {
        if (size < 32)
        {
            return [(byte)(type | (size << 3))];
        }

        int value = type | (size < 4096 ? 1 << 2 : 3 << 2) | (size << 4);
        return size < 4096 ? [(byte)value, (byte)(value >> 8)] : [(byte)value, (byte)(value >> 8), (byte)(value >> 16)];
    }

    /// <summary>
    /// A Compressed (type 2) or Treeless (type 3) header with 10-bit sizes: size format 0 for one
    /// stream, 1 for four.
    /// </summary>
    public static byte[] CompressedHeader(int type, bool fourStreams, int regeneratedSize, int compressedSize)
    {
        int value = type | ((fourStreams ? 1 : 0) << 2) | (regeneratedSize << 4) | (compressedSize << 14);
        return [(byte)value, (byte)(value >> 8), (byte)(value >> 16)];
    }

    /// <summary>A direct tree description: 127 plus the weight count, then the weights two to a byte, the first in the high half.</summary>
    public static byte[] DirectTree(byte[] explicitWeights)
    {
        byte[] tree = new byte[1 + ((explicitWeights.Length + 1) / 2)];
        tree[0] = (byte)(127 + explicitWeights.Length);
        for (int index = 0; index < explicitWeights.Length; index++)
        {
            tree[1 + (index / 2)] |= (byte)(index % 2 == 0 ? explicitWeights[index] << 4 : explicitWeights[index]);
        }

        return tree;
    }

    /// <summary>
    /// An FSE-compressed tree description: its size, the description of <paramref name="weightCounts"/>
    /// and the weights' bit stream, two states taking turns until one's update reads past the start.
    /// </summary>
    public static byte[] CompressedTree(byte[] explicitWeights, short[] weightCounts, int accuracyLog)
    {
        byte[] description = ZstdFseDescriptionWriter.Write(weightCounts, accuracyLog);
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.FromNormalizedCounts(weightCounts, accuracyLog);
        int lastUpdated = (explicitWeights.Length - 2) % 2;
        int[][] chains = [.. Enumerable.Range(0, 2).Select(turn => ZstdStateChains.Chain(
            table,
            [.. explicitWeights.Where((_, index) => index % 2 == turn)],
            turn == lastUpdated))];
        ZstdBackwardBitStreamWriter stream = new ZstdBackwardBitStreamWriter()
            .Add(accuracyLog, chains[0][0]).Add(accuracyLog, chains[1][0]);
        for (int emitted = 0; emitted < explicitWeights.Length - 2; emitted++)
        {
            int[] chain = chains[emitted % 2];
            int step = emitted / 2;
            stream.Add(table.BitCount(chain[step]), chain[step + 1] - table.Baseline(chain[step]));
        }

        byte[] bits = stream.ToArray();
        return [(byte)(description.Length + bits.Length), .. description, .. bits];
    }

    /// <summary>Each symbol's prefix code and its length in bits, from the explicit weights.</summary>
    public static (int Code, int Length)[] Codes(byte[] explicitWeights)
    {
        int sum = explicitWeights.Sum(weight => weight == 0 ? 0 : 1 << (weight - 1));
        int tableLog = BitOperations.Log2((uint)sum) + 1;
        byte[] weights = [.. explicitWeights, (byte)(BitOperations.Log2((uint)((1 << tableLog) - sum)) + 1)];
        (int Code, int Length)[] codes = new (int, int)[weights.Length];
        int cell = 0;
        for (int weight = 1; weight <= tableLog; weight++)
        {
            for (int symbol = 0; symbol < weights.Length; symbol++)
            {
                if (weights[symbol] == weight)
                {
                    codes[symbol] = (cell >> (weight - 1), tableLog + 1 - weight);
                    cell += 1 << (weight - 1);
                }
            }
        }

        return codes;
    }

    /// <summary>One Huffman stream of <paramref name="literals"/>: each code in turn, as the decoder reads them.</summary>
    public static byte[] Stream(ReadOnlySpan<byte> literals, (int Code, int Length)[] codes)
    {
        ZstdBackwardBitStreamWriter stream = new();
        foreach (byte literal in literals)
        {
            stream.Add(codes[literal].Length, codes[literal].Code);
        }

        return stream.ToArray();
    }

    /// <summary>Four streams after their jump table, each but the last a quarter of the literals, rounded up.</summary>
    public static byte[] FourStreams(byte[] literals, (int Code, int Length)[] codes)
    {
        int share = (literals.Length + 3) / 4;
        byte[][] streams = [.. Enumerable.Range(0, 4).Select(stream => Stream(
            literals.AsSpan(stream * share, stream < 3 ? share : literals.Length - (3 * share)),
            codes))];
        return
        [
            (byte)streams[0].Length, (byte)(streams[0].Length >> 8),
            (byte)streams[1].Length, (byte)(streams[1].Length >> 8),
            (byte)streams[2].Length, (byte)(streams[2].Length >> 8),
            .. streams.SelectMany(stream => stream),
        ];
    }

    /// <summary>A literals section of <paramref name="literals"/> Huffman-coded behind <paramref name="tree"/>, or Treeless when it is empty.</summary>
    public static byte[] HuffmanLiterals(byte[] literals, byte[] explicitWeights, byte[] tree, bool fourStreams)
    {
        (int Code, int Length)[] codes = Codes(explicitWeights);
        byte[] payload = [.. tree, .. fourStreams ? FourStreams(literals, codes) : Stream(literals, codes)];
        return [.. CompressedHeader(tree.Length == 0 ? 3 : 2, fourStreams, literals.Length, payload.Length), .. payload];
    }
}
