using System.Buffers.Binary;
using System.Numerics;

namespace Surl.Compression;

/// <summary>
/// XXH32 with seed 0, as xxHash's <c>doc/xxhash_spec.md</c> defines it: four accumulators over
/// 16-byte stripes, then the remaining 4-byte words and bytes, then the avalanche. The LZ4 frame
/// uses it for its header, block and content checksums.
/// </summary>
public static class XxHash32
{
    private const uint Prime1 = 2654435761U;
    private const uint Prime2 = 2246822519U;
    private const uint Prime3 = 3266489917U;
    private const uint Prime4 = 668265263U;
    private const uint Prime5 = 374761393U;
    private const int StripeLength = 16;

    /// <summary>The XXH32 of <paramref name="input"/> with seed 0.</summary>
    public static uint Hash(ReadOnlySpan<byte> input)
    {
        int position = input.Length - (input.Length % StripeLength);
        uint hash = (input.Length >= StripeLength ? Stripes(input[..position]) : Prime5) + (uint)input.Length;
        for (; position + sizeof(uint) <= input.Length; position += sizeof(uint))
        {
            hash = BitOperations.RotateLeft(hash + (BinaryPrimitives.ReadUInt32LittleEndian(input[position..]) * Prime3), 17) * Prime4;
        }

        for (; position < input.Length; position++)
        {
            hash = BitOperations.RotateLeft(hash + (input[position] * Prime5), 11) * Prime1;
        }

        hash = (hash ^ (hash >> 15)) * Prime2;
        hash = (hash ^ (hash >> 13)) * Prime3;
        return hash ^ (hash >> 16);
    }

    /// <summary>The four accumulators over whole stripes, converged into one.</summary>
    private static uint Stripes(ReadOnlySpan<byte> stripes)
    {
        uint first = unchecked(Prime1 + Prime2);
        uint second = Prime2;
        uint third = 0;
        uint fourth = unchecked(0U - Prime1);
        for (int position = 0; position < stripes.Length; position += StripeLength)
        {
            first = Round(first, BinaryPrimitives.ReadUInt32LittleEndian(stripes[position..]));
            second = Round(second, BinaryPrimitives.ReadUInt32LittleEndian(stripes[(position + 4)..]));
            third = Round(third, BinaryPrimitives.ReadUInt32LittleEndian(stripes[(position + 8)..]));
            fourth = Round(fourth, BinaryPrimitives.ReadUInt32LittleEndian(stripes[(position + 12)..]));
        }

        return BitOperations.RotateLeft(first, 1) + BitOperations.RotateLeft(second, 7)
            + BitOperations.RotateLeft(third, 12) + BitOperations.RotateLeft(fourth, 18);
    }

    private static uint Round(uint accumulator, uint lane) =>
        BitOperations.RotateLeft(accumulator + (lane * Prime2), 13) * Prime1;
}
