using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// AES in counter mode as RFC 4344 section 4 composes it for <c>aes128-ctr</c>,
/// <c>aes192-ctr</c> and <c>aes256-ctr</c>: the IV is a 128-bit big-endian counter, each block
/// of key stream is AES of the counter, and the counter goes up by one per block, modulo
/// 2^128, carrying on from one packet to the next. The block cipher is the BCL's
/// <c>Aes.EncryptEcb</c> (ADR-0051, decision 2).
/// </summary>
internal sealed class SshAesCtr
{
    /// <summary>
    /// AES's block size in bytes, which is also the counter's.
    /// </summary>
    public const int BlockSize = 16;

    private readonly Aes aes;
    private readonly byte[] counter;

    /// <summary>
    /// Creates the cipher at the start of its key stream.
    /// </summary>
    /// <param name="key">The key: 16, 24 or 32 bytes.</param>
    /// <param name="initialCounter">The IV, the counter's first value: 16 bytes.</param>
    public SshAesCtr(byte[] key, byte[] initialCounter)
    {
        aes = Aes.Create();
        aes.Key = key;
        counter = (byte[])initialCounter.Clone();
    }

    /// <summary>
    /// XORs <paramref name="input"/> with the next <paramref name="input"/>.Length bytes of key
    /// stream; encrypting and decrypting are the same operation. SSH hands it whole blocks only.
    /// </summary>
    /// <param name="input">The bytes, a whole number of blocks long.</param>
    /// <returns>The transformed bytes.</returns>
    public byte[] Transform(ReadOnlySpan<byte> input)
    {
        var counterBlocks = new byte[input.Length];
        for (var offset = 0; offset < counterBlocks.Length; offset += BlockSize)
        {
            counter.CopyTo(counterBlocks, offset);
            Increment();
        }

        var output = aes.EncryptEcb(counterBlocks, PaddingMode.None);
        for (var index = 0; index < output.Length; index++)
        {
            output[index] ^= input[index];
        }

        return output;
    }

    private void Increment()
    {
        for (var index = BlockSize - 1; index >= 0; index--)
        {
            if (++counter[index] != 0)
            {
                return;
            }
        }
    }
}
