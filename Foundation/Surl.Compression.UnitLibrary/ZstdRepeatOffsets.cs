namespace Surl.Compression;

/// <summary>
/// A zstd frame's three repeat offsets (RFC 8878, section 3.1.1.5), starting at 1, 4 and 8 and
/// carried from Compressed block to Compressed block. The decoder resolves an Offset_Value to the
/// offset it names; the encoder picks the Offset_Value naming an offset. Both update the repeat
/// offsets the same way, so that an encoder and a decoder that start alike stay alike.
/// </summary>
internal sealed class ZstdRepeatOffsets
{
    /// <summary>How many repeat offsets a frame keeps.</summary>
    public const int Count = 3;

    private readonly int[] offsets;

    /// <summary>The repeat offsets a frame starts with: 1, 4 and 8.</summary>
    public ZstdRepeatOffsets()
        : this([1, 4, 8])
    {
    }

    private ZstdRepeatOffsets(int[] offsets) => this.offsets = offsets;

    /// <summary>The repeat offset at <paramref name="index"/>, 0 being the most recent.</summary>
    public int this[int index] => offsets[index];

    /// <summary>An independent copy, which an encoder updates while it tries a block it may not keep.</summary>
    public ZstdRepeatOffsets Copy() => new([.. offsets]);

    /// <summary>
    /// The offset <paramref name="offsetValue"/> names: above 3 a new offset of the value minus 3;
    /// otherwise a repeat offset, shifted by one when <paramref name="literalLength"/> is 0, the fourth
    /// being the first repeat offset minus one. The repeat offsets are updated to match.
    /// </summary>
    public int Resolve(long offsetValue, int literalLength)
    {
        if (offsetValue > Count)
        {
            return MakeFirst((int)Math.Min(offsetValue - Count, int.MaxValue), Count - 1);
        }

        int index = (int)offsetValue - 1 + (literalLength == 0 ? 1 : 0);
        int offset = index < Count ? offsets[index] : offsets[0] - 1;
        if (offset == 0)
        {
            throw DecompressionRefusedException.Malformed("a repeat offset is 0");
        }

        return MakeFirst(offset, Math.Min(index, Count - 1));
    }

    /// <summary>
    /// The Offset_Value naming <paramref name="offset"/> after <paramref name="literalLength"/> literals:
    /// the repeat code of the first repeat offset equal to it, else the offset plus 3. The repeat
    /// offsets are updated as <see cref="Resolve"/> updates them for that value.
    /// </summary>
    public long ValueFor(int offset, int literalLength)
    {
        int shift = literalLength == 0 ? 1 : 0;
        long value = offset + (long)Count;
        for (int index = shift; index < shift + Count; index++)
        {
            int candidate = index < Count ? offsets[index] : offsets[0] - 1;
            if (candidate == offset)
            {
                value = index - shift + 1;
                break;
            }
        }

        Resolve(value, literalLength);
        return value;
    }

    /// <summary>Makes <paramref name="offset"/> the first repeat offset, moving the ones before <paramref name="replaced"/> down one.</summary>
    private int MakeFirst(int offset, int replaced)
    {
        for (int index = replaced; index > 0; index--)
        {
            offsets[index] = offsets[index - 1];
        }

        offsets[0] = offset;
        return offset;
    }
}
