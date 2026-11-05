namespace Surl.Compression;

/// <summary>
/// Finds the sequences of one zstd frame's blocks with <see cref="GreedyMatchFinder"/> and the frame's
/// repeat offsets: at each position the longest match at a repeat offset, unless the hash table offers
/// a longer one, and literals where neither offers four bytes. A match may reach back into any
/// earlier block of the frame, as far as the predefined offset table's highest code can name.
/// The repeat offsets advance only when the caller keeps a block as Compressed (<see cref="Keep"/>),
/// since a Raw or RLE block leaves a decoder's repeat offsets as they were.
/// </summary>
internal sealed class ZstdSequenceFinder(byte[] input)
{
    /// <summary>The farthest a match reaches back: Offset_Value below two to the power of 29, offset code 28 being the predefined table's highest.</summary>
    internal const int MaximumOffset = (1 << 29) - 1 - ZstdRepeatOffsets.Count;

    private readonly GreedyMatchFinder matchFinder = new(input);

    private ZstdRepeatOffsets repeatOffsets = new();

    /// <summary>The sequences and literals of <c>input[start..end]</c>, from the repeat offsets kept so far.</summary>
    public ZstdBlockSequences Find(int start, int end)
    {
        ZstdRepeatOffsets offsets = repeatOffsets.Copy();
        List<ZstdSequence> sequences = [];
        List<byte> literals = [];
        int literalStart = start;
        int position = start;
        while (position + GreedyMatchFinder.MinimumMatchLength <= end)
        {
            (int offset, int length) = FindMatch(position, end, offsets);
            if (length == 0)
            {
                position++;
                continue;
            }

            int literalLength = position - literalStart;
            literals.AddRange(input.AsSpan(literalStart, literalLength));
            sequences.Add(new ZstdSequence(literalLength, offsets.ValueFor(offset, literalLength), length));
            position += length;
            literalStart = position;
        }

        literals.AddRange(input.AsSpan(literalStart, end - literalStart));
        return new ZstdBlockSequences(sequences, [.. literals], offsets);
    }

    /// <summary>Keeps <paramref name="block"/>'s repeat offsets for the blocks after it.</summary>
    public void Keep(ZstdBlockSequences block) => repeatOffsets = block.RepeatOffsetsAfter;

    /// <summary>The match at <paramref name="position"/>, at most to <paramref name="end"/>: the hash table's, or a repeat offset's at least as long.</summary>
    private (int Offset, int Length) FindMatch(int position, int end, ZstdRepeatOffsets offsets)
    {
        (int Offset, int Length) best = matchFinder.FindAndRemember(position, Math.Max(0, position - MaximumOffset), end - position);
        for (int index = 0; index < ZstdRepeatOffsets.Count; index++)
        {
            int offset = offsets[index];
            int length = offset <= position ? MatchLength(position - offset, position, end) : 0;
            if (length >= GreedyMatchFinder.MinimumMatchLength && length >= best.Length)
            {
                best = (offset, length);
            }
        }

        return best;
    }

    private int MatchLength(int earlier, int position, int end)
    {
        int length = 0;
        while (position + length < end && input[earlier + length] == input[position + length])
        {
            length++;
        }

        return length;
    }
}
