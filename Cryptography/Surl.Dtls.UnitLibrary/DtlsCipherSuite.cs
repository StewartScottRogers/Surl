using System.Security.Cryptography;

namespace Surl.Dtls;

/// <summary>
/// One cipher suite the server serves (ADR-0087 decision 2): its code, its AEAD, its key length,
/// its PRF hash and the certificate key it needs, or none for a pre-shared-key suite.
/// </summary>
/// <param name="Code">The suite's number on the wire.</param>
/// <param name="Aead">The record protection.</param>
/// <param name="KeyLength">The write key's length in bytes: 16 or 32.</param>
/// <param name="PrfHash">The PRF's and the handshake hash's algorithm.</param>
/// <param name="KeyAlgorithm">
/// The certificate key the suite needs, or <see langword="null"/> for a PSK suite (RFC 4279), which
/// needs an identity the caller's <see cref="DtlsPreSharedKeyLookup"/> knows instead.
/// </param>
public sealed record DtlsCipherSuite(
    ushort Code, DtlsAeadAlgorithm Aead, int KeyLength, HashAlgorithmName PrfHash, DtlsCertificateKeyAlgorithm? KeyAlgorithm)
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
    /// The pre-shared-key suites, rows 7 to 9 of ADR-0087 decision 2, in the server's order:
    /// <c>TLS_PSK_WITH_AES_128_GCM_SHA256</c> (RFC 5487), <c>TLS_PSK_WITH_AES_128_CCM_8</c>, which
    /// RFC 7252 section 9.1.3.1 mandates, and <c>TLS_PSK_WITH_AES_128_CCM</c> (RFC 6655).
    /// </summary>
    public static IReadOnlyList<DtlsCipherSuite> PreSharedKeySuitesInServerOrder { get; } =
    [
        new(0x00A8, DtlsAeadAlgorithm.AesGcm, 16, HashAlgorithmName.SHA256, null),
        new(0xC0A8, DtlsAeadAlgorithm.AesCcm8, 16, HashAlgorithmName.SHA256, null),
        new(0xC0A4, DtlsAeadAlgorithm.AesCcm, 16, HashAlgorithmName.SHA256, null),
    ];

    /// <summary>Every suite, rows 1 to 9 of ADR-0087 decision 2, in the server's order.</summary>
    public static IReadOnlyList<DtlsCipherSuite> AllSuitesInServerOrder { get; } =
        [.. CertificateSuitesInServerOrder, .. PreSharedKeySuitesInServerOrder];

    /// <summary>Whether the suite keys with a pre-shared key (RFC 4279) rather than ECDHE under a certificate.</summary>
    public bool UsesPreSharedKey => KeyAlgorithm is null;

    /// <summary>
    /// The key block's length: both write keys, then both 4-byte implicit salts (RFC 5246 section
    /// 6.3; AEAD suites have no MAC keys).
    /// </summary>
    public int KeyBlockLength => 2 * (KeyLength + DtlsRecordProtection.SaltSize);
}
