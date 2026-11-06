namespace Surl.Protocol.Ssh;

/// <summary>
/// The host keys and host certificates in <c>Fixtures/host-certificates</c>, written by
/// <c>ssh-keygen</c> from OpenSSH_10.3p1 (Windows' OpenSSH client, with OpenSSL 3.5.7) for these
/// tests only: an RSA 2048-bit, an ECDSA P-256 and an Ed25519 host key, each certified by the
/// Ed25519 test CA <c>ca_ed25519.pub</c> (<c>ssh-keygen -s ca_ed25519 -h -I surl-test-&lt;key&gt;
/// -n localhost -V 20260101:20360101 -z 1 &lt;key&gt;.pub</c>); a host certificate for an ECDSA
/// P-384 key whose private key was deleted (<c>other_ecdsa_p384-cert.pub</c>); and a user
/// certificate for the Ed25519 host key (<c>user_ed25519-cert.pub</c>, <c>-I surl-test-user -n
/// tester -z 2</c>, no <c>-h</c>).
/// </summary>
internal static class SshTestCertificates
{
    public const string Folder = "host-certificates";

    /// <summary>The three certified host keys' fixture names.</summary>
    public static readonly string[] HostKeyNames = ["host_rsa", "host_ecdsa_p256", "host_ed25519"];

    public static byte[] FileBytes(string fileName) => RecordedFixture.ReadBytes(Folder, fileName);

    public static SshHostKey HostKey(string name) => SshHostKeyFile.Read(FileBytes(name), null, allowWeakAlgorithms: false).Key!;

    public static SshHostCertificate Certificate(string name) => SshHostCertificate.Read(FileBytes(name + "-cert.pub")).Certificate!;

    /// <summary>
    /// A set holding the three fixture host keys and each one's certificate.
    /// </summary>
    public static SshHostKeySet CertifiedHostKeys()
    {
        var set = new SshHostKeySet();
        foreach (var name in HostKeyNames)
        {
            set.TryAdd(HostKey(name), out _);
            set.TryAdd(Certificate(name), out _);
        }

        return set;
    }
}
