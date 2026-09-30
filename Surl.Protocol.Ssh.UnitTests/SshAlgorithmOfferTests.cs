namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshAlgorithmOfferTests
{
    [TestMethod]
    public void Default_HostKeys_AreThoseHeldInTheDecisionsOrderWithUnknownNamesLeftOut()
    {
        var offer = SshAlgorithmOffer.Default(["rsa-sha2-256", "ssh-dss", "ecdsa-sha2-nistp384", "ssh-ed25519", "rsa-sha2-512"], aesGcmIsSupported: true);

        CollectionAssert.AreEqual(
            new[] { "ssh-ed25519", "ecdsa-sha2-nistp384", "rsa-sha2-512", "rsa-sha2-256" },
            offer.ServerHostKey.ToArray());
    }

    [TestMethod]
    public void Default_AesGcmNotSupported_LeavesTheGcmCiphersOut()
    {
        var offer = SshAlgorithmOffer.Default(["ssh-ed25519"], aesGcmIsSupported: false);

        CollectionAssert.AreEqual(
            new[] { "chacha20-poly1305@openssh.com", "aes256-ctr", "aes192-ctr", "aes128-ctr" },
            offer.Cipher.ToArray());
    }

    [TestMethod]
    public void Default_KeyExchange_EndsWithTheStrictKeyExchangeServerMarker()
    {
        var offer = SshAlgorithmOffer.Default([], aesGcmIsSupported: true);

        Assert.AreEqual("curve25519-sha256", offer.KeyExchange[0]);
        Assert.AreEqual("kex-strict-s-v00@openssh.com", offer.KeyExchange[^1]);
        Assert.AreEqual(SshAlgorithmOffer.StrictKeyExchangeServerMarker, offer.KeyExchange[^1]);
    }

    [TestMethod]
    public void Default_NullHostKeyAlgorithms_AreRefused()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SshAlgorithmOffer.Default(null!, aesGcmIsSupported: true));
    }
}
