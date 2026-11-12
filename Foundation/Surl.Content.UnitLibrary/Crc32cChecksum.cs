using System.Buffers.Binary;
using System.Numerics;

namespace Surl.Content;

/// <summary>
/// The CRC-32C (Castagnoli) checksum, the one Cloud Storage's <c>x-goog-hash</c> carries and the
/// object metadata index caches beside an object's MD5 (ADR-0110, decision 1.1).
/// </summary>
/// <remarks>
/// A checksum is the finished value, so a checksum of no bytes is 0 and a run of bytes can be
/// checksummed in pieces: <c>Append(Append(0, a), b)</c> is the checksum of <c>a</c> then <c>b</c>.
/// </remarks>
public static class Crc32cChecksum
{
    /// <summary>
    /// The checksum of <paramref name="bytes"/> following the bytes <paramref name="checksum"/> covers.
    /// </summary>
    /// <param name="checksum">The checksum of the bytes so far; 0 for none.</param>
    /// <param name="bytes">The next bytes.</param>
    /// <returns>The checksum of every byte so far.</returns>
    public static uint Append(uint checksum, ReadOnlySpan<byte> bytes)
    {
        var state = ~checksum;
        while (bytes.Length >= sizeof(ulong))
        {
            state = BitOperations.Crc32C(state, BinaryPrimitives.ReadUInt64LittleEndian(bytes));
            bytes = bytes[sizeof(ulong)..];
        }

        foreach (var octet in bytes)
        {
            state = BitOperations.Crc32C(state, octet);
        }

        return ~state;
    }
}
