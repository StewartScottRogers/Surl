namespace Surl.Conformance;

/// <summary>
/// The host keys, their OpenSSH host certificates and the certificate authority's public key the
/// <c>--hostcert</c> conformance tests serve and trust: copies of
/// <c>Surl.Protocol.Ssh.UnitTests/Fixtures/host-certificates</c>, written by <c>ssh-keygen</c> from
/// OpenSSH_10.3p1 for tests only (that folder's <c>README.md</c>, "Host certificates", says how).
/// Each certificate names the principal <c>localhost</c> and is valid from 2026-01-01 to 2036-01-01.
/// </summary>
internal static class SshTestHostCertificates
{
    /// <summary>The CA's public key, <c>ca_ed25519.pub</c>, as a <c>known_hosts</c> key: type and base64 blob.</summary>
    public const string CertificateAuthorityPublicKey =
        "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIFrEwtaN4iFKfxUjoffYqng6TIM9te63RQSFVNgrXVuW";

    /// <summary>An RSA 2048-bit host key, <c>host_rsa</c>, in <c>openssh-key-v1</c>.</summary>
    public const string RsaHostKey =
        "-----BEGIN OPENSSH PRIVATE KEY-----\n"
        + "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\n"
        + "-----END OPENSSH PRIVATE KEY-----\n";

    /// <summary>The host certificate of <see cref="RsaHostKey"/>, <c>host_rsa-cert.pub</c>.</summary>
    public const string RsaHostCertificate =
        "ssh-rsa-cert-v01@openssh.com AAAAHHNzaC1yc2EtY2VydC12MDFAb3BlbnNzaC5jb20AAAAgQnveSXm0MOtE5VrJZ2fb17kyvnxPLXLEBkHaP/UzT0wAAAADAQABAAABAQDByb+qFlOizbhNEmqb7Evp/4TFknh7fqQqQLuzwWgSexFCHnt/es2urzTv8Svg/CGoNwgcFMJAAwXEWVqQcCI3CUB5jrEwmZhGTvNM3aCkscq0rStI8N2LCuMCw5aEZ34+qUnK8bCPuzg/yM5a+AV8TOBJsbRC4tJ0xmc20MoYqoEc+3YvOp0Cnsngd9RxOsIKcptUHxr04Chesu2W00iYlkCLOx4mzL9DjkF3uxb4jlqdZIt7crJiWkH4SWOoHkGYl93CLh2APK9sh3b2wL++1uxHEmIh2GqxSAjr8xDoahbOFPBH4ShVWgM2MT6QDrKbv/WhGunj305ASGF6P4CZAAAAAAAAAAEAAAACAAAAEnN1cmwtdGVzdC1ob3N0X3JzYQAAAA0AAAAJbG9jYWxob3N0AAAAAGlWG3AAAAAAfCTBcAAAAAAAAAAAAAAAAAAAADMAAAALc3NoLWVkMjU1MTkAAAAgWsTC1o3iIUp/FSOh99iqeDpMgz217rdFBIVU2CtdW5YAAABTAAAAC3NzaC1lZDI1NTE5AAAAQEo244Eod81S5s+nO9Elh10JZutEICDBkBeWZuM5n1CtmqB+qV2s5plMb1h20ifAijsYgeumnCKMVSr9jm7pLAo= surl-test-host\n";

    /// <summary>An Ed25519 host key, <c>host_ed25519</c>, in <c>openssh-key-v1</c>.</summary>
    public const string Ed25519HostKey =
        "-----BEGIN OPENSSH PRIVATE KEY-----\n"
        + "b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW\n"
        + "QyNTUxOQAAACBD/DLLHtbymr8gkqi9SpECIpUCZCWMZ2/sXk7Ie5nEpQAAAJgXudblF7nW\n"
        + "5QAAAAtzc2gtZWQyNTUxOQAAACBD/DLLHtbymr8gkqi9SpECIpUCZCWMZ2/sXk7Ie5nEpQ\n"
        + "AAAECN4xgWbkZ3PcTOyBieGezySpNHanZk/AptSoEKFgcDMEP8Msse1vKavyCSqL1KkQIi\n"
        + "lQJkJYxnb+xeTsh7mcSlAAAADnN1cmwtdGVzdC1ob3N0AQIDBAUGBw==\n"
        + "-----END OPENSSH PRIVATE KEY-----\n";

    /// <summary>The host certificate of <see cref="Ed25519HostKey"/>, <c>host_ed25519-cert.pub</c>.</summary>
    public const string Ed25519HostCertificate =
        "ssh-ed25519-cert-v01@openssh.com AAAAIHNzaC1lZDI1NTE5LWNlcnQtdjAxQG9wZW5zc2guY29tAAAAIEKwQ+RSOe/QCNK46H9BD7FkGwweDbWLtXbNH1mkMz3rAAAAIEP8Msse1vKavyCSqL1KkQIilQJkJYxnb+xeTsh7mcSlAAAAAAAAAAEAAAACAAAAFnN1cmwtdGVzdC1ob3N0X2VkMjU1MTkAAAANAAAACWxvY2FsaG9zdAAAAABpVhtwAAAAAHwkwXAAAAAAAAAAAAAAAAAAAAAzAAAAC3NzaC1lZDI1NTE5AAAAIFrEwtaN4iFKfxUjoffYqng6TIM9te63RQSFVNgrXVuWAAAAUwAAAAtzc2gtZWQyNTUxOQAAAEBdgxis/ZRSp5yZuAIjPyzMeFU4ELD8LDgZisbcWXnklXWwSnnkrd4pd/E5F34hCYONrDQcRFJWp3zVtzr90DsI surl-test-host\n";
}
