namespace Surl.Protocol.Ssh;

/// <summary>
/// Why a <c>--hostcert</c> file gives no certificate to serve (ADR-0051, decision 4's refusals).
/// </summary>
public enum SshHostCertificateRefusalReason
{
    /// <summary>
    /// Not one line holding an OpenSSH host certificate surl reads: not a certificate, a user
    /// certificate, a certificate type surl does not read, or a malformed one.
    /// </summary>
    NotAHostCertificate,

    /// <summary>The certificate's key is none of the <c>--hostkey</c> keys.</summary>
    CertifiesNoHostKey,
}
