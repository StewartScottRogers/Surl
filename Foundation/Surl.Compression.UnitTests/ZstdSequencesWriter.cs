using System.Numerics;

namespace Surl.Compression;

/// <summary>
/// Writes a zstd sequences section (RFC 8878, section 3.1.1.3.2): the sequence count, the
/// compression modes byte, the table descriptions given, and the bit stream, in which each value is
/// a code from the RFC's literal length, offset and match length code tables plus its extra bits.
/// </summary>
internal static class ZstdSequencesWriter
{
    /// <summary>Literals_Length_Code baselines and extra bits, section 3.1.1.3.2.1.1's table.</summary>
    private static readonly (int Baseline, int Bits)[] LiteralLengthCodes =
    [
        (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 0), (9, 0), (10, 0), (11, 0),
        (12, 0), (13, 0), (14, 0), (15, 0), (16, 1), (18, 1), (20, 1), (22, 1), (24, 2), (28, 2), (32, 3),
        (40, 3), (48, 4), (64, 6), (128, 7), (256, 8), (512, 9), (1024, 10), (2048, 11), (4096, 12),
        (8192, 13), (16384, 14), (32768, 15), (65536, 16),
    ];

    /// <summary>Match_Length_Code baselines and extra bits, section 3.1.1.3.2.1.1's table.</summary>
    private static readonly (int Baseline, int Bits)[] MatchLengthCodes =
    [
        .. Enumerable.Range(3, 32).Select(length => (length, 0)),
        (35, 1), (37, 1), (39, 1), (41, 1), (43, 2), (47, 2), (51, 3), (59, 3), (67, 4), (83, 4), (99, 5),
        (131, 7), (259, 8), (515, 9), (1027, 10), (2051, 11), (4099, 12), (8195, 13), (16387, 14),
        (32771, 15), (65539, 16),
    ];

    /// <summary>
    /// The section for <paramref name="sequences"/>, whose tables are <paramref name="literalLengths"/>,
    /// <paramref name="offsets"/> and <paramref name="matchLengths"/>, under <paramref name="modes"/>
    /// with <paramref name="tableDescriptions"/> after it (RLE codes or FSE descriptions).
    /// </summary>
    public static byte[] Section(
        IReadOnlyList<ZstdTestSequence> sequences,
        byte modes,
        byte[] tableDescriptions,
        ZstdFiniteStateEntropyTable literalLengths,
        ZstdFiniteStateEntropyTable offsets,
        ZstdFiniteStateEntropyTable matchLengths)
    {
        (int Code, int Extra)[] literalLengthValues = [.. sequences.Select(sequence => Encode(LiteralLengthCodes, sequence.LiteralLength))];
        (int Code, int Extra)[] matchLengthValues = [.. sequences.Select(sequence => Encode(MatchLengthCodes, sequence.MatchLength))];
        (int Code, int Extra)[] offsetValues = [.. sequences.Select(sequence => EncodeOffset(sequence.OffsetValue))];
        int[] literalLengthStates = ZstdStateChains.Chain(literalLengths, [.. literalLengthValues.Select(value => (byte)value.Code)], false);
        int[] offsetStates = ZstdStateChains.Chain(offsets, [.. offsetValues.Select(value => (byte)value.Code)], false);
        int[] matchLengthStates = ZstdStateChains.Chain(matchLengths, [.. matchLengthValues.Select(value => (byte)value.Code)], false);
        ZstdBackwardBitStreamWriter stream = new ZstdBackwardBitStreamWriter()
            .Add(literalLengths.AccuracyLog, literalLengthStates[0])
            .Add(offsets.AccuracyLog, offsetStates[0])
            .Add(matchLengths.AccuracyLog, matchLengthStates[0]);
        for (int index = 0; index < sequences.Count; index++)
        {
            stream.Add(offsetValues[index].Code, offsetValues[index].Extra)
                .Add(MatchLengthCodes[matchLengthValues[index].Code].Bits, matchLengthValues[index].Extra)
                .Add(LiteralLengthCodes[literalLengthValues[index].Code].Bits, literalLengthValues[index].Extra);
            if (index + 1 < sequences.Count)
            {
                AddUpdate(stream, literalLengths, literalLengthStates, index);
                AddUpdate(stream, matchLengths, matchLengthStates, index);
                AddUpdate(stream, offsets, offsetStates, index);
            }
        }

        return [.. Count(sequences.Count), modes, .. tableDescriptions, .. stream.ToArray()];
    }

    /// <summary>Number_of_Sequences in one, two or three bytes.</summary>
    public static byte[] Count(int count) => count switch
    {
        < 128 => [(byte)count],
        < 0x7F00 => [(byte)(128 + (count >> 8)), (byte)count],
        _ => [255, (byte)(count - 0x7F00), (byte)((count - 0x7F00) >> 8)],
    };

    private static void AddUpdate(ZstdBackwardBitStreamWriter stream, ZstdFiniteStateEntropyTable table, int[] states, int index) =>
        stream.Add(table.BitCount(states[index]), states[index + 1] - table.Baseline(states[index]));

    private static (int Code, int Extra) Encode((int Baseline, int Bits)[] codes, int value)
    {
        int code = Array.FindLastIndex(codes, entry => entry.Baseline <= value);
        return (code, value - codes[code].Baseline);
    }

    private static (int Code, int Extra) EncodeOffset(long offsetValue)
    {
        int code = BitOperations.Log2((ulong)offsetValue);
        return (code, (int)(offsetValue - (1L << code)));
    }
}
