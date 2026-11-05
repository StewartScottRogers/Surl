namespace Surl.Dtls;

/// <summary>
/// The AEAD algorithms that protect records under ADR-0087's suites.
/// </summary>
public enum DtlsAeadAlgorithm
{
    /// <summary>AES-GCM with a 16-byte tag (RFC 5288), with a 16- or 32-byte key.</summary>
    AesGcm,

    /// <summary>AES-CCM with a 16-byte tag (RFC 6655), with a 16-byte key.</summary>
    AesCcm,

    /// <summary>AES-CCM with an 8-byte tag, the <c>_CCM_8</c> suites (RFC 6655, RFC 7251), with a 16-byte key.</summary>
    AesCcm8,
}
