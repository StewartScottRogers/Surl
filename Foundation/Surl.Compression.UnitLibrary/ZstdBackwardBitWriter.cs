namespace Surl.Compression;

/// <summary>
/// Writes a zstd bit stream that <see cref="ZstdBackwardBitReader"/> reads backward (RFC 8878,
/// section 4.1). Fields are written in the reverse of the order the decoder reads them, each packed
/// least significant bit first; <see cref="Finish"/> adds the end mark, a 1 bit, after the last, so
/// that the decoder, reading from the end mark down, meets the last field written first.
/// </summary>
internal sealed class ZstdBackwardBitWriter
{
    private readonly List<byte> bytes = [];
    private ulong pending;
    private int pendingBitCount;

    /// <summary>
    /// Writes the low <paramref name="width"/> bits of <paramref name="value"/>, at most 32, which the
    /// decoder reads before every field written earlier.
    /// </summary>
    public void Write(int width, long value)
    {
        pending |= ((ulong)value & ((1UL << width) - 1)) << pendingBitCount;
        pendingBitCount += width;
        while (pendingBitCount >= 8)
        {
            bytes.Add((byte)pending);
            pending >>= 8;
            pendingBitCount -= 8;
        }
    }

    /// <summary>Writes the end mark, pads its byte with zero bits, and gives the stream.</summary>
    public byte[] Finish()
    {
        Write(1, 1);
        Write((8 - pendingBitCount) & 7, 0);
        return [.. bytes];
    }
}
