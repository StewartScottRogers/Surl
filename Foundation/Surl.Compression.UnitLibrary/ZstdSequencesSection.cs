namespace Surl.Compression;

/// <summary>
/// Reads the sequences section of a zstd compressed block (RFC 8878, section 3.1.1.3.2) and executes
/// it (sections 3.1.1.4 and 3.1.1.5): each sequence appends literals, then copies a match from
/// earlier output. The literal length, offset and match length tables are each Predefined, RLE,
/// FSE_Compressed or Repeat, which reuses the frame's last table of that kind; the three repeat
/// offsets carry from block to block. One instance serves one frame.
/// </summary>
/// <param name="frameStart">Where the frame's output starts; no match reaches before it.</param>
internal sealed class ZstdSequencesSection(int frameStart)
{
    private const int PredefinedMode = 0;
    private const int RleMode = 1;
    private const int FseCompressedMode = 2;
    private const int MaximumLiteralLengthCode = 35;
    private const int MaximumOffsetCode = 31;
    private const int MaximumMatchLengthCode = 52;
    private const int MaximumLiteralLengthAccuracyLog = 9;
    private const int MaximumOffsetAccuracyLog = 8;
    private const int MaximumMatchLengthAccuracyLog = 9;
    private const int LongCountMarker = 255;
    private const int TwoByteCountMarker = 128;

    /// <summary>Literals_Length_Code extra bits (RFC 8878, section 3.1.1.3.2.1.1).</summary>
    internal static readonly int[] LiteralLengthExtraBits =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 3, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
    ];

    /// <summary>Match_Length_Code extra bits (RFC 8878, section 3.1.1.3.2.1.1).</summary>
    internal static readonly int[] MatchLengthExtraBits =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 2, 2, 3, 3, 4, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
    ];

    /// <summary>Literals_Length_Code baselines, each the last plus two to the power of its extra bits.</summary>
    internal static readonly int[] LiteralLengthBaselines = BuildBaselines(LiteralLengthExtraBits, 0);

    /// <summary>Match_Length_Code baselines, from 3.</summary>
    internal static readonly int[] MatchLengthBaselines = BuildBaselines(MatchLengthExtraBits, 3);

    private readonly ZstdRepeatOffsets repeatOffsets = new();

    private ZstdFiniteStateEntropyTable? literalLengthTable;
    private ZstdFiniteStateEntropyTable? offsetTable;
    private ZstdFiniteStateEntropyTable? matchLengthTable;

    /// <summary>
    /// Executes <paramref name="section"/> over <paramref name="literals"/>, appending to
    /// <paramref name="output"/>, whose earlier bytes the matches copy from; the literals left after
    /// the last sequence are appended too.
    /// </summary>
    public void Execute(ReadOnlySpan<byte> section, byte[] literals, BoundedDecompressionOutput output)
    {
        int count = ReadCount(section, out int position);
        if (count == 0)
        {
            output.Append(position == section.Length
                ? literals
                : throw DecompressionRefusedException.Malformed("bytes follow a sequences section of no sequences"));
            return;
        }

        byte modes = Take(section, ref position);
        if ((modes & 3) != 0)
        {
            throw DecompressionRefusedException.Malformed("a reserved bit of the sequence compression modes is set");
        }

        literalLengthTable = ReadTable(modes >> 6, literalLengthTable, ZstdFiniteStateEntropyTable.PredefinedLiteralLength, MaximumLiteralLengthAccuracyLog, MaximumLiteralLengthCode, section, ref position);
        offsetTable = ReadTable((modes >> 4) & 3, offsetTable, ZstdFiniteStateEntropyTable.PredefinedOffset, MaximumOffsetAccuracyLog, MaximumOffsetCode, section, ref position);
        matchLengthTable = ReadTable((modes >> 2) & 3, matchLengthTable, ZstdFiniteStateEntropyTable.PredefinedMatchLength, MaximumMatchLengthAccuracyLog, MaximumMatchLengthCode, section, ref position);
        int literalsUsed = ExecuteSequences(new ZstdBackwardBitReader(section[position..]), count, literals, output);
        output.Append(literals.AsSpan(literalsUsed));
    }

    private static int[] BuildBaselines(int[] extraBits, int first)
    {
        int[] baselines = new int[extraBits.Length];
        int next = first;
        for (int code = 0; code < extraBits.Length; code++)
        {
            baselines[code] = next;
            next += 1 << extraBits[code];
        }

        return baselines;
    }

    /// <summary>Number_of_Sequences: one byte below 128, two below 255, else 255 and two more (RFC 8878, section 3.1.1.3.2.1).</summary>
    private static int ReadCount(ReadOnlySpan<byte> section, out int position)
    {
        position = 0;
        int first = Take(section, ref position);
        if (first < TwoByteCountMarker)
        {
            return first;
        }

        if (first < LongCountMarker)
        {
            return ((first - TwoByteCountMarker) << 8) + Take(section, ref position);
        }

        return Take(section, ref position) + (Take(section, ref position) << 8) + 0x7F00;
    }

    private static ZstdFiniteStateEntropyTable ReadTable(
        int mode,
        ZstdFiniteStateEntropyTable? previous,
        ZstdFiniteStateEntropyTable predefined,
        int maxAccuracyLog,
        int maxCode,
        ReadOnlySpan<byte> section,
        ref int position)
    {
        switch (mode)
        {
            case PredefinedMode:
                return predefined;
            case RleMode:
                byte code = Take(section, ref position);
                return code <= maxCode
                    ? ZstdFiniteStateEntropyTable.Rle(code)
                    : throw DecompressionRefusedException.Malformed("an RLE sequence code is above the most its kind allows");
            case FseCompressedMode:
                ZstdFiniteStateEntropyTable table = ZstdFiniteStateEntropyTable.Read(section[position..], maxAccuracyLog, maxCode, out int bytesRead);
                position += bytesRead;
                return table;
            default:
                return previous ?? throw DecompressionRefusedException.Malformed("Repeat mode has no earlier sequence table in the frame");
        }
    }

    /// <summary>
    /// Decodes and executes <paramref name="count"/> sequences: the initial literal length, offset and
    /// match length states, then for each sequence the offset, match length and literal length extra
    /// bits and, but for the last, the literal length, match length and offset state updates.
    /// </summary>
    private int ExecuteSequences(ZstdBackwardBitReader reader, int count, byte[] literals, BoundedDecompressionOutput output)
    {
        ZstdFiniteStateEntropyTable literalLengths = literalLengthTable!;
        ZstdFiniteStateEntropyTable offsets = offsetTable!;
        ZstdFiniteStateEntropyTable matchLengths = matchLengthTable!;
        int literalLengthState = literalLengths.ReadInitialState(reader);
        int offsetState = offsets.ReadInitialState(reader);
        int matchLengthState = matchLengths.ReadInitialState(reader);
        int literalsUsed = 0;
        for (int sequence = 0; sequence < count; sequence++)
        {
            int offsetCode = offsets.Symbol(offsetState);
            int matchLengthCode = matchLengths.Symbol(matchLengthState);
            int literalLengthCode = literalLengths.Symbol(literalLengthState);
            long offsetValue = (1L << offsetCode) + reader.ReadBits(offsetCode);
            int matchLength = MatchLengthBaselines[matchLengthCode] + (int)reader.ReadBits(MatchLengthExtraBits[matchLengthCode]);
            int literalLength = LiteralLengthBaselines[literalLengthCode] + (int)reader.ReadBits(LiteralLengthExtraBits[literalLengthCode]);
            literalsUsed = AppendLiterals(literals, literalsUsed, literalLength, output);
            output.CopyFromEarlier(repeatOffsets.Resolve(offsetValue, literalLength), matchLength, frameStart);
            if (sequence + 1 < count)
            {
                literalLengthState = literalLengths.NextState(literalLengthState, reader);
                matchLengthState = matchLengths.NextState(matchLengthState, reader);
                offsetState = offsets.NextState(offsetState, reader);
            }
        }

        return reader.RemainingBits == 0
            ? literalsUsed
            : throw DecompressionRefusedException.Malformed("the sequences bit stream does not end where its sequences do");
    }

    private static int AppendLiterals(byte[] literals, int used, int length, BoundedDecompressionOutput output)
    {
        if (length > literals.Length - used)
        {
            throw DecompressionRefusedException.Malformed("a sequence takes more literals than the block holds");
        }

        output.Append(literals.AsSpan(used, length));
        return used + length;
    }

    private static byte Take(ReadOnlySpan<byte> section, ref int position)
    {
        if (position >= section.Length)
        {
            throw DecompressionRefusedException.Malformed("the sequences section is truncated");
        }

        return section[position++];
    }
}
