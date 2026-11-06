namespace Surl.Networking;

/// <summary>
/// The format of the <c>--cert</c> file, named by <c>--cert-type</c> (ADR-0010, section 3).
/// </summary>
public enum ServerCertificateFormat
{
    /// <summary>
    /// <c>PEM</c>: one or more <c>CERTIFICATE</c> blocks, the server's first, optionally with its key.
    /// </summary>
    Pem = 0,

    /// <summary>
    /// <c>DER</c>: exactly one certificate; the key comes from <c>--key</c>.
    /// </summary>
    Der,

    /// <summary>
    /// <c>P12</c>: a PKCS#12 file holding the certificate, its key and optionally intermediates.
    /// </summary>
    P12,
}
