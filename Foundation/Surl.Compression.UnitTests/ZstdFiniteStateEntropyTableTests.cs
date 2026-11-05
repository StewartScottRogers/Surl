namespace Surl.Compression;

/// <summary>
/// The FSE decoding tables against RFC 8878's Appendix A, which lists the predefined tables state by
/// state (symbol, bits, baseline), and table descriptions written as section 4.1.1 says.
/// </summary>
[TestClass]
public sealed class ZstdFiniteStateEntropyTableTests
{
    [TestMethod]
    [DataRow(0, 0, 4, 0)]
    [DataRow(1, 0, 4, 16)]
    [DataRow(2, 1, 5, 32)]
    [DataRow(3, 3, 5, 0)]
    [DataRow(63, 32, 6, 0)]
    public void PredefinedLiteralLength_State_IsAppendixAsRow(int state, int symbol, int bits, int baseline)
    {
        AssertRow(ZstdFiniteStateEntropyTable.PredefinedLiteralLength, state, symbol, bits, baseline);
    }

    [TestMethod]
    [DataRow(0, 0, 6, 0)]
    [DataRow(1, 1, 4, 0)]
    [DataRow(2, 2, 5, 32)]
    [DataRow(3, 3, 5, 0)]
    public void PredefinedMatchLength_State_IsAppendixAsRow(int state, int symbol, int bits, int baseline)
    {
        AssertRow(ZstdFiniteStateEntropyTable.PredefinedMatchLength, state, symbol, bits, baseline);
    }

    [TestMethod]
    [DataRow(0, 0, 5, 0)]
    [DataRow(1, 6, 4, 0)]
    [DataRow(2, 9, 5, 0)]
    [DataRow(3, 15, 5, 0)]
    [DataRow(4, 21, 5, 0)]
    [DataRow(5, 3, 5, 0)]
    [DataRow(6, 7, 4, 0)]
    public void PredefinedOffset_State_IsAppendixAsRow(int state, int symbol, int bits, int baseline)
    {
        AssertRow(ZstdFiniteStateEntropyTable.PredefinedOffset, state, symbol, bits, baseline);
    }

    [TestMethod]
    public void Read_DescriptionOfThePredefinedOffsetCounts_GivesThePredefinedTable()
    {
        short[] counts = [1, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1];
        byte[] description = ZstdFseDescriptionWriter.Write(counts, 5);

        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Read([.. description, 0xFF], 8, 31, out int bytesRead);

        Assert.AreEqual(description.Length, bytesRead);
        AssertSameTable(ZstdFiniteStateEntropyTable.PredefinedOffset, table);
    }

    [TestMethod]
    public void Read_LongZeroRun_TakesRepeatFieldsOfThree()
    {
        short[] counts = [16, 0, 0, 0, 0, 0, 0, 0, 16];

        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Read(ZstdFseDescriptionWriter.Write(counts, 5), 8, 31, out _);

        AssertSameTable(ZstdFiniteStateEntropyTable.FromNormalizedCounts(counts, 5), table);
        Assert.AreEqual(8, Enumerable.Range(0, 32).Max(table.Symbol));
    }

    [TestMethod]
    public void Rle_AnyState_DecodesItsSymbolAndReadsNoBits()
    {
        ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Rle(7);

        Assert.AreEqual(0, table.AccuracyLog);
        Assert.AreEqual(7, table.Symbol(0));
        Assert.AreEqual(0, table.BitCount(0));
    }

    private static void AssertRow(ZstdFiniteStateEntropyTable table, int state, int symbol, int bits, int baseline)
    {
        Assert.AreEqual(symbol, table.Symbol(state));
        Assert.AreEqual(bits, table.BitCount(state));
        Assert.AreEqual(baseline, table.Baseline(state));
    }

    private static void AssertSameTable(ZstdFiniteStateEntropyTable expected, ZstdFiniteStateEntropyTable actual)
    {
        Assert.AreEqual(expected.AccuracyLog, actual.AccuracyLog);
        for (int state = 0; state < 1 << expected.AccuracyLog; state++)
        {
            AssertRow(actual, state, expected.Symbol(state), expected.BitCount(state), expected.Baseline(state));
        }
    }
}
