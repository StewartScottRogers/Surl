namespace Surl.Compression;

/// <summary>
/// Finds the FSE states that decode a run of symbols, working back from the last as an FSE encoder
/// does (RFC 8878, section 4.1): each state's symbol is the one wanted, and the state after it lies
/// in the range its baseline and bit count reach.
/// </summary>
internal static class ZstdStateChains
{
    /// <summary>
    /// The states decoding <paramref name="symbols"/> in order; when <paramref name="lastReadsBits"/>,
    /// the last state's update reads at least one bit, so that it can run past the start of a stream.
    /// </summary>
    public static int[] Chain(ZstdFiniteStateEntropyTable table, byte[] symbols, bool lastReadsBits)
    {
        int size = 1 << table.AccuracyLog;
        int[] states = new int[symbols.Length];
        states[^1] = Enumerable.Range(0, size).First(state =>
            table.Symbol(state) == symbols[^1] && (!lastReadsBits || table.BitCount(state) > 0));
        for (int index = symbols.Length - 2; index >= 0; index--)
        {
            int next = states[index + 1];
            states[index] = Enumerable.Range(0, size).First(state =>
                table.Symbol(state) == symbols[index]
                && next >= table.Baseline(state)
                && next < table.Baseline(state) + (1 << table.BitCount(state)));
        }

        return states;
    }
}
