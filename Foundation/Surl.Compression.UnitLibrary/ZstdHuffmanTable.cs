using System.Numerics;

namespace Surl.Compression;

/// <summary>
/// A zstd Huffman decoding table (RFC 8878, section 4.2): weights read from a tree description,
/// direct as 4-bit values or FSE-compressed, the last symbol's weight implied by completing a power of
/// two. Codes go to symbols by weight, then by symbol value, the lowest weight first (section
/// 4.2.1), and each is looked up by peeking <see cref="TableLog"/> bits of a backward bit stream.
/// </summary>
internal sealed class ZstdHuffmanTable
{
    private const int MaximumTableLog = 11;
    private const int MaximumWeight = 11;
    private const int MaximumWeightAccuracyLog = 6;
    private const int FirstDirectHeader = 128;
    private const int MaximumExplicitWeights = 255;

    private readonly byte[] symbols;
    private readonly byte[] bitCounts;

    private ZstdHuffmanTable(int tableLog, byte[] symbols, byte[] bitCounts)
    {
        TableLog = tableLog;
        this.symbols = symbols;
        this.bitCounts = bitCounts;
    }

    /// <summary>The longest code in bits; the table has two to the power of this many cells.</summary>
    public int TableLog { get; }

    /// <summary>
    /// Reads a Huffman tree description from the start of <paramref name="source"/>: a header byte of
    /// 128 or more is followed by that minus 127 weights in 4-bit fields, a smaller one is the size of
    /// FSE-compressed weights. <paramref name="bytesRead"/> is the description's length.
    /// </summary>
    public static ZstdHuffmanTable Read(ReadOnlySpan<byte> source, out int bytesRead)
    {
        if (source.IsEmpty)
        {
            throw DecompressionRefusedException.Malformed("a Huffman tree description is missing");
        }

        int header = source[0];
        byte[] weights = header >= FirstDirectHeader
            ? ReadDirectWeights(source, header - (FirstDirectHeader - 1), out bytesRead)
            : ReadCompressedWeights(source, header, out bytesRead);
        return FromWeights(weights);
    }

    /// <summary>
    /// Builds the table from <paramref name="explicitWeights"/>, for symbols 0 up, and the weight of
    /// the symbol after them, which completes the sum of two to the power of each weight minus one to
    /// a power of two; weights that cannot are refused as malformed.
    /// </summary>
    public static ZstdHuffmanTable FromWeights(ReadOnlySpan<byte> explicitWeights)
    {
        byte[] weights = CompleteWeights(explicitWeights, out int tableLog);
        byte[] symbols = new byte[1 << tableLog];
        byte[] bitCounts = new byte[1 << tableLog];
        int cell = 0;
        for (int weight = 1; weight <= tableLog; weight++)
        {
            for (int symbol = 0; symbol < weights.Length; symbol++)
            {
                int cells = weights[symbol] == weight ? 1 << (weight - 1) : 0;
                symbols.AsSpan(cell, cells).Fill((byte)symbol);
                bitCounts.AsSpan(cell, cells).Fill((byte)(tableLog + 1 - weight));
                cell += cells;
            }
        }

        return new ZstdHuffmanTable(tableLog, symbols, bitCounts);
    }

    /// <summary>Reads one code from <paramref name="reader"/>; the caller checks the stream ends where it should.</summary>
    public byte DecodeSymbol(ZstdBackwardBitReader reader)
    {
        int cell = (int)reader.PeekBits(TableLog);
        reader.SkipBits(bitCounts[cell]);
        return symbols[cell];
    }

    private static byte[] CompleteWeights(ReadOnlySpan<byte> explicitWeights, out int tableLog)
    {
        int sum = 0;
        foreach (byte weight in explicitWeights)
        {
            sum += weight == 0 ? 0 : 1 << (weight - 1);
        }

        if (sum == 0)
        {
            throw DecompressionRefusedException.Malformed("every Huffman weight is zero");
        }

        tableLog = BitOperations.Log2((uint)sum) + 1;
        int missing = (1 << tableLog) - sum;
        if (tableLog > MaximumTableLog || !BitOperations.IsPow2(missing))
        {
            throw DecompressionRefusedException.Malformed("the Huffman weights do not complete a power of two of at most 2048");
        }

        return [.. explicitWeights, (byte)(BitOperations.Log2((uint)missing) + 1)];
    }

    private static byte[] ReadDirectWeights(ReadOnlySpan<byte> source, int count, out int bytesRead)
    {
        bytesRead = 1 + ((count + 1) / 2);
        if (source.Length < bytesRead)
        {
            throw DecompressionRefusedException.Malformed("the Huffman weights are truncated");
        }

        byte[] weights = new byte[count];
        for (int index = 0; index < count; index++)
        {
            byte pair = source[1 + (index / 2)];
            weights[index] = (byte)(index % 2 == 0 ? pair >> 4 : pair & 0x0F);
        }

        return weights;
    }

    private static byte[] ReadCompressedWeights(ReadOnlySpan<byte> source, int size, out int bytesRead)
    {
        bytesRead = 1 + size;
        if (size == 0 || source.Length < bytesRead)
        {
            throw DecompressionRefusedException.Malformed("the FSE-compressed Huffman weights are empty or truncated");
        }

        return DecodeWeightStates(source.Slice(1, size));
    }

    /// <summary>
    /// Decodes FSE-compressed weights with two states taking turns (RFC 8878, section 4.2.1.2),
    /// until a state's update reads past the start of the stream; the other state's symbol is then
    /// the last weight.
    /// </summary>
    private static byte[] DecodeWeightStates(ReadOnlySpan<byte> compressed)
    {
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Read(compressed, MaximumWeightAccuracyLog, MaximumWeight, out int tableBytes);
        ZstdBackwardBitReader reader = new(compressed[tableBytes..]);
        int[] states = [table.ReadInitialState(reader), table.ReadInitialState(reader)];
        if (reader.Overflowed)
        {
            throw DecompressionRefusedException.Malformed("the FSE-compressed Huffman weights are truncated");
        }

        List<byte> weights = [];
        for (int turn = 0; ; turn ^= 1)
        {
            AddWeight(weights, table.Symbol(states[turn]));
            states[turn] = table.NextState(states[turn], reader);
            if (reader.Overflowed)
            {
                AddWeight(weights, table.Symbol(states[turn ^ 1]));
                return [.. weights];
            }
        }
    }

    private static void AddWeight(List<byte> weights, byte weight)
    {
        if (weights.Count == MaximumExplicitWeights)
        {
            throw DecompressionRefusedException.Malformed("the FSE-compressed Huffman weights name more than 255 symbols");
        }

        weights.Add(weight);
    }
}
