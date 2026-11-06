using System.Buffers.Binary;
using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// One seeded identifier source: SHA-256 in counter mode over a fixed prefix, block <c>n</c> being
/// SHA-256 of the prefix followed by <c>n</c> as 8 big-endian bytes. Made by
/// <see cref="SeededIdentifierRandom"/>.
/// </summary>
/// <param name="prefix">The bytes every block hashes before its counter.</param>
internal sealed class Sha256CounterIdentifierRandom(byte[] prefix) : IIdentifierRandom
{
    private readonly byte[] input = CreateInput(prefix);
    private readonly byte[] block = new byte[SHA256.HashSizeInBytes];
    private long nextCounter;
    private int blockPosition = SHA256.HashSizeInBytes;

    /// <inheritdoc/>
    public void NextBytes(Span<byte> destination)
    {
        while (!destination.IsEmpty)
        {
            if (blockPosition == block.Length)
            {
                FillNextBlock();
            }

            var count = Math.Min(destination.Length, block.Length - blockPosition);
            block.AsSpan(blockPosition, count).CopyTo(destination);
            blockPosition += count;
            destination = destination[count..];
        }
    }

    private static byte[] CreateInput(byte[] prefix)
    {
        var input = new byte[prefix.Length + sizeof(long)];
        prefix.CopyTo(input, 0);
        return input;
    }

    private void FillNextBlock()
    {
        BinaryPrimitives.WriteInt64BigEndian(input.AsSpan(input.Length - sizeof(long)), nextCounter);
        nextCounter++;
        SHA256.HashData(input, block);
        blockPosition = 0;
    }
}
