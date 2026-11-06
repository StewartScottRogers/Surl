using System.Security.Cryptography;

namespace Surl.Kerberos;

/// <summary>
/// <c>aes128-cts-hmac-sha1-96</c> and <c>aes256-cts-hmac-sha1-96</c> (RFC 3962): RFC 3961's
/// simplified profile over AES, with keys derived by <c>DK</c>, and HMAC-SHA1 over the confounded
/// plain text truncated to 96 bits for both integrity and the checksum.
/// </summary>
internal sealed class AesCtsHmacSha1Profile : KerberosEncryptionProfile
{
    private const int Sha1HmacLength = 12;

    /// <summary>Initialises one of the two RFC 3962 encryption types.</summary>
    /// <param name="encryptionType">Enctype 17 or 18.</param>
    /// <param name="checksumType">Checksum type 15 or 16.</param>
    /// <param name="keyLength">16 or 32.</param>
    public AesCtsHmacSha1Profile(KerberosEncryptionType encryptionType, KerberosChecksumType checksumType, int keyLength)
        : base(encryptionType, checksumType, keyLength, Sha1HmacLength)
    {
    }

    /// <inheritdoc />
    protected override byte[] DeriveKeyFromConstant(ReadOnlySpan<byte> baseKey, ReadOnlySpan<byte> constant, KerberosDerivedKeyPurpose purpose) =>
        SimplifiedProfileKeyDerivation.DeriveKey(baseKey, constant);

    /// <inheritdoc />
    protected override byte[] ComputeIntegrityHmac(byte[] integrityKey, ReadOnlySpan<byte> confoundedPlainText, ReadOnlySpan<byte> enciphered) =>
        ComputeTruncatedHmac(integrityKey, confoundedPlainText);

    /// <inheritdoc />
    protected override byte[] ComputeTruncatedHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message) =>
        HMACSHA1.HashData(key, message)[..Sha1HmacLength];
}
