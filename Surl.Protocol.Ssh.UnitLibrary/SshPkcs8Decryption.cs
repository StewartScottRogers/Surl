using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Decrypts a PKCS #8 <c>EncryptedPrivateKeyInfo</c> (<c>ENCRYPTED PRIVATE KEY</c>, RFC 5958
/// section 3) under PBES2 (RFC 8018, section 6.2) with PBKDF2 over HMAC-SHA-1, -256, -384 or
/// -512 and AES-128, -192 or -256 in CBC mode - what OpenSSL and <c>ssh-keygen -m PKCS8</c>
/// write - from the BCL's <see cref="Rfc2898DeriveBytes.Pbkdf2(byte[], byte[], int, HashAlgorithmName, int)"/>
/// and <see cref="Aes"/>. It is done here rather than by an <c>ImportEncryptedPkcs8PrivateKey</c>,
/// which needs the key's algorithm known before it is decrypted, so a DSA or Ed25519 key is
/// refused for its type and not for its passphrase.
/// </summary>
internal static class SshPkcs8Decryption
{
    private const string Pbes2Oid = "1.2.840.113549.1.5.13";

    private const string Pbkdf2Oid = "1.2.840.113549.1.5.12";

    // AES-CBC's IV is one block.
    private const int AesBlockBytes = 16;

    /// <summary>
    /// Decrypts <paramref name="der"/> to the DER <c>PrivateKeyInfo</c> it holds.
    /// </summary>
    /// <param name="der">The <c>EncryptedPrivateKeyInfo</c>.</param>
    /// <param name="passphrase">The <c>--pass</c> value, UTF-8 encoded for PBKDF2; <see langword="null"/> when not given.</param>
    /// <returns>The <c>PrivateKeyInfo</c>.</returns>
    /// <exception cref="SshHostKeyRefusedException">
    /// No passphrase was given, the scheme is not one this reads or its IV is not one block
    /// (not a private key surl can read), or the passphrase does not decrypt the key to a DER
    /// structure.
    /// </exception>
    /// <exception cref="AsnContentException">The structure is malformed.</exception>
    public static byte[] Decrypt(byte[] der, string? passphrase)
    {
        var info = new AsnReader(der, AsnEncodingRules.DER).ReadSequence();
        var algorithm = info.ReadSequence();
        RequireOid(algorithm.ReadObjectIdentifier(), Pbes2Oid);
        var parameters = algorithm.ReadSequence();
        var encrypted = info.ReadOctetString();

        var derivation = parameters.ReadSequence();
        RequireOid(derivation.ReadObjectIdentifier(), Pbkdf2Oid);
        var (salt, iterations, prf) = ReadPbkdf2Parameters(derivation.ReadSequence());
        var encryption = parameters.ReadSequence();
        var keyLength = AesKeyLength(encryption.ReadObjectIdentifier());
        var iv = encryption.ReadOctetString();
        if (iv.Length != AesBlockBytes)
        {
            throw NotAPrivateKey();
        }

        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase ?? throw new SshHostKeyRefusedException(SshHostKeyRefusal.EncryptedWithoutPassphrase)),
            salt,
            iterations,
            prf,
            keyLength);

        return DecryptToDer(key, iv, encrypted);
    }

    // PBKDF2-params: salt, iterationCount, an optional keyLength (the cipher decides it
    // here), and an optional PRF that defaults to HMAC-SHA-1.
    private static (byte[] Salt, int Iterations, HashAlgorithmName Prf) ReadPbkdf2Parameters(AsnReader parameters)
    {
        var salt = parameters.ReadOctetString();
        if (!parameters.TryReadInt32(out var iterations) || iterations < 1)
        {
            throw NotAPrivateKey();
        }

        if (parameters.HasData && parameters.PeekTag().HasSameClassAndValue(Asn1Tag.Integer))
        {
            parameters.ReadInteger();
        }

        var prf = parameters.HasData ? Prf(parameters.ReadSequence().ReadObjectIdentifier()) : HashAlgorithmName.SHA1;

        return (salt, iterations, prf);
    }

    // A wrong passphrase shows as bad padding, or, one time in about 256, as padding that
    // happens to be right around bytes that are not a DER structure.
    private static byte[] DecryptToDer(byte[] key, byte[] iv, byte[] encrypted)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = key;
            var plaintext = aes.DecryptCbc(encrypted, iv);
            var reader = new AsnReader(plaintext, AsnEncodingRules.DER);
            reader.ReadSequence();
            reader.ThrowIfNotEmpty();

            return plaintext;
        }
        catch (Exception wrong) when (wrong is CryptographicException or AsnContentException)
        {
            throw new SshHostKeyRefusedException(SshHostKeyRefusal.PassphraseDoesNotDecrypt);
        }
    }

    private static HashAlgorithmName Prf(string oid) => oid switch
    {
        "1.2.840.113549.2.7" => HashAlgorithmName.SHA1,
        "1.2.840.113549.2.9" => HashAlgorithmName.SHA256,
        "1.2.840.113549.2.10" => HashAlgorithmName.SHA384,
        "1.2.840.113549.2.11" => HashAlgorithmName.SHA512,
        _ => throw NotAPrivateKey(),
    };

    private static int AesKeyLength(string oid) => oid switch
    {
        "2.16.840.1.101.3.4.1.2" => 16,
        "2.16.840.1.101.3.4.1.22" => 24,
        "2.16.840.1.101.3.4.1.42" => 32,
        _ => throw NotAPrivateKey(),
    };

    private static void RequireOid(string oid, string expected)
    {
        if (oid != expected)
        {
            throw NotAPrivateKey();
        }
    }

    private static SshHostKeyRefusedException NotAPrivateKey() => new(SshHostKeyRefusal.NotAPrivateKey);
}
