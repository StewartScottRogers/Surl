using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// A block cipher in CBC mode as RFC 4253 section 6.3 runs it for <c>aes256-cbc</c>,
/// <c>rijndael-cbc@lysator.liu.se</c>, <c>aes192-cbc</c>, <c>aes128-cbc</c> and <c>3des-cbc</c>,
/// offered only with <c>--allow-weak-ssh-algorithms</c>: the IV is the key exchange's, and each
/// packet's first block chains from the last ciphertext block of the packet before. The block
/// cipher is the BCL's <see cref="System.Security.Cryptography.Aes"/> or <see cref="TripleDES"/>
/// (ADR-0051, decision 2).
/// </summary>
internal sealed class SshCbc : ISshPacketCipher
{
    private readonly SymmetricAlgorithm cipher;
    private byte[] chainingBlock;

    private SshCbc(SymmetricAlgorithm cipher, byte[] key, byte[] initializationVector)
    {
        this.cipher = cipher;
        cipher.Key = key;
        chainingBlock = initializationVector;
    }

    /// <inheritdoc/>
    public int BlockSize => chainingBlock.Length;

    /// <summary>
    /// AES in CBC mode.
    /// </summary>
    /// <param name="key">The key: 16, 24 or 32 bytes.</param>
    /// <param name="initializationVector">The IV: 16 bytes.</param>
    /// <returns>The cipher.</returns>
    public static SshCbc Aes(byte[] key, byte[] initializationVector) => new(System.Security.Cryptography.Aes.Create(), key, initializationVector);

    /// <summary>
    /// Three-key triple DES (EDE) in CBC mode, <c>3des-cbc</c>.
    /// </summary>
    /// <param name="key">The key: 24 bytes.</param>
    /// <param name="initializationVector">The IV: 8 bytes.</param>
    /// <returns>The cipher.</returns>
    public static SshCbc TripleDes(byte[] key, byte[] initializationVector) => new(TripleDES.Create(), key, initializationVector);

    /// <inheritdoc/>
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var ciphertext = cipher.EncryptCbc(plaintext, chainingBlock, PaddingMode.None);
        Chain(ciphertext);

        return ciphertext;
    }

    /// <inheritdoc/>
    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext)
    {
        var plaintext = cipher.DecryptCbc(ciphertext, chainingBlock, PaddingMode.None);
        Chain(ciphertext);

        return plaintext;
    }

    // The next bytes chain from the last ciphertext block; no bytes leave the chain as it is.
    private void Chain(ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.Length > 0)
        {
            chainingBlock = ciphertext[^BlockSize..].ToArray();
        }
    }
}
