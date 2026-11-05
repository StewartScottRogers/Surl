using System.Numerics;

namespace Surl.Compression;

/// <summary>
/// Reads a zstd bit stream backward (RFC 8878, section 4.1): the last byte's highest set bit is an
/// end mark, the bits below it are read first, and the first bit read is a value's most significant.
/// Reading past the start of the stream gives zero bits and makes <see cref="Overflowed"/> true,
/// which the caller checks, as FSE-compressed Huffman weights end by running out of bits.
/// </summary>
internal sealed class ZstdBackwardBitReader
{
    private readonly byte[] bytes;

    /// <summary>Reads <paramref name="stream"/>; refuses as malformed one that is empty or ends in a zero byte, having no end mark.</summary>
    public ZstdBackwardBitReader(ReadOnlySpan<byte> stream)
    {
        if (stream.IsEmpty || stream[^1] == 0)
        {
            throw DecompressionRefusedException.Malformed("a bit stream has no end mark");
        }

        bytes = stream.ToArray();
        RemainingBits = ((stream.Length - 1) * 8) + BitOperations.Log2(stream[^1]);
    }

    /// <summary>The bits not yet read; negative once reading has gone past the start of the stream.</summary>
    public int RemainingBits { get; private set; }

    /// <summary>Whether more bits were read than the stream holds.</summary>
    public bool Overflowed => RemainingBits < 0;

    /// <summary>The next <paramref name="count"/> bits (at most 32) without reading them; bits before the start are zero.</summary>
    public uint PeekBits(int count)
    {
        uint value = 0;
        for (int offset = 1; offset <= count; offset++)
        {
            int index = RemainingBits - offset;
            value = (value << 1) | (index >= 0 ? (uint)(bytes[index >> 3] >> (index & 7)) & 1 : 0);
        }

        return value;
    }

    /// <summary>Reads <paramref name="count"/> bits without giving them.</summary>
    public void SkipBits(int count) => RemainingBits -= count;

    /// <summary>Reads and gives the next <paramref name="count"/> bits (at most 32).</summary>
    public uint ReadBits(int count)
    {
        uint value = PeekBits(count);
        SkipBits(count);
        return value;
    }
}
