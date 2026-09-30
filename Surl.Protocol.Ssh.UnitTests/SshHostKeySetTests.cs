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
        CollectionAssert.AreEqual(new[] { "rsa-sha2-512", "rsa-sha2-256", "ecdsa-sha2-nistp256" }, set.SignatureAlgorithms.ToArray());
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
        Assert.ThrowsExactly<ArgumentNullException>(() => new SshHostKeySet().TryAdd(null!, out _));
    }
}
