using System.Text;

namespace Surl.Console;

/// <summary>
/// The <c>--hostcert</c> files the start-up tests read through the runner's file seam: copies of
/// <c>Surl.Protocol.Ssh.UnitTests/Fixtures/host-certificates</c>, written by <c>ssh-keygen</c> from
/// OpenSSH_10.3p1 for tests only (that folder's <c>SshTestCertificates</c> says how).
/// </summary>
internal static class TestSshCertificateFiles
{
    /// <summary>An Ed25519 host key, <c>host_ed25519</c>, in <c>openssh-key-v1</c>.</summary>
    public static byte[] Ed25519HostKey => Encoding.ASCII.GetBytes(
        "-----BEGIN OPENSSH PRIVATE KEY-----\n"
        + "b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW\n"
        + "QyNTUxOQAAACBD/DLLHtbymr8gkqi9SpECIpUCZCWMZ2/sXk7Ie5nEpQAAAJgXudblF7nW\n"
        + "5QAAAAtzc2gtZWQyNTUxOQAAACBD/DLLHtbymr8gkqi9SpECIpUCZCWMZ2/sXk7Ie5nEpQ\n"
        + "AAAECN4xgWbkZ3PcTOyBieGezySpNHanZk/AptSoEKFgcDMEP8Msse1vKavyCSqL1KkQIi\n"
        + "lQJkJYxnb+xeTsh7mcSlAAAADnN1cmwtdGVzdC1ob3N0AQIDBAUGBw==\n"
        + "-----END OPENSSH PRIVATE KEY-----\n");

    /// <summary>The host certificate of <see cref="Ed25519HostKey"/>, <c>host_ed25519-cert.pub</c>.</summary>
    public static byte[] Ed25519HostCertificate => Encoding.ASCII.GetBytes(
        "ssh-ed25519-cert-v01@openssh.com AAAAIHNzaC1lZDI1NTE5LWNlcnQtdjAxQG9wZW5zc2guY29tAAAAIEKwQ+RSOe/QCNK46H9BD7FkGwweDbWLtXbNH1mkMz3rAAAAIEP8Msse1vKavyCSqL1KkQIilQJkJYxnb+xeTsh7mcSlAAAAAAAAAAEAAAACAAAAFnN1cmwtdGVzdC1ob3N0X2VkMjU1MTkAAAANAAAACWxvY2FsaG9zdAAAAABpVhtwAAAAAHwkwXAAAAAAAAAAAAAAAAAAAAAzAAAAC3NzaC1lZDI1NTE5AAAAIFrEwtaN4iFKfxUjoffYqng6TIM9te63RQSFVNgrXVuWAAAAUwAAAAtzc2gtZWQyNTUxOQAAAEBdgxis/ZRSp5yZuAIjPyzMeFU4ELD8LDgZisbcWXnklXWwSnnkrd4pd/E5F34hCYONrDQcRFJWp3zVtzr90DsI surl-test-host\n");

    /// <summary>A user certificate of <see cref="Ed25519HostKey"/>'s key, <c>user_ed25519-cert.pub</c>.</summary>
    public static byte[] Ed25519UserCertificate => Encoding.ASCII.GetBytes(
        "ssh-ed25519-cert-v01@openssh.com AAAAIHNzaC1lZDI1NTE5LWNlcnQtdjAxQG9wZW5zc2guY29tAAAAIJwJ7DEC2OlBfbv2yQ2iW8efBi4/vMDU+xjDo1vK0cIyAAAAIEP8Msse1vKavyCSqL1KkQIilQJkJYxnb+xeTsh7mcSlAAAAAAAAAAIAAAABAAAADnN1cmwtdGVzdC11c2VyAAAACgAAAAZ0ZXN0ZXIAAAAAAAAAAP//////////AAAAAAAAAIIAAAAVcGVybWl0LVgxMS1mb3J3YXJkaW5nAAAAAAAAABdwZXJtaXQtYWdlbnQtZm9yd2FyZGluZwAAAAAAAAAWcGVybWl0LXBvcnQtZm9yd2FyZGluZwAAAAAAAAAKcGVybWl0LXB0eQAAAAAAAAAOcGVybWl0LXVzZXItcmMAAAAAAAAAAAAAADMAAAALc3NoLWVkMjU1MTkAAAAgWsTC1o3iIUp/FSOh99iqeDpMgz217rdFBIVU2CtdW5YAAABTAAAAC3NzaC1lZDI1NTE5AAAAQAcSDRadDUTsMTg5IHRSRdIUArPdCx60fVO0kfQdMmVm00H+TnGXi4xbjwXQnBrRzyWG/4h9fGvOtW5mOmD36wA= surl-test-host\n");

    /// <summary>A host certificate of an ECDSA P-384 key no test holds, <c>other_ecdsa_p384-cert.pub</c>.</summary>
    public static byte[] OtherEcdsaP384HostCertificate => Encoding.ASCII.GetBytes(
        "ecdsa-sha2-nistp384-cert-v01@openssh.com AAAAKGVjZHNhLXNoYTItbmlzdHAzODQtY2VydC12MDFAb3BlbnNzaC5jb20AAAAguMawejRM5v8YGCFvWGeNSnxNGln525xsp5ipYQh+iUYAAAAIbmlzdHAzODQAAABhBPS1yv82rfOwy78IKKlTnG35Aw10My5MaDMzAtpUcUpOll2U+3B7lQ3qEi3IBLyMkLrKGVbr9/wgXfn/YPOsUjXw94mR/0GC5J6CeUrtqmRYmW0HKLZpSTWtWrQV0dFJbgAAAAAAAAABAAAAAgAAABpzdXJsLXRlc3Qtb3RoZXJfZWNkc2FfcDM4NAAAAA0AAAAJbG9jYWxob3N0AAAAAGlWG3AAAAAAfCTBcAAAAAAAAAAAAAAAAAAAADMAAAALc3NoLWVkMjU1MTkAAAAgWsTC1o3iIUp/FSOh99iqeDpMgz217rdFBIVU2CtdW5YAAABTAAAAC3NzaC1lZDI1NTE5AAAAQBMyQUhewvycpDrTBQJ9rpHd65pIeOFtV4ErEgJk/J1o5uYALjreVPhQR8heJHD3AzJ9HwMI+8u4SaJseNMH+gU= surl-test-host\n");
}
