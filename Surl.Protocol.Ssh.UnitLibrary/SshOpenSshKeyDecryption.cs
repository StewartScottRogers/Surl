using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography.BcryptPbkdf;
using Surl.Cryptography.ChaCha20;
using Surl.Cryptography.Poly1305;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Decrypts the private section of an encrypted <c>openssh-key-v1</c> key (OpenSSH's
/// <c>PROTOCOL.key</c> and <c>sshkey.c</c>'s <c>private2_decrypt</c>): the KDF is <c>bcrypt</c>,
/// whose options are the salt and the round count, and one <c>bcrypt_pbkdf</c> output of the
/// cipher's key length plus its IV length is the key, then the IV. The section is encrypted as a
/// whole, a whole number of the cipher's blocks long, and an AEAD cipher's tag follows it.
/// </summary>
/// <remarks>
/// The ciphers read are those <c>ssh-keygen -Z</c> writes: <c>aes128-ctr</c>, <c>aes192-ctr</c>,
/// <c>aes256-ctr</c>, <c>aes128-gcm@openssh.com</c>, <c>aes256-gcm@openssh.com</c> and
/// <c>chacha20-poly1305@openssh.com</c>, and the CBC and <c>arcfour</c> ciphers the transport
/// builds for <c>--allow-weak-ssh-algorithms</c>, which older OpenSSH releases also wrote. An
/// AEAD tag that does not verify is a wrong passphrase, as it is to OpenSSH; for the other
/// ciphers the section's two check integers show it (<see cref="SshOpenSshKeyDecoder"/>).
/// </remarks>
internal static class SshOpenSshKeyDecryption
{
    /// <summary>The only KDF an encrypted key is read with.</summary>
    public const string BcryptKdf = "bcrypt";

    // OpenSSH's cipher.c gives a stream cipher (arcfour) a block size of 8.
    private const int StreamCipherBlockSize = 8;

    private const int GcmNonceLength = 12;

    private const int GcmTagLength = 16;

    private static readonly Dictionary<string, KeyCipher> AeadCiphers = new(StringComparer.Ordinal)
    {
        ["aes128-gcm@openssh.com"] = new(16, GcmNonceLength, 16, GcmTagLength, OpenAesGcm),
        ["aes256-gcm@openssh.com"] = new(32, GcmNonceLength, 16, GcmTagLength, OpenAesGcm),
        [SshChaCha20Poly1305Protection.Name] = new(SshChaCha20Poly1305Protection.KeyMaterialLength, 0, 8, Poly1305.TagSize, OpenChaCha20Poly1305),
    };

    // Opens an encrypted section: the key, the IV, the ciphertext and the tag, to the
    // plaintext, or null when the tag does not verify.
    private delegate byte[]? SectionOpener(byte[] key, byte[] iv, ReadOnlyMemory<byte> ciphertext, ReadOnlyMemory<byte> tag);

    /// <summary>
    /// Reads the encrypted section and the tag after it, and decrypts the section.
    /// </summary>
    /// <param name="reader">The key's reader, just past its public keys.</param>
    /// <param name="cipherName">The key's <c>ciphername</c>.</param>
    /// <param name="kdfOptions">The key's <c>kdfoptions</c>: the salt and the round count.</param>
    /// <param name="passphrase">The <c>--pass</c> value, UTF-8 encoded for <c>bcrypt_pbkdf</c>; <see langword="null"/> when not given.</param>
    /// <returns>The decrypted private section.</returns>
    /// <exception cref="SshHostKeyRefusedException">
    /// The cipher is not one read, no passphrase was given, the key is not well formed, or the
    /// passphrase does not open an AEAD cipher's section.
    /// </exception>
    /// <exception cref="SshDisconnectRequiredException">A field runs past the key's end.</exception>
    public static byte[] DecryptSection(SshWireReader reader, string cipherName, ReadOnlyMemory<byte> kdfOptions, string? passphrase)
    {
        var cipher = ForName(cipherName) ?? throw NotAPrivateKey();
        if (passphrase is null)
        {
            throw new SshHostKeyRefusedException(SshHostKeyRefusal.EncryptedWithoutPassphrase);
        }

        var (salt, rounds) = ReadKdfOptions(kdfOptions);
        var ciphertext = reader.ReadString();
        var tag = reader.ReadBytes(cipher.TagLength);
        if (ciphertext.IsEmpty || ciphertext.Length % cipher.BlockSize != 0)
        {
            throw NotAPrivateKey();
        }

        var keyAndIv = DeriveKeyAndIv(passphrase, salt.Span, rounds, cipher.KeyLength + cipher.IvLength);

        return cipher.Open(keyAndIv[..cipher.KeyLength], keyAndIv[cipher.KeyLength..], ciphertext, tag)
            ?? throw new SshHostKeyRefusedException(SshHostKeyRefusal.PassphraseDoesNotDecrypt);
    }

    // The salt, 1 to bcrypt_pbkdf's most bytes long, then the round count, at least 1.
    private static (ReadOnlyMemory<byte> Salt, int Rounds) ReadKdfOptions(ReadOnlyMemory<byte> kdfOptions)
    {
        var options = new SshWireReader(kdfOptions);
        var salt = options.ReadString();
        var rounds = options.ReadUInt32();

        return salt.IsEmpty || salt.Length > BcryptPbkdf.MaximumSaltSize || rounds is 0 or > int.MaxValue
            ? throw NotAPrivateKey()
            : (salt, (int)rounds);
    }

    private static KeyCipher? ForName(string cipherName)
    {
        if (AeadCiphers.TryGetValue(cipherName, out var aead))
        {
            return aead;
        }

        var algorithm = SshCipherAlgorithm.ForName(cipherName);

        return algorithm is null
            ? null
            : new(
                algorithm.KeyLength,
                algorithm.InitializationVectorLength,
                Math.Max(algorithm.InitializationVectorLength, StreamCipherBlockSize),
                0,
                (key, iv, ciphertext, _) => algorithm.Create(key, iv).Decrypt(ciphertext.Span));
    }

    // OpenSSH treats an empty passphrase as no passphrase, which cannot decrypt the key;
    // bcrypt_pbkdf itself refuses an empty password.
    private static byte[] DeriveKeyAndIv(string passphrase, ReadOnlySpan<byte> salt, int rounds, int length)
    {
        var password = Encoding.UTF8.GetBytes(passphrase);
        if (password.Length == 0)
        {
            throw new SshHostKeyRefusedException(SshHostKeyRefusal.PassphraseDoesNotDecrypt);
        }

        var keyAndIv = new byte[length];
        try
        {
            BcryptPbkdf.DeriveKey(password, salt, rounds, keyAndIv);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
        }

        return keyAndIv;
    }

    // OpenSSH's cipher_crypt with no additional data: the IV is the whole 12-byte nonce.
    private static byte[]? OpenAesGcm(byte[] key, byte[] iv, ReadOnlyMemory<byte> ciphertext, ReadOnlyMemory<byte> tag)
    {
        using var gcm = new AesGcm(key, GcmTagLength);
        var plaintext = new byte[ciphertext.Length];
        try
        {
            gcm.Decrypt(iv, ciphertext.Span, tag.Span, plaintext);
        }
        catch (AuthenticationTagMismatchException)
        {
            return null;
        }

        return plaintext;
    }

    // OpenSSH's chachapoly_crypt at sequence number 0 with no length field: the first 32 bytes
    // key the payload, the Poly1305 key is their block 0, the tag covers the ciphertext alone,
    // and the section is decrypted from block 1. The second 32 bytes, the length key, go unused.
    private static byte[]? OpenChaCha20Poly1305(byte[] key, byte[] iv, ReadOnlyMemory<byte> ciphertext, ReadOnlyMemory<byte> tag)
    {
        var payloadKey = key.AsSpan(0, ChaCha20.KeySize);
        Span<byte> nonce = stackalloc byte[ChaCha20.OriginalNonceSize];
        Span<byte> block = stackalloc byte[ChaCha20.BlockSize];
        ChaCha20.ComputeOriginalBlock(payloadKey, nonce, 0, block);
        var computedTag = Poly1305.ComputeTag(block[..Poly1305.KeySize], ciphertext.Span);
        CryptographicOperations.ZeroMemory(block);
        if (!CryptographicOperations.FixedTimeEquals(computedTag, tag.Span))
        {
            return null;
        }

        var plaintext = new byte[ciphertext.Length];
        ChaCha20.ApplyOriginalKeyStream(payloadKey, nonce, 1, ciphertext.Span, plaintext);

        return plaintext;
    }

    private static SshHostKeyRefusedException NotAPrivateKey() => new(SshHostKeyRefusal.NotAPrivateKey);

    private sealed record KeyCipher(int KeyLength, int IvLength, int BlockSize, int TagLength, SectionOpener Open);
}
