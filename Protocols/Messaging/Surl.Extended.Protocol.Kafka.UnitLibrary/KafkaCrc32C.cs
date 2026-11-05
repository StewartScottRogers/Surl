using System.Numerics;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The CRC-32C (Castagnoli) a record batch carries in its <c>crc</c> field (the Kafka "Record
/// Batch" section; ADR-0105 decision 4): the reflected polynomial 0x82F63B78, started at all
/// ones and finished by inverting every bit, computed with
/// <see cref="BitOperations.Crc32C(uint, byte)"/>.
/// </summary>
public static class KafkaCrc32C
{
    /// <summary>The CRC-32C of <paramref name="bytes"/>.</summary>
    /// <param name="bytes">The bytes, in order.</param>
    /// <returns>The checksum, as the batch's <c>crc</c> field holds it.</returns>
    public static uint Compute(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc = BitOperations.Crc32C(crc, value);
        }

        return ~crc;
    }
}
