namespace Surl.Dtls;

/// <summary>
/// The signature schemes the server signs <c>ServerKeyExchange</c> with (ADR-0087 decision 5;
/// RFC 5246 section 7.4.1.4.1, and RFC 8446 section 4.2.3 for the PSS code points). SHA-1 is never used.
/// </summary>
public enum DtlsSignatureScheme : ushort
{
    /// <summary><c>rsa_pkcs1_sha256</c> (0x0401).</summary>
    RsaPkcs1Sha256 = 0x0401,

    /// <summary><c>ecdsa_secp256r1_sha256</c> (0x0403).</summary>
    EcdsaSecp256r1Sha256 = 0x0403,

    /// <summary><c>rsa_pkcs1_sha384</c> (0x0501).</summary>
    RsaPkcs1Sha384 = 0x0501,

    /// <summary><c>ecdsa_secp384r1_sha384</c> (0x0503).</summary>
    EcdsaSecp384r1Sha384 = 0x0503,

    /// <summary><c>rsa_pss_rsae_sha256</c> (0x0804).</summary>
    RsaPssRsaeSha256 = 0x0804,

    /// <summary><c>rsa_pss_rsae_sha384</c> (0x0805).</summary>
    RsaPssRsaeSha384 = 0x0805,
}
