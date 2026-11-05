using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The two hashes <c>x-goog-hash</c> carries ("HTTP headers and common request headers"; ADR-0110,
/// decision 1.1), computed together as bytes arrive: the CRC-32C (Castagnoli), with
/// <see cref="BitOperations.Crc32C(uint, ulong)"/>, and the MD5.
/// </summary>
internal sealed class GcsObjectHashes : IDisposable
{
    private readonly IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
    private uint crc = uint.MaxValue;

    /// <summary>
    /// Adds bytes to both hashes.
    /// </summary>
    /// <param name="bytes">The next bytes.</param>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        md5.AppendData(bytes);
        var state = crc;
        while (bytes.Length >= sizeof(ulong))
        {
            state = BitOperations.Crc32C(state, BinaryPrimitives.ReadUInt64LittleEndian(bytes));
            bytes = bytes[sizeof(ulong)..];
        }

        foreach (var octet in bytes)
        {
            state = BitOperations.Crc32C(state, octet);
        }

        crc = state;
    }

    /// <summary>
    /// Finishes both hashes.
    /// </summary>
    /// <returns>The big-endian CRC-32C and the MD5 of every byte appended.</returns>
    public GcsObjectDigests Finish()
    {
        var crc32c = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(crc32c, ~crc);

        return new GcsObjectDigests(crc32c, md5.GetHashAndReset());
    }

    /// <inheritdoc/>
    public void Dispose() => md5.Dispose();
}
