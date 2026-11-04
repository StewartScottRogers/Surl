namespace Surl.Protocol.Ssh;

/// <summary>
/// Why a <c>--hostcert</c> file gives no certificate to serve, and the words ADR-0051 decision 4
/// writes after <c>surl: (2) Host certificate &lt;path&gt;: </c>.
/// </summary>
/// <param name="Reason">Why.</param>
/// <param name="Text">The words after <c>Host certificate &lt;path&gt;: </c>.</param>
public sealed record SshHostCertificateRefusal(SshHostCertificateRefusalReason Reason, string Text)
{
    /// <summary><c>not an OpenSSH host certificate</c>.</summary>
    public static SshHostCertificateRefusal NotAHostCertificate { get; } =
        new(SshHostCertificateRefusalReason.NotAHostCertificate, "not an OpenSSH host certificate");

    /// <summary><c>certifies no --hostkey key</c>.</summary>
    public static SshHostCertificateRefusal CertifiesNoHostKey { get; } =
        new(SshHostCertificateRefusalReason.CertifiesNoHostKey, "certifies no --hostkey key");
}
