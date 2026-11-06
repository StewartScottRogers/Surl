namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshHostKeySetTests
{
    [TestMethod]
    public void TryAdd_KeysOfDifferentTypes_AreAllHeldInOrder()
    {
        var set = new SshHostKeySet();
        var rsa = SshHostKey.FromRsa(SshTestKeys.Rsa2048);
        var ecdsa = SshHostKey.FromEcdsa(SshTestKeys.EcdsaP256);

        Assert.IsTrue(set.TryAdd(rsa, out var heldForRsa));
        Assert.IsTrue(set.TryAdd(ecdsa, out var heldForEcdsa));

        Assert.IsNull(heldForRsa);
        Assert.IsNull(heldForEcdsa);
        CollectionAssert.AreEqual(new[] { rsa, ecdsa }, set.Keys.ToArray());
        CollectionAssert.AreEqual(new[] { "rsa-sha2-512", "rsa-sha2-256", "ssh-rsa", "ecdsa-sha2-nistp256" }, set.SignatureAlgorithms.ToArray());
    }

    [TestMethod]
    public void TryAdd_SecondKeyOfOneType_IsNotAddedAndNamesTheKeyHeld()
    {
        var set = new SshHostKeySet();
        var first = SshHostKey.FromRsa(SshTestKeys.Rsa2048);
        set.TryAdd(first, out _);

        var added = set.TryAdd(SshHostKey.FromRsa(SshTestKeys.Rsa1024), out var held);

        Assert.IsFalse(added);
        Assert.AreSame(first, held);
        Assert.HasCount(1, set.Keys);
    }

    [TestMethod]
    public void ForSignatureAlgorithm_TheKeyThatSignsIt_IsReturned()
    {
        var set = SshTestKeys.AllHostKeys();

        Assert.AreEqual("ssh-ed25519", set.ForSignatureAlgorithm("ssh-ed25519").KeyType);
        Assert.AreEqual("ecdsa-sha2-nistp384", set.ForSignatureAlgorithm("ecdsa-sha2-nistp384").KeyType);
        Assert.AreEqual("ssh-rsa", set.ForSignatureAlgorithm("rsa-sha2-256").KeyType);
    }

    [TestMethod]
    public void ForSignatureAlgorithm_NoKeySignsIt_Throws()
    {
        var set = SshTestKeys.HostKeysOf(SshTestKeys.Rsa2048);

        var refusal = Assert.ThrowsExactly<InvalidOperationException>(() => set.ForSignatureAlgorithm("ssh-ed25519"));

        Assert.AreEqual("The SSH server offered the host-key algorithm ssh-ed25519, but holds no key that signs with it.", refusal.Message);
    }

    [TestMethod]
    public void TryAdd_Null_IsRefused()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SshHostKeySet().TryAdd((SshHostKey)null!, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SshHostKeySet().TryAdd((SshHostCertificate)null!, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SshHostKeySet().HoldsKeyCertifiedBy(null!));
    }

    [TestMethod]
    public void TryAdd_CertificatesOfHeldKeys_AreHeldInOrderAndSignWithTheCertificateAlgorithms()
    {
        var set = new SshHostKeySet();
        set.TryAdd(SshTestCertificates.HostKey("host_rsa"), out _);
        set.TryAdd(SshTestCertificates.HostKey("host_ed25519"), out _);
        var ed25519Certificate = SshTestCertificates.Certificate("host_ed25519");
        var rsaCertificate = SshTestCertificates.Certificate("host_rsa");

        Assert.IsTrue(set.HoldsKeyCertifiedBy(ed25519Certificate));
        Assert.IsTrue(set.TryAdd(ed25519Certificate, out var heldForEd25519));
        Assert.IsTrue(set.TryAdd(rsaCertificate, out var heldForRsa));

        Assert.IsNull(heldForEd25519);
        Assert.IsNull(heldForRsa);
        Assert.HasCount(2, set.Keys);
        CollectionAssert.AreEqual(new[] { ed25519Certificate, rsaCertificate }, set.Certificates.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "rsa-sha2-512", "rsa-sha2-256", "ssh-rsa", "ssh-ed25519",
                "ssh-ed25519-cert-v01@openssh.com",
                "rsa-sha2-512-cert-v01@openssh.com", "rsa-sha2-256-cert-v01@openssh.com", "ssh-rsa-cert-v01@openssh.com",
            },
            set.SignatureAlgorithms.ToArray());
        var certifiedKey = set.ForSignatureAlgorithm("rsa-sha2-256-cert-v01@openssh.com");
        Assert.AreEqual("ssh-rsa-cert-v01@openssh.com", certifiedKey.KeyType);
        CollectionAssert.AreEqual(rsaCertificate.Blob.ToArray(), certifiedKey.PublicKeyBlob.ToArray());
        Assert.AreEqual("ssh-rsa", set.ForSignatureAlgorithm("rsa-sha2-256").KeyType);
    }

    [TestMethod]
    public void TryAdd_SecondCertificateOfOneType_IsNotAddedAndNamesTheCertificateHeld()
    {
        var set = new SshHostKeySet();
        set.TryAdd(SshTestCertificates.HostKey("host_ed25519"), out _);
        var first = SshTestCertificates.Certificate("host_ed25519");
        set.TryAdd(first, out _);

        var added = set.TryAdd(SshTestCertificates.Certificate("host_ed25519"), out var held);

        Assert.IsFalse(added);
        Assert.AreSame(first, held);
        Assert.HasCount(1, set.Certificates);
    }

    [TestMethod]
    public void TryAdd_CertificateOfNoKeyHeld_Throws()
    {
        var set = SshTestKeys.HostKeysOf(SshTestKeys.Rsa2048);
        var certificate = SshTestCertificates.Certificate("host_rsa");

        Assert.IsFalse(set.HoldsKeyCertifiedBy(certificate));
        Assert.ThrowsExactly<ArgumentException>(() => set.TryAdd(certificate, out _));
        Assert.IsEmpty(set.Certificates);
    }
}
