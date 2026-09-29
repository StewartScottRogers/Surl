namespace Surl.Cli;

/// <summary>
/// The format of a <c>--cert</c> or <c>--key</c> file, named on the command line with
/// <c>--cert-type</c> or <c>--key-type</c> (ADR-0010 section 3).
/// </summary>
public enum CertificateFileFormat
{
    /// <summary><c>PEM</c>: Base64 text between <c>-----BEGIN</c> and <c>-----END</c> lines. The default.</summary>
    Pem,

    /// <summary><c>DER</c>: the binary encoding of one certificate or key.</summary>
    Der,

    /// <summary><c>P12</c>: a PKCS#12 file holding the certificate and its private key; <c>--cert-type</c> only.</summary>
    Pkcs12,
}
