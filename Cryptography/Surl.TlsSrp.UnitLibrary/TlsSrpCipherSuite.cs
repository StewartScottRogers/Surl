namespace Surl.TlsSrp;

/// <summary>
/// The nine cipher suites of RFC 5054 section 2.7, by their two-byte code (ADR-0122 decision 2).
/// </summary>
public enum TlsSrpCipherSuite : ushort
{
    /// <summary><c>TLS_SRP_SHA_WITH_3DES_EDE_CBC_SHA</c> (<c>C0 1A</c>).</summary>
    SrpSha3DesEdeCbcSha = 0xC01A,

    /// <summary><c>TLS_SRP_SHA_RSA_WITH_3DES_EDE_CBC_SHA</c> (<c>C0 1B</c>).</summary>
    SrpShaRsa3DesEdeCbcSha = 0xC01B,

    /// <summary><c>TLS_SRP_SHA_DSS_WITH_3DES_EDE_CBC_SHA</c> (<c>C0 1C</c>).</summary>
    SrpShaDss3DesEdeCbcSha = 0xC01C,

    /// <summary><c>TLS_SRP_SHA_WITH_AES_128_CBC_SHA</c> (<c>C0 1D</c>).</summary>
    SrpShaAes128CbcSha = 0xC01D,

    /// <summary><c>TLS_SRP_SHA_RSA_WITH_AES_128_CBC_SHA</c> (<c>C0 1E</c>).</summary>
    SrpShaRsaAes128CbcSha = 0xC01E,

    /// <summary><c>TLS_SRP_SHA_DSS_WITH_AES_128_CBC_SHA</c> (<c>C0 1F</c>).</summary>
    SrpShaDssAes128CbcSha = 0xC01F,

    /// <summary><c>TLS_SRP_SHA_WITH_AES_256_CBC_SHA</c> (<c>C0 20</c>).</summary>
    SrpShaAes256CbcSha = 0xC020,

    /// <summary><c>TLS_SRP_SHA_RSA_WITH_AES_256_CBC_SHA</c> (<c>C0 21</c>).</summary>
    SrpShaRsaAes256CbcSha = 0xC021,

    /// <summary><c>TLS_SRP_SHA_DSS_WITH_AES_256_CBC_SHA</c> (<c>C0 22</c>).</summary>
    SrpShaDssAes256CbcSha = 0xC022,
}
