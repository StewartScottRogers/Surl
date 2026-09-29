namespace Surl.Networking;

/// <summary>
/// The format of the <c>--key</c> file, named by <c>--key-type</c> (ADR-0010, section 3).
/// </summary>
public enum ServerKeyFormat
{
    /// <summary>
    /// <c>PEM</c>: a <c>PRIVATE KEY</c>, <c>ENCRYPTED PRIVATE KEY</c>, <c>RSA PRIVATE KEY</c> or
    /// <c>EC PRIVATE KEY</c> block.
    /// </summary>
    Pem = 0,

    /// <summary>
    /// <c>DER</c>: a PKCS#8 or encrypted PKCS#8 key.
    /// </summary>
    Der,
}
