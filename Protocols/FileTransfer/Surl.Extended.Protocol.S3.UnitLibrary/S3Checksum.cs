using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// One body's running additional checksum ("Checking object integrity"; ADR-0108 decision 4.5):
/// CRC-32 by <see cref="S3Crc32"/>, CRC-32C by <see cref="BitOperations.Crc32C(uint, byte)"/>, SHA-1
/// and SHA-256 by <see cref="IncrementalHash"/>. Each CRC is written big-endian, as S3 sends it.
/// </summary>
internal sealed class S3Checksum : IDisposable
{
    private readonly IncrementalHash? hash;
    private uint crc = uint.MaxValue;

    /// <summary>
    /// Starts a checksum of no bytes.
    /// </summary>
    /// <param name="algorithm">The algorithm.</param>
    public S3Checksum(S3ChecksumAlgorithm algorithm)
    {
        Algorithm = algorithm;
        hash = algorithm == S3ChecksumAlgorithm.Sha1 ? IncrementalHash.CreateHash(HashAlgorithmName.SHA1)
            : algorithm == S3ChecksumAlgorithm.Sha256 ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
            : null;
    }

    /// <summary>
    /// The algorithm.
    /// </summary>
    public S3ChecksumAlgorithm Algorithm { get; }

    /// <summary>
    /// Runs the next bytes of the body through the checksum.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        if (hash is not null)
        {
            hash.AppendData(bytes);
        }
        else
        {
            crc = Algorithm == S3ChecksumAlgorithm.Crc32 ? S3Crc32.Append(crc, bytes) : AppendCrc32C(crc, bytes);
        }
    }

    /// <summary>
    /// The digest of every byte appended.
    /// </summary>
    /// <returns>The digest's bytes.</returns>
    public byte[] Digest()
    {
        if (hash is not null)
        {
            return hash.GetCurrentHash();
        }

        var digest = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(digest, ~crc);

        return digest;
    }

    /// <inheritdoc/>
    public void Dispose() => hash?.Dispose();

    private static uint AppendCrc32C(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc = BitOperations.Crc32C(crc, value);
        }

        return crc;
    }
}
