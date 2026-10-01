using System.Security.Cryptography;

namespace Surl.Kerberos;

/// <summary>
/// <c>aes128-cts-hmac-sha256-128</c> and <c>aes256-cts-hmac-sha384-192</c> (RFC 8009): keys
/// derived by <c>KDF-HMAC-SHA2</c>, and the HMAC taken over the all-zero initial vector and the
/// cipher text (encrypt-then-MAC), truncated to 128 or 192 bits.
/// </summary>
internal sealed class AesCtsHmacSha2Profile : KerberosEncryptionProfile
{
    private static readonly byte[] ZeroInitialVector = new byte[AesCiphertextStealing.BlockLength];

    private readonly HashAlgorithmName _hashAlgorithm;

    /// <summary>Initialises one of the two RFC 8009 encryption types.</summary>
    /// <param name="encryptionType">Enctype 19 or 20.</param>
    /// <param name="checksumType">Checksum type 19 or 20.</param>
    /// <param name="hashAlgorithm">SHA-256 or SHA-384.</param>
    /// <param name="keyLength">16 or 32.</param>
    /// <param name="hmacLength">16 or 24.</param>
    public AesCtsHmacSha2Profile(KerberosEncryptionType encryptionType, KerberosChecksumType checksumType, HashAlgorithmName hashAlgorithm, int keyLength, int hmacLength)
        : base(encryptionType, checksumType, keyLength, hmacLength)
    {
        _hashAlgorithm = hashAlgorithm;
    }

    /// <inheritdoc />
    /// <remarks>Ke is as long as the base key; Kc and Ki are as long as the truncated HMAC (RFC 8009 section 5).</remarks>
    protected override byte[] DeriveKeyFromConstant(ReadOnlySpan<byte> baseKey, ReadOnlySpan<byte> constant, KerberosDerivedKeyPurpose purpose) =>
        KdfHmacSha2.Derive(_hashAlgorithm, baseKey, constant, purpose == KerberosDerivedKeyPurpose.Encryption ? KeyLength : HmacLength);

    /// <inheritdoc />
    protected override byte[] ComputeIntegrityHmac(byte[] integrityKey, ReadOnlySpan<byte> confoundedPlainText, ReadOnlySpan<byte> enciphered) =>
        ComputeTruncatedHmac(integrityKey, [.. ZeroInitialVector, .. enciphered]);

    /// <inheritdoc />
    protected override byte[] ComputeTruncatedHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message) =>
        CryptographicOperations.HmacData(_hashAlgorithm, key, message)[..HmacLength];
}
