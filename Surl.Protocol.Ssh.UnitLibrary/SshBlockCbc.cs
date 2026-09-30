namespace Surl.Protocol.Ssh;

/// <summary>
/// A hand-built 8-byte block cipher in CBC mode as RFC 4253 section 6.3 runs it for
/// <c>blowfish-cbc</c> and <c>cast128-cbc</c>, offered only with
/// <c>--allow-weak-ssh-algorithms</c> (ADR-0061): the IV is the key exchange's, and each
/// packet's first block chains from the last ciphertext block of the packet before. The block
/// function is <see cref="Surl.Cryptography.Blowfish.Blowfish"/>'s or
/// <see cref="Surl.Cryptography.Cast128.Cast128"/>'s, neither of which is a BCL
/// <see cref="System.Security.Cryptography.SymmetricAlgorithm"/>, so the chaining is done here
/// rather than by <see cref="SshCbc"/>.
/// </summary>
internal sealed class SshBlockCbc : ISshPacketCipher
{
    private readonly SshBlockFunction encryptBlock;
    private readonly SshBlockFunction decryptBlock;
    private byte[] chainingBlock;

    /// <summary>
    /// Runs CBC over the block functions given.
    /// </summary>
    /// <param name="encryptBlock">Encrypts one block.</param>
    /// <param name="decryptBlock">Decrypts one block.</param>
    /// <param name="initializationVector">The IV: one block.</param>
    public SshBlockCbc(SshBlockFunction encryptBlock, SshBlockFunction decryptBlock, byte[] initializationVector)
    {
        this.encryptBlock = encryptBlock;
        this.decryptBlock = decryptBlock;
        chainingBlock = initializationVector;
    }

    /// <summary>
    /// Transforms one block of <paramref name="source"/> into <paramref name="destination"/>.
    /// </summary>
    /// <param name="source">The block in.</param>
    /// <param name="destination">The block out.</param>
    internal delegate void SshBlockFunction(ReadOnlySpan<byte> source, Span<byte> destination);

    /// <inheritdoc/>
    public int BlockSize => chainingBlock.Length;

    /// <summary>
    /// Blowfish in CBC mode, <c>blowfish-cbc</c>.
    /// </summary>
    /// <param name="key">The key: 16 bytes.</param>
    /// <param name="initializationVector">The IV: 8 bytes.</param>
    /// <returns>The cipher.</returns>
    public static SshBlockCbc Blowfish(byte[] key, byte[] initializationVector)
    {
        var blowfish = new Surl.Cryptography.Blowfish.Blowfish(key);

        return new(blowfish.EncryptBlock, blowfish.DecryptBlock, initializationVector);
    }

    /// <summary>
    /// CAST-128 in CBC mode, <c>cast128-cbc</c>.
    /// </summary>
    /// <param name="key">The key: 16 bytes.</param>
    /// <param name="initializationVector">The IV: 8 bytes.</param>
    /// <returns>The cipher.</returns>
    public static SshBlockCbc Cast128(byte[] key, byte[] initializationVector)
    {
        var cast128 = new Surl.Cryptography.Cast128.Cast128(key);

        return new(cast128.EncryptBlock, cast128.DecryptBlock, initializationVector);
    }

    /// <inheritdoc/>
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var ciphertext = new byte[plaintext.Length];
        var block = new byte[BlockSize];
        for (var offset = 0; offset < plaintext.Length; offset += BlockSize)
        {
            for (var index = 0; index < BlockSize; index++)
            {
                block[index] = (byte)(plaintext[offset + index] ^ chainingBlock[index]);
            }

            var cipherBlock = ciphertext.AsSpan(offset, BlockSize);
            encryptBlock(block, cipherBlock);
            chainingBlock = cipherBlock.ToArray();
        }

        return ciphertext;
    }

    /// <inheritdoc/>
    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext)
    {
        var plaintext = new byte[ciphertext.Length];
        for (var offset = 0; offset < ciphertext.Length; offset += BlockSize)
        {
            var cipherBlock = ciphertext.Slice(offset, BlockSize);
            var plainBlock = plaintext.AsSpan(offset, BlockSize);
            decryptBlock(cipherBlock, plainBlock);
            for (var index = 0; index < BlockSize; index++)
            {
                plainBlock[index] ^= chainingBlock[index];
            }

            chainingBlock = cipherBlock.ToArray();
        }

        return plaintext;
    }
}
