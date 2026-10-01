using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Surl.Kerberos;

/// <summary>
/// One accepted Kerberos encryption type: how it derives keys from a base key and a key usage
/// (RFC 3961 section 3), encrypts and decrypts with a confounder and an integrity HMAC, and
/// computes the keyed checksum of its checksum type.
/// </summary>
/// <remarks>
/// Every operation uses the default cipher state, an all-zero initial vector, which is the only
/// state RFC 4120 messages and RFC 4121 tokens use.
/// </remarks>
internal abstract class KerberosEncryptionProfile
{
    /// <summary>The length of the random confounder encrypted ahead of every message, in bytes.</summary>
    public const int ConfounderLength = AesCiphertextStealing.BlockLength;

    private static readonly KerberosEncryptionProfile Aes128CtsHmacSha196 =
        new AesCtsHmacSha1Profile(KerberosEncryptionType.Aes128CtsHmacSha196, KerberosChecksumType.HmacSha196Aes128, keyLength: 16);

    private static readonly KerberosEncryptionProfile Aes256CtsHmacSha196 =
        new AesCtsHmacSha1Profile(KerberosEncryptionType.Aes256CtsHmacSha196, KerberosChecksumType.HmacSha196Aes256, keyLength: 32);

    private static readonly KerberosEncryptionProfile Aes128CtsHmacSha256128 =
        new AesCtsHmacSha2Profile(KerberosEncryptionType.Aes128CtsHmacSha256128, KerberosChecksumType.HmacSha256128Aes128, HashAlgorithmName.SHA256, keyLength: 16, hmacLength: 16);

    private static readonly KerberosEncryptionProfile Aes256CtsHmacSha384192 =
        new AesCtsHmacSha2Profile(KerberosEncryptionType.Aes256CtsHmacSha384192, KerberosChecksumType.HmacSha384192Aes256, HashAlgorithmName.SHA384, keyLength: 32, hmacLength: 24);

    /// <summary>Initialises the parameters every profile shares.</summary>
    /// <param name="encryptionType">The encryption type this profile implements.</param>
    /// <param name="checksumType">The checksum type the encryption type brings.</param>
    /// <param name="keyLength">The length of a base key, and of the encryption key Ke, in bytes.</param>
    /// <param name="hmacLength">The length of the truncated integrity HMAC and of the checksum, in bytes.</param>
    protected KerberosEncryptionProfile(KerberosEncryptionType encryptionType, KerberosChecksumType checksumType, int keyLength, int hmacLength)
    {
        EncryptionType = encryptionType;
        ChecksumType = checksumType;
        KeyLength = keyLength;
        HmacLength = hmacLength;
    }

    /// <summary>Gets the encryption type this profile implements.</summary>
    public KerberosEncryptionType EncryptionType { get; }

    /// <summary>Gets the checksum type the encryption type brings.</summary>
    public KerberosChecksumType ChecksumType { get; }

    /// <summary>Gets the length of a base key, in bytes.</summary>
    public int KeyLength { get; }

    /// <summary>Gets the length of the truncated integrity HMAC, and of a checksum, in bytes.</summary>
    public int HmacLength { get; }

    /// <summary>Gets the profile of <paramref name="encryptionType" />.</summary>
    /// <param name="encryptionType">One of the four accepted encryption types.</param>
    /// <returns>Its profile.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="encryptionType" /> is not one of the four.</exception>
    public static KerberosEncryptionProfile For(KerberosEncryptionType encryptionType) => encryptionType switch
    {
        KerberosEncryptionType.Aes128CtsHmacSha196 => Aes128CtsHmacSha196,
        KerberosEncryptionType.Aes256CtsHmacSha196 => Aes256CtsHmacSha196,
        KerberosEncryptionType.Aes128CtsHmacSha256128 => Aes128CtsHmacSha256128,
        KerberosEncryptionType.Aes256CtsHmacSha384192 => Aes256CtsHmacSha384192,
        _ => throw new ArgumentOutOfRangeException(nameof(encryptionType), encryptionType, "Not an accepted Kerberos encryption type."),
    };

    /// <summary>Derives the key <paramref name="purpose" /> names for <paramref name="keyUsage" /> from <paramref name="baseKey" />.</summary>
    /// <param name="baseKey">The protocol key, <see cref="KeyLength" /> bytes.</param>
    /// <param name="keyUsage">The RFC 4120 section 7.5.1 key usage number.</param>
    /// <param name="purpose">Which of Kc, Ke and Ki to derive.</param>
    /// <returns>The derived key.</returns>
    public byte[] DeriveKey(ReadOnlySpan<byte> baseKey, int keyUsage, KerberosDerivedKeyPurpose purpose)
    {
        Span<byte> constant = stackalloc byte[5];
        BinaryPrimitives.WriteInt32BigEndian(constant, keyUsage);
        constant[4] = (byte)purpose;
        return DeriveKeyFromConstant(baseKey, constant, purpose);
    }

    /// <summary>
    /// Encrypts <paramref name="plainText" /> for <paramref name="keyUsage" />: the confounder and
    /// the plain text enciphered under Ke, then the truncated integrity HMAC under Ki.
    /// </summary>
    /// <param name="baseKey">The protocol key, <see cref="KeyLength" /> bytes.</param>
    /// <param name="keyUsage">The RFC 4120 section 7.5.1 key usage number.</param>
    /// <param name="confounder">Exactly <see cref="ConfounderLength" /> random bytes.</param>
    /// <param name="plainText">The message.</param>
    /// <returns>The cipher text: the enciphered bytes, then the HMAC.</returns>
    /// <exception cref="ArgumentException"><paramref name="confounder" /> is not <see cref="ConfounderLength" /> bytes.</exception>
    public byte[] Encrypt(ReadOnlySpan<byte> baseKey, int keyUsage, ReadOnlySpan<byte> confounder, ReadOnlySpan<byte> plainText)
    {
        if (confounder.Length != ConfounderLength)
        {
            throw new ArgumentException($"A Kerberos confounder is {ConfounderLength} bytes; got {confounder.Length}.", nameof(confounder));
        }

        byte[] confoundedPlainText = [.. confounder, .. plainText];
        byte[] enciphered = AesCiphertextStealing.Encrypt(DeriveKey(baseKey, keyUsage, KerberosDerivedKeyPurpose.Encryption), confoundedPlainText);
        byte[] hmac = ComputeIntegrityHmac(DeriveKey(baseKey, keyUsage, KerberosDerivedKeyPurpose.Integrity), confoundedPlainText, enciphered);
        return [.. enciphered, .. hmac];
    }

    /// <summary>
    /// Decrypts <paramref name="cipherText" /> for <paramref name="keyUsage" /> and checks its
    /// integrity HMAC in constant time. A cipher text that is too short or fails the check is
    /// refused, never thrown on.
    /// </summary>
    /// <param name="baseKey">The protocol key, <see cref="KeyLength" /> bytes.</param>
    /// <param name="keyUsage">The RFC 4120 section 7.5.1 key usage number.</param>
    /// <param name="cipherText">The cipher text, as <see cref="Encrypt" /> lays it out.</param>
    /// <param name="plainText">The message without its confounder, or empty when refused.</param>
    /// <returns><see langword="true" /> when the cipher text is intact.</returns>
    public bool TryDecrypt(ReadOnlySpan<byte> baseKey, int keyUsage, ReadOnlySpan<byte> cipherText, out byte[] plainText)
    {
        plainText = [];
        if (cipherText.Length < ConfounderLength + HmacLength)
        {
            return false;
        }

        ReadOnlySpan<byte> enciphered = cipherText[..^HmacLength];
        byte[] confoundedPlainText = AesCiphertextStealing.Decrypt(DeriveKey(baseKey, keyUsage, KerberosDerivedKeyPurpose.Encryption), enciphered);
        byte[] expectedHmac = ComputeIntegrityHmac(DeriveKey(baseKey, keyUsage, KerberosDerivedKeyPurpose.Integrity), confoundedPlainText, enciphered);
        if (!CryptographicOperations.FixedTimeEquals(expectedHmac, cipherText[^HmacLength..]))
        {
            return false;
        }

        plainText = confoundedPlainText[ConfounderLength..];
        return true;
    }

    /// <summary>Computes the keyed checksum of <paramref name="message" /> for <paramref name="keyUsage" />, under Kc.</summary>
    /// <param name="baseKey">The protocol key, <see cref="KeyLength" /> bytes.</param>
    /// <param name="keyUsage">The RFC 4120 section 7.5.1 key usage number.</param>
    /// <param name="message">The bytes to checksum.</param>
    /// <returns>The checksum, <see cref="HmacLength" /> bytes.</returns>
    public byte[] ComputeChecksum(ReadOnlySpan<byte> baseKey, int keyUsage, ReadOnlySpan<byte> message) =>
        ComputeTruncatedHmac(DeriveKey(baseKey, keyUsage, KerberosDerivedKeyPurpose.Checksum), message);

    /// <summary>Checks <paramref name="checksum" /> against <paramref name="message" /> in constant time.</summary>
    /// <param name="baseKey">The protocol key, <see cref="KeyLength" /> bytes.</param>
    /// <param name="keyUsage">The RFC 4120 section 7.5.1 key usage number.</param>
    /// <param name="message">The bytes the checksum covers.</param>
    /// <param name="checksum">The checksum received.</param>
    /// <returns><see langword="true" /> when the checksum matches.</returns>
    public bool VerifyChecksum(ReadOnlySpan<byte> baseKey, int keyUsage, ReadOnlySpan<byte> message, ReadOnlySpan<byte> checksum) =>
        CryptographicOperations.FixedTimeEquals(ComputeChecksum(baseKey, keyUsage, message), checksum);

    /// <summary>Derives a key from <paramref name="baseKey" /> and a five-byte derivation constant.</summary>
    /// <param name="baseKey">The protocol key.</param>
    /// <param name="constant">The key usage, big-endian, then the purpose octet.</param>
    /// <param name="purpose">Which of Kc, Ke and Ki is derived, for profiles whose key lengths differ by purpose.</param>
    /// <returns>The derived key.</returns>
    protected abstract byte[] DeriveKeyFromConstant(ReadOnlySpan<byte> baseKey, ReadOnlySpan<byte> constant, KerberosDerivedKeyPurpose purpose);

    /// <summary>Computes the truncated integrity HMAC of one encryption.</summary>
    /// <param name="integrityKey">Ki.</param>
    /// <param name="confoundedPlainText">The confounder and the plain text.</param>
    /// <param name="enciphered">The enciphered confounder and plain text.</param>
    /// <returns>The HMAC, <see cref="HmacLength" /> bytes.</returns>
    protected abstract byte[] ComputeIntegrityHmac(byte[] integrityKey, ReadOnlySpan<byte> confoundedPlainText, ReadOnlySpan<byte> enciphered);

    /// <summary>Computes the profile's HMAC of <paramref name="message" />, truncated to <see cref="HmacLength" /> bytes.</summary>
    /// <param name="key">The HMAC key.</param>
    /// <param name="message">The bytes to authenticate.</param>
    /// <returns>The truncated HMAC.</returns>
    protected abstract byte[] ComputeTruncatedHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message);
}
