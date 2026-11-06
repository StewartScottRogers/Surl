namespace Surl.Tls13;

/// <summary>
/// Signature schemes of RFC 8446 section 4.2.3 that ADR-0082 decision 3 names. A client may
/// offer other values, which are kept as they came.
/// </summary>
public enum TlsSignatureScheme : ushort
{
    /// <summary>rsa_pkcs1_sha256 (0x0401), which TLS 1.3 allows only in certificates.</summary>
    RsaPkcs1Sha256 = 0x0401,

    /// <summary>ecdsa_secp256r1_sha256 (0x0403).</summary>
    EcdsaSecp256r1Sha256 = 0x0403,

    /// <summary>ecdsa_secp384r1_sha384 (0x0503).</summary>
    EcdsaSecp384r1Sha384 = 0x0503,

    /// <summary>ecdsa_secp521r1_sha512 (0x0603).</summary>
    EcdsaSecp521r1Sha512 = 0x0603,

    /// <summary>rsa_pss_rsae_sha256 (0x0804).</summary>
    RsaPssRsaeSha256 = 0x0804,

    /// <summary>rsa_pss_rsae_sha384 (0x0805).</summary>
    RsaPssRsaeSha384 = 0x0805,

    /// <summary>rsa_pss_rsae_sha512 (0x0806).</summary>
    RsaPssRsaeSha512 = 0x0806,

    /// <summary>ed25519 (0x0807).</summary>
    Ed25519 = 0x0807,
}
