using System.Security.Cryptography;

namespace Surl.Dtls;

/// <summary>
/// One cipher suite the server serves (ADR-0087 decision 2): its code, its AEAD, its key length,
/// its PRF hash and the certificate key it needs.
/// </summary>
/// <param name="Code">The suite's number on the wire.</param>
/// <param name="Aead">The record protection.</param>
/// <param name="KeyLength">The write key's length in bytes: 16 or 32.</param>
/// <param name="PrfHash">The PRF's and the handshake hash's algorithm.</param>
/// <param name="KeyAlgorithm">The certificate key the suite needs.</param>
public sealed record DtlsCipherSuite(
    ushort Code, DtlsAeadAlgorithm Aead, int KeyLength, HashAlgorithmName PrfHash, DtlsCertificateKeyAlgorithm KeyAlgorithm)
{
    /// <summary>
    /// The certificate suites, rows 1 to 6 of ADR-0087 decision 2, in the server's order.
    /// </summary>
    public static IReadOnlyList<DtlsCipherSuite> CertificateSuitesInServerOrder { get; } =
    [
        new(0xC02B, DtlsAeadAlgorithm.AesGcm, 16, HashAlgorithmName.SHA256, DtlsCertificateKeyAlgorithm.Ecdsa),
        new(0xC02C, DtlsAeadAlgorithm.AesGcm, 32, HashAlgorithmName.SHA384, DtlsCertificateKeyAlgorithm.Ecdsa),
        new(0xC0AE, DtlsAeadAlgorithm.AesCcm8, 16, HashAlgorithmName.SHA256, DtlsCertificateKeyAlgorithm.Ecdsa),
        new(0xC0AC, DtlsAeadAlgorithm.AesCcm, 16, HashAlgorithmName.SHA256, DtlsCertificateKeyAlgorithm.Ecdsa),
        new(0xC02F, DtlsAeadAlgorithm.AesGcm, 16, HashAlgorithmName.SHA256, DtlsCertificateKeyAlgorithm.Rsa),
        new(0xC030, DtlsAeadAlgorithm.AesGcm, 32, HashAlgorithmName.SHA384, DtlsCertificateKeyAlgorithm.Rsa),
    ];

    /// <summary>
    /// The key block's length: both write keys, then both 4-byte implicit salts (RFC 5246 section
    /// 6.3; AEAD suites have no MAC keys).
    /// </summary>
    public int KeyBlockLength => 2 * (KeyLength + DtlsRecordProtection.SaltSize);
}
