namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshNegotiatedAlgorithmsTests
{
    [TestMethod]
    public void ToNote_MacsAgreedAndStrictOff_NamesEveryAlgorithm()
    {
        var algorithms = new SshNegotiatedAlgorithms(
            "curve25519-sha256", "ssh-ed25519", "aes128-ctr", "aes256-ctr", "hmac-sha2-256", "hmac-sha2-512", "none", "zlib", false, false);

        var note = algorithms.ToNote();

        Assert.AreEqual(
            "SSH negotiated kex curve25519-sha256, host key ssh-ed25519, cipher aes128-ctr/aes256-ctr, "
            + "MAC hmac-sha2-256/hmac-sha2-512, compression none/zlib, strict kex off",
            note);
    }

    [TestMethod]
    public void ToNote_AeadCipherAndStrictOn_WritesImplicitMacs()
    {
        var algorithms = new SshNegotiatedAlgorithms(
            "curve25519-sha256", "ssh-ed25519", "aes256-gcm@openssh.com", "aes256-gcm@openssh.com", null, null, "none", "none", true, false);

        var note = algorithms.ToNote();

        Assert.AreEqual(
            "SSH negotiated kex curve25519-sha256, host key ssh-ed25519, cipher aes256-gcm@openssh.com/aes256-gcm@openssh.com, "
            + "MAC implicit/implicit, compression none/none, strict kex on",
            note);
    }
}
