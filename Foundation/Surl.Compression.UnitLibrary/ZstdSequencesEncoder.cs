using System.Numerics;

namespace Surl.Compression;

/// <summary>
/// Writes the sequences section of a zstd compressed block (RFC 8878, section 3.1.1.3.2) with every
/// table in Predefined_Mode: Number_of_Sequences, the compression modes byte of 0, then the bit
/// stream, which <see cref="ZstdSequencesSection"/> reads as the initial literal length, offset and
/// match length states, then for each sequence the offset, match length and literal length extra
/// bits and, but for the last, the literal length, match length and offset state updates.
/// </summary>
internal static class ZstdSequencesEncoder
{
    private const byte AllPredefinedModes = 0;
    private const int TwoByteCountMarker = 128;
    private const int LongCountMarker = 255;
    private const int LongCountBase = 0x7F00;

    /// <summary>The section for <paramref name="sequences"/>; with none, the single byte 0.</summary>
    public static byte[] Encode(IReadOnlyList<ZstdSequence> sequences)
    {
        if (sequences.Count == 0)
        {
            return [0];
        }

        int[] literalLengthCodes = [.. sequences.Select(sequence => CodeFor(ZstdSequencesSection.LiteralLengthBaselines, sequence.LiteralLength))];
        int[] matchLengthCodes = [.. sequences.Select(sequence => CodeFor(ZstdSequencesSection.MatchLengthBaselines, sequence.MatchLength))];
        int[] offsetCodes = [.. sequences.Select(sequence => BitOperations.Log2((ulong)sequence.OffsetValue))];
        ZstdSequenceStates states = new(
            ZstdFiniteStateEntropyEncodingTable.PredefinedLiteralLength.StatesFor(literalLengthCodes),
            ZstdFiniteStateEntropyEncodingTable.PredefinedOffset.StatesFor(offsetCodes),
            ZstdFiniteStateEntropyEncodingTable.PredefinedMatchLength.StatesFor(matchLengthCodes));
        ZstdBackwardBitWriter writer = new();
        for (int index = sequences.Count - 1; index >= 0; index--)
        {
            WriteStateUpdates(writer, states, index);
            ZstdSequence sequence = sequences[index];
            WriteExtraBits(writer, ZstdSequencesSection.LiteralLengthExtraBits, ZstdSequencesSection.LiteralLengthBaselines, literalLengthCodes[index], sequence.LiteralLength);
            WriteExtraBits(writer, ZstdSequencesSection.MatchLengthExtraBits, ZstdSequencesSection.MatchLengthBaselines, matchLengthCodes[index], sequence.MatchLength);
            writer.Write(offsetCodes[index], sequence.OffsetValue - (1L << offsetCodes[index]));
        }

        writer.Write(ZstdFiniteStateEntropyEncodingTable.PredefinedMatchLength.AccuracyLog, states.MatchLengths[0]);
        writer.Write(ZstdFiniteStateEntropyEncodingTable.PredefinedOffset.AccuracyLog, states.Offsets[0]);
        writer.Write(ZstdFiniteStateEntropyEncodingTable.PredefinedLiteralLength.AccuracyLog, states.LiteralLengths[0]);
        return [.. Count(sequences.Count), AllPredefinedModes, .. writer.Finish()];
    }

    /// <summary>Number_of_Sequences: one byte below 128, two below 0x7F00, else 255 and two more.</summary>
    private static byte[] Count(int count) => count switch
    {
        < TwoByteCountMarker => [(byte)count],
        < LongCountBase => [(byte)(TwoByteCountMarker + (count >> 8)), (byte)count],
        _ => [LongCountMarker, (byte)(count - LongCountBase), (byte)((count - LongCountBase) >> 8)],
    };

    /// <summary>The code whose baseline is the greatest not above <paramref name="value"/>.</summary>
    private static int CodeFor(int[] baselines, int value)
    {
        int index = Array.BinarySearch(baselines, value);
        return index >= 0 ? index : ~index - 1;
    }

    /// <summary>The offset, match length and literal length updates after sequence <paramref name="index"/>, none after the last, in reverse.</summary>
    private static void WriteStateUpdates(ZstdBackwardBitWriter writer, ZstdSequenceStates states, int index)
    {
        if (index + 1 == states.Offsets.Length)
        {
            return;
        }

        ZstdFiniteStateEntropyEncodingTable.PredefinedOffset.WriteTransition(writer, states.Offsets[index], states.Offsets[index + 1]);
        ZstdFiniteStateEntropyEncodingTable.PredefinedMatchLength.WriteTransition(writer, states.MatchLengths[index], states.MatchLengths[index + 1]);
        ZstdFiniteStateEntropyEncodingTable.PredefinedLiteralLength.WriteTransition(writer, states.LiteralLengths[index], states.LiteralLengths[index + 1]);
    }

    private static void WriteExtraBits(ZstdBackwardBitWriter writer, int[] extraBits, int[] baselines, int code, int value) =>
        writer.Write(extraBits[code], value - baselines[code]);

    /// <summary>The literal length, offset and match length states of every sequence.</summary>
    private sealed record ZstdSequenceStates(int[] LiteralLengths, int[] Offsets, int[] MatchLengths);
}
