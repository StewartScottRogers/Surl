using System.Security.Cryptography;

namespace Surl.Kerberos;

/// <summary>
/// AES in CBC mode with ciphertext stealing and an all-zero initial vector, the cipher of RFC 3962
/// section 5 and RFC 8009 section 5: the last two cipher blocks are swapped and the last one is cut
/// to the length of the last plain block, so the cipher text is exactly as long as the plain text.
/// Built over the base class library's <see cref="SymmetricAlgorithm.EncryptCbc(ReadOnlySpan{byte}, ReadOnlySpan{byte}, PaddingMode)" />.
/// </summary>
/// <remarks>
/// Kerberos always encrypts a 16-byte confounder ahead of the message, so input shorter than one
/// block is never encrypted here and is refused.
/// </remarks>
internal static class AesCiphertextStealing
{
    /// <summary>The AES block length, in bytes.</summary>
    public const int BlockLength = 16;

    private static readonly byte[] ZeroInitialVector = new byte[BlockLength];

    /// <summary>Encrypts <paramref name="plainText" /> under <paramref name="key" />.</summary>
    /// <param name="key">A 16- or 32-byte AES key.</param>
    /// <param name="plainText">At least one block of plain text.</param>
    /// <returns>The cipher text, as long as <paramref name="plainText" />.</returns>
    /// <exception cref="ArgumentException"><paramref name="plainText" /> is shorter than one block.</exception>
    public static byte[] Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plainText)
    {
        ThrowIfShorterThanOneBlock(plainText.Length, nameof(plainText));
        using Aes aes = CreateAes(key);
        int blockCount = CountBlocks(plainText.Length);
        byte[] padded = new byte[blockCount * BlockLength];
        plainText.CopyTo(padded);
        byte[] chained = aes.EncryptCbc(padded, ZeroInitialVector, PaddingMode.None);
        if (blockCount == 1)
        {
            return chained;
        }

        int lastLength = plainText.Length - ((blockCount - 1) * BlockLength);
        int secondLastStart = (blockCount - 2) * BlockLength;
        byte[] cipherText = new byte[plainText.Length];
        chained.AsSpan(0, secondLastStart).CopyTo(cipherText);
        chained.AsSpan(secondLastStart + BlockLength, BlockLength).CopyTo(cipherText.AsSpan(secondLastStart));
        chained.AsSpan(secondLastStart, lastLength).CopyTo(cipherText.AsSpan(secondLastStart + BlockLength));
        return cipherText;
    }

    /// <summary>Decrypts <paramref name="cipherText" /> under <paramref name="key" />.</summary>
    /// <param name="key">A 16- or 32-byte AES key.</param>
    /// <param name="cipherText">At least one block of cipher text.</param>
    /// <returns>The plain text, as long as <paramref name="cipherText" />.</returns>
    /// <exception cref="ArgumentException"><paramref name="cipherText" /> is shorter than one block.</exception>
    public static byte[] Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> cipherText)
    {
        ThrowIfShorterThanOneBlock(cipherText.Length, nameof(cipherText));
        using Aes aes = CreateAes(key);
        int blockCount = CountBlocks(cipherText.Length);
        if (blockCount == 1)
        {
            return aes.DecryptCbc(cipherText, ZeroInitialVector, PaddingMode.None);
        }

        int lastLength = cipherText.Length - ((blockCount - 1) * BlockLength);
        int secondLastStart = (blockCount - 2) * BlockLength;
        ReadOnlySpan<byte> lastWholeBlock = cipherText.Slice(secondLastStart, BlockLength);
        ReadOnlySpan<byte> stolenBlockHead = cipherText[(secondLastStart + BlockLength)..];

        // Deciphering the last whole block gives the zero-padded last plain block XOR the true
        // second-last cipher block, whose head was sent cut short and whose tail this restores.
        byte[] lastBlockXorPrevious = aes.DecryptEcb(lastWholeBlock, PaddingMode.None);
        byte[] chained = new byte[(blockCount - 1) * BlockLength];
        cipherText[..secondLastStart].CopyTo(chained);
        Span<byte> secondLastCipherBlock = chained.AsSpan(secondLastStart);
        stolenBlockHead.CopyTo(secondLastCipherBlock);
        lastBlockXorPrevious.AsSpan(lastLength).CopyTo(secondLastCipherBlock[lastLength..]);

        byte[] plainText = new byte[cipherText.Length];
        aes.DecryptCbc(chained, ZeroInitialVector, PaddingMode.None).CopyTo(plainText, 0);
        for (int index = 0; index < lastLength; index++)
        {
            plainText[secondLastStart + BlockLength + index] = (byte)(lastBlockXorPrevious[index] ^ secondLastCipherBlock[index]);
        }

        return plainText;
    }

    /// <summary>Enciphers one whole block under <paramref name="key" />, with no chaining.</summary>
    /// <param name="key">A 16- or 32-byte AES key.</param>
    /// <param name="block">Exactly one block.</param>
    /// <returns>The enciphered block.</returns>
    public static byte[] EncryptBlock(ReadOnlySpan<byte> key, ReadOnlySpan<byte> block)
    {
        using Aes aes = CreateAes(key);
        return aes.EncryptEcb(block, PaddingMode.None);
    }

    private static Aes CreateAes(ReadOnlySpan<byte> key)
    {
        Aes aes = Aes.Create();
        aes.Key = key.ToArray();
        return aes;
    }

    private static int CountBlocks(int length) => (length + BlockLength - 1) / BlockLength;

    private static void ThrowIfShorterThanOneBlock(int length, string parameterName)
    {
        if (length < BlockLength)
        {
            throw new ArgumentException($"AES ciphertext stealing needs at least {BlockLength} bytes; got {length}.", parameterName);
        }
    }
}
