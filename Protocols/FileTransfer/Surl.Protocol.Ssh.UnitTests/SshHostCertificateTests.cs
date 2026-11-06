using System.Text;
using static Surl.Protocol.Ssh.SshTestCertificates;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Reading <c>--hostcert</c> files (ADR-0051, decision 4) from the <c>ssh-keygen</c> fixtures
/// <see cref="SshTestCertificates"/> describes, and each refusal.
/// </summary>
[TestClass]
public sealed class SshHostCertificateTests
{
    [TestMethod]
    [DataRow("host_rsa", "ssh-rsa")]
    [DataRow("host_ecdsa_p256", "ecdsa-sha2-nistp256")]
    [DataRow("host_ed25519", "ssh-ed25519")]
    public void Read_HostCertificateFixture_IsReadAndCertifiesItsHostKey(string name, string keyType)
    {
        var fileBytes = FileBytes(name + "-cert.pub");

        var reading = SshHostCertificate.Read(fileBytes);

        Assert.IsNull(reading.Refusal);
        var certificate = reading.Certificate!;
        Assert.AreEqual(keyType, certificate.KeyType);
        Assert.AreEqual(keyType + "-cert-v01@openssh.com", certificate.CertificateType);
        CollectionAssert.AreEqual(Convert.FromBase64String(Encoding.ASCII.GetString(fileBytes).Split(' ')[1]), certificate.Blob.ToArray());
        var hostKey = HostKey(name);
        CollectionAssert.AreEqual(hostKey.PublicKeyBlob.ToArray(), certificate.CertifiedPublicKeyBlob.ToArray());
        Assert.IsTrue(certificate.Certifies(hostKey));
    }

    [TestMethod]
    public void Certifies_AnotherKey_IsFalse()
    {
        var certificate = Certificate("other_ecdsa_p384");

        Assert.AreEqual("ecdsa-sha2-nistp384-cert-v01@openssh.com", certificate.CertificateType);
        foreach (var name in HostKeyNames)
        {
            Assert.IsFalse(certificate.Certifies(HostKey(name)));
        }

        Assert.IsFalse(certificate.Certifies(SshHostKey.FromEcdsa(SshTestKeys.EcdsaP384)));
    }

    [TestMethod]
    public void Certifies_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Certificate("host_rsa").Certifies(null!));
    }

    [TestMethod]
    public void Read_WithoutCommentOrWithTabsAndLineEnd_IsRead()
    {
        var words = CertificateLine("host_ed25519").Split(' ');

        Assert.IsNotNull(SshHostCertificate.Read(Encoding.ASCII.GetBytes(words[0] + " " + words[1])).Certificate);
        Assert.IsNotNull(SshHostCertificate.Read(Encoding.ASCII.GetBytes(words[0] + "\t" + words[1] + "\tcomment\r\n")).Certificate);
    }

    [TestMethod]
    public void Read_UserCertificate_IsNotAnOpenSshHostCertificate()
    {
        AssertRefused(FileBytes("user_ed25519-cert.pub"));
    }

    [TestMethod]
    [DataRow("host_rsa")]
    [DataRow("ca_ed25519.pub")]
    public void Read_APrivateKeyOrAPlainPublicKey_IsNotAnOpenSshHostCertificate(string fileName)
    {
        AssertRefused(FileBytes(fileName));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   \r\n")]
    [DataRow("ssh-ed25519-cert-v01@openssh.com")]
    [DataRow("ssh-ed25519-cert-v01@openssh.com not-base64!")]
    [DataRow("ssh-ed25519-cert-v01@openssh.com AAAA")]
    public void Read_NotALineWithAWholeBlob_IsNotAnOpenSshHostCertificate(string text)
    {
        AssertRefused(Encoding.ASCII.GetBytes(text));
    }

    [TestMethod]
    public void Read_TwoLines_IsNotAnOpenSshHostCertificate()
    {
        var line = CertificateLine("host_ed25519");

        AssertRefused(Encoding.ASCII.GetBytes(line + "\n" + line));
    }

    [TestMethod]
    public void Read_LineTypeThatIsNotTheBlobs_IsNotAnOpenSshHostCertificate()
    {
        var words = CertificateLine("host_ed25519").Split(' ');

        AssertRefused(Encoding.ASCII.GetBytes("ssh-rsa-cert-v01@openssh.com " + words[1]));
    }

    [TestMethod]
    [DataRow("ssh-dss-cert-v01@openssh.com")]
    [DataRow("sk-ssh-ed25519-cert-v01@openssh.com")]
    [DataRow("ssh-ed25519")]
    public void Read_ATypeThatIsNoCertificateSurlReads_IsNotAnOpenSshHostCertificate(string type)
    {
        var blob = Blob("host_ed25519");
        var retyped = new SshWireWriter();
        retyped.WriteString(type);
        retyped.WriteBytes(blob.AsSpan(4 + 32));

        AssertRefused(Line(type, retyped.ToArray()));
    }

    [TestMethod]
    public void Read_BlobCutShort_IsNotAnOpenSshHostCertificate()
    {
        var blob = Blob("host_ed25519");

        AssertRefused(Line("ssh-ed25519-cert-v01@openssh.com", blob[..^1]));
    }

    [TestMethod]
    public void Read_BlobRunningOnPastItsSignature_IsNotAnOpenSshHostCertificate()
    {
        var blob = Blob("host_ed25519");

        AssertRefused(Line("ssh-ed25519-cert-v01@openssh.com", [.. blob, 0]));
    }

    private static string CertificateLine(string name) => Encoding.ASCII.GetString(FileBytes(name + "-cert.pub")).TrimEnd();

    private static byte[] Blob(string name) => Convert.FromBase64String(CertificateLine(name).Split(' ')[1]);

    private static byte[] Line(string type, byte[] blob) => Encoding.ASCII.GetBytes(type + " " + Convert.ToBase64String(blob) + " comment\n");

    private static void AssertRefused(byte[] fileBytes)
    {
        var reading = SshHostCertificate.Read(fileBytes);

        Assert.IsNull(reading.Certificate);
        Assert.AreSame(SshHostCertificateRefusal.NotAHostCertificate, reading.Refusal);
        Assert.AreEqual(SshHostCertificateRefusalReason.NotAHostCertificate, reading.Refusal!.Reason);
        Assert.AreEqual("not an OpenSSH host certificate", reading.Refusal.Text);
    }
}
