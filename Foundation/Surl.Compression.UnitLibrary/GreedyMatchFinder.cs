using System.Buffers.Binary;

namespace Surl.Compression;

/// <summary>
/// The greedy hash-table matcher the Snappy, LZ4 and zstd encoders use: it remembers, for each hash of four bytes,
/// the last position it was asked about, and offers that position as a match when its four bytes
/// are the same and it lies inside the allowed window, extended as far as the bytes agree.
/// </summary>
internal sealed class GreedyMatchFinder(byte[] input)
{
    /// <summary>The shortest match the encoders take, which every one of their formats can encode.</summary>
    public const int MinimumMatchLength = 4;

    private const int HashBits = 14;

    private readonly int[] lastPositionPlusOne = new int[1 << HashBits];

    /// <summary>
    /// Remembers <paramref name="position"/> and returns the earlier match for the bytes there:
    /// its offset back and its length, at most <paramref name="maximumLength"/>; a length of 0 when
    /// there is none at or after <paramref name="windowStart"/>. LZ4 blocks of at most 64 KiB keep
    /// every offset within LZ4's 65535, Snappy has a 4-byte offset for any distance, and zstd's
    /// window start keeps every offset one its predefined offset table can name.
    /// The caller keeps four bytes and <paramref name="maximumLength"/> bytes readable at <paramref name="position"/>.
    /// </summary>
    public (int Offset, int Length) FindAndRemember(int position, int windowStart, int maximumLength)
    {
        uint key = BinaryPrimitives.ReadUInt32LittleEndian(input.AsSpan(position));
        int slot = (int)((key * 2654435761U) >> (32 - HashBits));
        int candidate = lastPositionPlusOne[slot] - 1;
        lastPositionPlusOne[slot] = position + 1;
        if (candidate < windowStart
            || BinaryPrimitives.ReadUInt32LittleEndian(input.AsSpan(candidate)) != key)
        {
            return (0, 0);
        }

        int length = MinimumMatchLength;
        while (length < maximumLength && input[candidate + length] == input[position + length])
        {
            length++;
        }

        return (position - candidate, length);
    }
}
