namespace Surl.Dtls;

/// <summary>
/// The kind of key a server certificate holds, which decides the live certificate suites
/// (ADR-0087 decision 2).
/// </summary>
public enum DtlsCertificateKeyAlgorithm
{
    /// <summary>An ECDSA key on P-256 or P-384: the <c>ECDHE_ECDSA</c> suites.</summary>
    Ecdsa,

    /// <summary>An RSA key of 2048 bits or more: the <c>ECDHE_RSA</c> suites.</summary>
    Rsa,
}
