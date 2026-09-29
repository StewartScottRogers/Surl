namespace Surl.Networking;

/// <summary>
/// Why a TLS option file could not be loaded (ADR-0010, section 3). <c>Surl.Networking</c>
/// never picks an exit code (ADR-0004, section 6); the composition root maps each value to one.
/// </summary>
public enum TlsFileLoadFailure
{
    /// <summary>
    /// <c>--cert</c> or <c>--key</c> is missing, unreadable or not in the named format, the key
    /// does not match the certificate, the key needs a <c>--pass</c> it was not given or was
    /// given a wrong one, or the key is of a type Surl cannot serve. Maps to <c>CertificateProblem</c> (58).
    /// </summary>
    ServerCertificateUnusable = 0,

    /// <summary>
    /// The <c>--cacert</c> file does not exist. Maps to <c>FailedInit</c> (2), as upstream curl answers it.
    /// </summary>
    CaCertificateNotFound,

    /// <summary>
    /// The <c>--cacert</c> file exists but holds no certificate Surl can read. Maps to
    /// <c>CaCertificateBadFile</c> (77).
    /// </summary>
    CaCertificateUnreadable,
}
