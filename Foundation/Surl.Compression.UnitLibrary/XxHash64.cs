using System.Buffers.Binary;
using System.Numerics;

namespace Surl.Compression;

/// <summary>
/// XXH64 with seed 0, as xxHash's <c>doc/xxhash_spec.md</c> defines it: four accumulators over
/// 32-byte stripes, merged, then the remaining 8-byte words, a 4-byte word and bytes, then the
/// avalanche. A zstd frame's content checksum is its lowest 32 bits (RFC 8878, section 3.1.1).
/// </summary>
public static class XxHash64
{
    private const ulong Prime1 = 0x9E3779B185EBCA87UL;
    private const ulong Prime2 = 0xC2B2AE3D27D4EB4FUL;
    private const ulong Prime3 = 0x165667B19E3779F9UL;
    private const ulong Prime4 = 0x85EBCA77C2B2AE63UL;
    private const ulong Prime5 = 0x27D4EB2F165667C5UL;
    private const int StripeLength = 32;

    /// <summary>The XXH64 of <paramref name="input"/> with seed 0.</summary>
    public static ulong Hash(ReadOnlySpan<byte> input)
    {
        int position = input.Length - (input.Length % StripeLength);
        ulong hash = (input.Length >= StripeLength ? Stripes(input[..position]) : Prime5) + (ulong)input.Length;
        for (; position + sizeof(ulong) <= input.Length; position += sizeof(ulong))
        {
            hash ^= Round(0, BinaryPrimitives.ReadUInt64LittleEndian(input[position..]));
            hash = (BitOperations.RotateLeft(hash, 27) * Prime1) + Prime4;
        }

        if (position + sizeof(uint) <= input.Length)
        {
            hash ^= BinaryPrimitives.ReadUInt32LittleEndian(input[position..]) * Prime1;
            hash = (BitOperations.RotateLeft(hash, 23) * Prime2) + Prime3;
            position += sizeof(uint);
        }

        for (; position < input.Length; position++)
        {
            hash ^= input[position] * Prime5;
            hash = BitOperations.RotateLeft(hash, 11) * Prime1;
        }

        hash = (hash ^ (hash >> 33)) * Prime2;
        hash = (hash ^ (hash >> 29)) * Prime3;
        return hash ^ (hash >> 32);
    }

    /// <summary>The four accumulators over whole stripes, converged and each merged in.</summary>
    private static ulong Stripes(ReadOnlySpan<byte> stripes)
    {
        ulong first = unchecked(Prime1 + Prime2);
        ulong second = Prime2;
        ulong third = 0;
        ulong fourth = unchecked(0UL - Prime1);
        for (int position = 0; position < stripes.Length; position += StripeLength)
        {
            first = Round(first, BinaryPrimitives.ReadUInt64LittleEndian(stripes[position..]));
            second = Round(second, BinaryPrimitives.ReadUInt64LittleEndian(stripes[(position + 8)..]));
            third = Round(third, BinaryPrimitives.ReadUInt64LittleEndian(stripes[(position + 16)..]));
            fourth = Round(fourth, BinaryPrimitives.ReadUInt64LittleEndian(stripes[(position + 24)..]));
        }

        ulong hash = BitOperations.RotateLeft(first, 1) + BitOperations.RotateLeft(second, 7)
            + BitOperations.RotateLeft(third, 12) + BitOperations.RotateLeft(fourth, 18);
        hash = Merge(hash, first);
        hash = Merge(hash, second);
        hash = Merge(hash, third);
        return Merge(hash, fourth);
    }

    private static ulong Round(ulong accumulator, ulong lane) =>
        BitOperations.RotateLeft(accumulator + (lane * Prime2), 31) * Prime1;

    private static ulong Merge(ulong hash, ulong accumulator) =>
        ((hash ^ Round(0, accumulator)) * Prime1) + Prime4;
}
