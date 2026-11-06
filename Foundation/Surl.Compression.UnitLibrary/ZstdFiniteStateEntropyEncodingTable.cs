namespace Surl.Compression;

/// <summary>
/// The encoding side of a <see cref="ZstdFiniteStateEntropyTable"/> (RFC 8878, section 4.1). An FSE
/// encoder works from the last symbol back to the first: the last symbol's state is any state that
/// decodes it, and each earlier symbol's state is the one that decodes it and whose baseline and bit
/// count reach the state after it. The states decoding one symbol split the table's states between
/// them, so that exactly one such state exists for every symbol the table can decode.
/// </summary>
internal sealed class ZstdFiniteStateEntropyEncodingTable
{
    private readonly ZstdFiniteStateEntropyTable table;
    private readonly int size;
    private readonly int[] lastStates;
    private readonly int[] previousStates;

    private ZstdFiniteStateEntropyEncodingTable(ZstdFiniteStateEntropyTable table)
    {
        this.table = table;
        size = 1 << table.AccuracyLog;
        int symbolCount = Enumerable.Range(0, size).Max(state => table.Symbol(state)) + 1;
        lastStates = new int[symbolCount];
        previousStates = new int[symbolCount * size];
        for (int state = size - 1; state >= 0; state--)
        {
            int symbol = table.Symbol(state);
            lastStates[symbol] = state;
            int reach = 1 << table.BitCount(state);
            Array.Fill(previousStates, state, (symbol * size) + table.Baseline(state), reach);
        }
    }

    /// <summary>The encoding side of the predefined literal length table.</summary>
    public static ZstdFiniteStateEntropyEncodingTable PredefinedLiteralLength { get; } = new(ZstdFiniteStateEntropyTable.PredefinedLiteralLength);

    /// <summary>The encoding side of the predefined match length table.</summary>
    public static ZstdFiniteStateEntropyEncodingTable PredefinedMatchLength { get; } = new(ZstdFiniteStateEntropyTable.PredefinedMatchLength);

    /// <summary>The encoding side of the predefined offset code table.</summary>
    public static ZstdFiniteStateEntropyEncodingTable PredefinedOffset { get; } = new(ZstdFiniteStateEntropyTable.PredefinedOffset);

    /// <summary>The bits the initial state is written in.</summary>
    public int AccuracyLog => table.AccuracyLog;

    /// <summary>
    /// The states decoding <paramref name="symbols"/> in order: the last the lowest state decoding the
    /// last symbol, each earlier one the state reaching the one after it.
    /// </summary>
    public int[] StatesFor(IReadOnlyList<int> symbols)
    {
        int[] states = new int[symbols.Count];
        states[^1] = lastStates[symbols[^1]];
        for (int index = symbols.Count - 2; index >= 0; index--)
        {
            states[index] = previousStates[(symbols[index] * size) + states[index + 1]];
        }

        return states;
    }

    /// <summary>Writes the bits that take the decoder from <paramref name="state"/> to <paramref name="nextState"/>.</summary>
    public void WriteTransition(ZstdBackwardBitWriter writer, int state, int nextState) =>
        writer.Write(table.BitCount(state), nextState - table.Baseline(state));
}
