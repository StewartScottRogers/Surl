namespace Surl.TlsSrp;

/// <summary>
/// The <c>SignatureScheme</c> code points (RFC 5246 section 7.4.1.4.1, RFC 8446 section 4.2.3) the
/// TLS-SRP server can sign a ServerKeyExchange with (ADR-0122 decision 3).
/// </summary>
public enum TlsSignatureScheme : ushort
{
    /// <summary><c>rsa_pkcs1_sha1</c> (<c>02 01</c>): what an RSA key signs with when the client sends no <c>signature_algorithms</c>.</summary>
    RsaPkcs1Sha1 = 0x0201,

    /// <summary><c>dsa_sha1</c> (<c>02 02</c>): what a DSA key signs with when the client sends no <c>signature_algorithms</c>.</summary>
    DsaSha1 = 0x0202,

    /// <summary><c>rsa_pkcs1_sha256</c> (<c>04 01</c>).</summary>
    RsaPkcs1Sha256 = 0x0401,

    /// <summary><c>dsa_sha256</c> (<c>04 02</c>).</summary>
    DsaSha256 = 0x0402,

    /// <summary><c>rsa_pkcs1_sha384</c> (<c>05 01</c>).</summary>
    RsaPkcs1Sha384 = 0x0501,

    /// <summary><c>dsa_sha384</c> (<c>05 02</c>).</summary>
    DsaSha384 = 0x0502,

    /// <summary><c>rsa_pss_rsae_sha256</c> (<c>08 04</c>).</summary>
    RsaPssRsaeSha256 = 0x0804,

    /// <summary><c>rsa_pss_rsae_sha384</c> (<c>08 05</c>).</summary>
    RsaPssRsaeSha384 = 0x0805,
}
