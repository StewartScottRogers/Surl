using System.Numerics;

namespace Surl.Compression;

/// <summary>
/// A zstd Finite State Entropy decoding table (RFC 8878, section 4.1): for each state, the symbol it
/// decodes, the bits the next state reads and the baseline they are added to. Built from normalized
/// counts read from a block (section 4.1.1), from a predefined distribution (section 3.1.1.3.2.2,
/// whose decoding tables are the RFC's Appendix A), or as a one-symbol RLE table.
/// </summary>
internal sealed class ZstdFiniteStateEntropyTable
{
    private const int MinimumAccuracyLog = 5;
    private const int AccuracyLogFieldBits = 4;
    private const int ZeroRunFieldBits = 2;
    private const int ZeroRunContinues = 3;

    private static readonly short[] LiteralLengthCounts =
    [
        4, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 2, 1, 1, 1, 1, 1, -1, -1, -1, -1,
    ];

    private static readonly short[] MatchLengthCounts =
    [
        1, 4, 3, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1, -1, -1,
    ];

    private static readonly short[] OffsetCounts =
    [
        1, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1,
    ];

    private readonly byte[] symbols;
    private readonly int[] baselines;
    private readonly byte[] bitCounts;

    private ZstdFiniteStateEntropyTable(int accuracyLog, byte[] symbols, int[] baselines, byte[] bitCounts)
    {
        AccuracyLog = accuracyLog;
        this.symbols = symbols;
        this.baselines = baselines;
        this.bitCounts = bitCounts;
    }

    /// <summary>The predefined literal length table, accuracy log 6.</summary>
    public static ZstdFiniteStateEntropyTable PredefinedLiteralLength { get; } = FromNormalizedCounts(LiteralLengthCounts, 6);

    /// <summary>The predefined match length table, accuracy log 6.</summary>
    public static ZstdFiniteStateEntropyTable PredefinedMatchLength { get; } = FromNormalizedCounts(MatchLengthCounts, 6);

    /// <summary>The predefined offset code table, accuracy log 5.</summary>
    public static ZstdFiniteStateEntropyTable PredefinedOffset { get; } = FromNormalizedCounts(OffsetCounts, 5);

    /// <summary>The bits that read the initial state; the table has two to the power of this many states.</summary>
    public int AccuracyLog { get; }

    /// <summary>The table that always decodes <paramref name="symbol"/> and reads no bits (RLE_Mode).</summary>
    public static ZstdFiniteStateEntropyTable Rle(byte symbol) => new(0, [symbol], [0], [0]);

    /// <summary>
    /// Reads a table description from the start of <paramref name="source"/>: the accuracy log, then
    /// the normalized counts (-1 is "less than one") as variable-width fields, with runs of zero
    /// counts in 2-bit repeat fields. <paramref name="bytesRead"/> is the whole bytes it takes.
    /// Refuses as malformed an accuracy log above <paramref name="maxAccuracyLog"/>, a symbol above
    /// <paramref name="maxSymbol"/>, counts that do not fill the table, and a truncated description.
    /// </summary>
    public static ZstdFiniteStateEntropyTable Read(ReadOnlySpan<byte> source, int maxAccuracyLog, int maxSymbol, out int bytesRead)
    {
        int bitPosition = 0;
        int accuracyLog = MinimumAccuracyLog + ReadForward(source, ref bitPosition, AccuracyLogFieldBits);
        if (accuracyLog > maxAccuracyLog)
        {
            throw DecompressionRefusedException.Malformed("an FSE accuracy log is above the most its table allows");
        }

        short[] counts = ReadCounts(source, ref bitPosition, accuracyLog, maxSymbol);
        if (bitPosition > source.Length * 8)
        {
            throw DecompressionRefusedException.Malformed("an FSE table description is truncated");
        }

        bytesRead = (bitPosition + 7) / 8;
        return FromNormalizedCounts(counts, accuracyLog);
    }

    /// <summary>
    /// Builds the decoding table for <paramref name="counts"/>, one per symbol from 0 (-1 for "less
    /// than one"), which must add up to two to the power of <paramref name="accuracyLog"/>, each -1
    /// counting as one; counts that do not are refused as malformed.
    /// </summary>
    public static ZstdFiniteStateEntropyTable FromNormalizedCounts(short[] counts, int accuracyLog)
    {
        int size = 1 << accuracyLog;
        if (counts.Sum(count => count == -1 ? 1 : count) != size)
        {
            throw DecompressionRefusedException.Malformed("FSE counts do not add up to the table size");
        }

        byte[] symbols = new byte[size];
        int[] nextStateNumbers = new int[counts.Length];
        int lastCell = PlaceLessThanOneSymbols(counts, symbols, nextStateNumbers);
        SpreadSymbols(counts, symbols, lastCell);
        int[] baselines = new int[size];
        byte[] bitCounts = new byte[size];
        for (int state = 0; state < size; state++)
        {
            int nextStateNumber = nextStateNumbers[symbols[state]]++;
            int bits = accuracyLog - BitOperations.Log2((uint)nextStateNumber);
            bitCounts[state] = (byte)bits;
            baselines[state] = (nextStateNumber << bits) - size;
        }

        return new ZstdFiniteStateEntropyTable(accuracyLog, symbols, baselines, bitCounts);
    }

    /// <summary>The symbol <paramref name="state"/> decodes.</summary>
    public byte Symbol(int state) => symbols[state];

    /// <summary>The bits the state after <paramref name="state"/> reads.</summary>
    public int BitCount(int state) => bitCounts[state];

    /// <summary>The value the bits the state after <paramref name="state"/> reads are added to.</summary>
    public int Baseline(int state) => baselines[state];

    /// <summary>Reads the initial state, <see cref="AccuracyLog"/> bits.</summary>
    public int ReadInitialState(ZstdBackwardBitReader reader) => (int)reader.ReadBits(AccuracyLog);

    /// <summary>The state after <paramref name="state"/>: its baseline plus the bits it reads.</summary>
    public int NextState(int state, ZstdBackwardBitReader reader) => Baseline(state) + (int)reader.ReadBits(BitCount(state));

    /// <summary>
    /// Reads counts until they fill the table or name more symbols than <paramref name="maxSymbol"/>
    /// allows, each field's width shrinking as the probability left to share falls.
    /// </summary>
    private static short[] ReadCounts(ReadOnlySpan<byte> source, ref int bitPosition, int accuracyLog, int maxSymbol)
    {
        int threshold = 1 << accuracyLog;
        int remaining = threshold + 1;
        int bitCount = accuracyLog + 1;
        List<short> counts = [];
        bool previousWasZero = false;
        while (remaining > 1 && counts.Count <= maxSymbol)
        {
            if (previousWasZero)
            {
                SkipZeroRun(source, ref bitPosition, counts, maxSymbol);
            }

            int count = ReadCount(source, ref bitPosition, bitCount, threshold, remaining) - 1;
            counts.Add((short)count);
            remaining -= Math.Abs(count);
            previousWasZero = count == 0;
            while (remaining < threshold)
            {
                bitCount--;
                threshold >>= 1;
            }
        }

        return [.. counts];
    }

    /// <summary>Puts each "less than one" symbol in a cell from the end of the table down; gives the last cell left.</summary>
    private static int PlaceLessThanOneSymbols(short[] counts, byte[] symbols, int[] nextStateNumbers)
    {
        int lastCell = symbols.Length - 1;
        for (int symbol = 0; symbol < counts.Length; symbol++)
        {
            nextStateNumbers[symbol] = Math.Abs((int)counts[symbol]);
            if (counts[symbol] == -1)
            {
                symbols[lastCell--] = (byte)symbol;
            }
        }

        return lastCell;
    }

    /// <summary>Spreads every other symbol's cells by the RFC's step, skipping the cells above <paramref name="lastCell"/>.</summary>
    private static void SpreadSymbols(short[] counts, byte[] symbols, int lastCell)
    {
        int step = (symbols.Length >> 1) + (symbols.Length >> 3) + 3;
        int mask = symbols.Length - 1;
        int position = 0;
        for (int symbol = 0; symbol < counts.Length; symbol++)
        {
            for (int placed = 0; placed < counts[symbol]; placed++)
            {
                symbols[position] = (byte)symbol;
                do
                {
                    position = (position + step) & mask;
                }
                while (position > lastCell);
            }
        }
    }

    /// <summary>One count plus one, in <paramref name="bitCount"/> bits or one fewer when its value is small (RFC 8878, section 4.1.1).</summary>
    private static int ReadCount(ReadOnlySpan<byte> source, ref int bitPosition, int bitCount, int threshold, int remaining)
    {
        int maximum = (2 * threshold) - 1 - remaining;
        int start = bitPosition;
        int value = ReadForward(source, ref bitPosition, bitCount);
        int low = value & (threshold - 1);
        if (low < maximum)
        {
            bitPosition = start + bitCount - 1;
            return low;
        }

        return value >= threshold ? value - maximum : value;
    }

    /// <summary>Adds the zero counts a run of 2-bit repeat fields gives, each 3 followed by another field.</summary>
    private static void SkipZeroRun(ReadOnlySpan<byte> source, ref int bitPosition, List<short> counts, int maxSymbol)
    {
        int repeat;
        do
        {
            repeat = ReadForward(source, ref bitPosition, ZeroRunFieldBits);
            counts.AddRange(Enumerable.Repeat((short)0, repeat));
        }
        while (repeat == ZeroRunContinues);

        if (counts.Count > maxSymbol)
        {
            throw DecompressionRefusedException.Malformed("FSE counts name a symbol above the most its table allows");
        }
    }

    /// <summary>
    /// Reads <paramref name="count"/> bits forward, least significant first; bits past the end of
    /// <paramref name="source"/> read as zero, and <see cref="Read"/> refuses a description whose
    /// bits end past it.
    /// </summary>
    private static int ReadForward(ReadOnlySpan<byte> source, ref int bitPosition, int count)
    {
        int value = 0;
        for (int offset = 0; offset < count; offset++)
        {
            int index = bitPosition + offset;
            value |= ((index >> 3) < source.Length ? (source[index >> 3] >> (index & 7)) & 1 : 0) << offset;
        }

        bitPosition += count;
        return value;
    }
}
