namespace Surl.Compression;

/// <summary>
/// The bytes a decoder has written so far, which refuses, with a chosen outcome, any write that
/// would take it past its limit, and copies matches from what it holds (the LZ77 back-reference
/// both Snappy and LZ4 use), overlapping copies included.
/// </summary>
internal sealed class BoundedDecompressionOutput(int limit, DecompressionOutcome outcomeWhenFull, string reasonWhenFull)
{
    private const int FirstCapacity = 4096;

    private byte[] buffer = new byte[Math.Min(limit, FirstCapacity)];

    /// <summary>How many bytes have been written.</summary>
    public int Count { get; private set; }

    /// <summary>How many more bytes may be written before the limit.</summary>
    public int Remaining => limit - Count;

    /// <summary>The bytes written so far.</summary>
    public ReadOnlySpan<byte> Written => buffer.AsSpan(0, Count);

    /// <summary>Appends <paramref name="bytes"/>, or refuses when they would pass the limit.</summary>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        MakeRoomFor(bytes.Length);
        bytes.CopyTo(buffer.AsSpan(Count));
        Count += bytes.Length;
    }

    /// <summary>
    /// Appends <paramref name="length"/> bytes copied from <paramref name="offset"/> bytes back,
    /// byte by byte so that a copy longer than its offset repeats what it has just written.
    /// Refuses as malformed an offset of 0 or one reaching before <paramref name="windowStart"/>.
    /// </summary>
    public void CopyFromEarlier(long offset, int length, int windowStart)
    {
        if (offset == 0 || offset > Count - windowStart)
        {
            throw DecompressionRefusedException.Malformed("a copy offset is 0 or reaches before the start of the output");
        }

        MakeRoomFor(length);
        int from = Count - (int)offset;
        for (int index = 0; index < length; index++)
        {
            buffer[Count + index] = buffer[from + index];
        }

        Count += length;
    }

    /// <summary>The bytes written, as an array of their own.</summary>
    public byte[] ToArray() => Written.ToArray();

    private void MakeRoomFor(int length)
    {
        if (length > limit - Count)
        {
            throw new DecompressionRefusedException(outcomeWhenFull, reasonWhenFull);
        }

        if (Count + length > buffer.Length)
        {
            Array.Resize(ref buffer, (int)Math.Min(limit, Math.Max(Count + (long)length, buffer.Length * 2L)));
        }
    }
}
