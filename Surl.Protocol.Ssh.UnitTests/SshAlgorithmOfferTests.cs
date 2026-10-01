namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshAlgorithmOfferTests
{
    /// <summary>Every weak name ADR-0051 decision 2 offers only with <c>--allow-weak-ssh-algorithms</c>.</summary>
    public static readonly string[] WeakNames =
    [
        "diffie-hellman-group14-sha1", "diffie-hellman-group-exchange-sha1", "diffie-hellman-group1-sha1",
        "ssh-rsa", "ssh-dss",
        "aes256-cbc", "rijndael-cbc@lysator.liu.se", "aes192-cbc", "aes128-cbc", "3des-cbc", "arcfour128", "arcfour", "blowfish-cbc", "cast128-cbc",
        "hmac-sha1-etm@openssh.com", "hmac-sha1", "hmac-sha1-96", "hmac-md5", "hmac-md5-96", "hmac-ripemd160", "hmac-ripemd160@openssh.com",
    ];

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
    public void Default_WithoutWeakAlgorithms_ListsNoWeakName()
    {
        var offer = SshAlgorithmOffer.Default(["rsa-sha2-512", "rsa-sha2-256", "ssh-rsa", "ssh-dss"], aesGcmIsSupported: true);

        string[] listed = [.. offer.KeyExchange, .. offer.ServerHostKey, .. offer.Cipher, .. offer.Mac, .. offer.Compression];

        Assert.IsFalse(offer.AllowsWeakAlgorithms);
        Assert.IsEmpty(listed.Intersect(WeakNames));
    }

    // ADR-0051 decision 2: each list's weak entries follow its default ones, in the decision's
    // order; the strict key exchange marker stays last.
    [TestMethod]
    public void Default_WithWeakAlgorithms_AppendsEachListsWeakNamesInTheDecisionsOrder()
    {
        var offer = SshAlgorithmOffer.Default(["ssh-dss", "rsa-sha2-512", "rsa-sha2-256", "ssh-rsa"], aesGcmIsSupported: false, allowWeakAlgorithms: true);

        Assert.IsTrue(offer.AllowsWeakAlgorithms);
        CollectionAssert.AreEqual(
            new[]
            {
                "curve25519-sha256", "curve25519-sha256@libssh.org", "ecdh-sha2-nistp256", "ecdh-sha2-nistp384", "ecdh-sha2-nistp521",
                "diffie-hellman-group-exchange-sha256", "diffie-hellman-group16-sha512", "diffie-hellman-group18-sha512", "diffie-hellman-group14-sha256",
                "diffie-hellman-group14-sha1", "diffie-hellman-group-exchange-sha1", "diffie-hellman-group1-sha1", "kex-strict-s-v00@openssh.com",
            },
            offer.KeyExchange.ToArray());
        CollectionAssert.AreEqual(new[] { "rsa-sha2-512", "rsa-sha2-256", "ssh-rsa", "ssh-dss" }, offer.ServerHostKey.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "chacha20-poly1305@openssh.com", "aes256-ctr", "aes192-ctr", "aes128-ctr",
                "aes256-cbc", "rijndael-cbc@lysator.liu.se", "aes192-cbc", "aes128-cbc", "3des-cbc", "arcfour128", "arcfour", "blowfish-cbc", "cast128-cbc",
            },
            offer.Cipher.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "hmac-sha2-256-etm@openssh.com", "hmac-sha2-512-etm@openssh.com", "hmac-sha2-256", "hmac-sha2-512",
                "hmac-sha1-etm@openssh.com", "hmac-sha1", "hmac-sha1-96", "hmac-md5", "hmac-md5-96", "hmac-ripemd160", "hmac-ripemd160@openssh.com",
            },
            offer.Mac.ToArray());
        CollectionAssert.AreEqual(new[] { "none", "zlib@openssh.com", "zlib" }, offer.Compression.ToArray());
    }

    [TestMethod]
    public void Default_WithWeakAlgorithmsAndNoDsaKey_LeavesSshDssOut()
    {
        var offer = SshAlgorithmOffer.Default(["rsa-sha2-512", "rsa-sha2-256", "ssh-rsa"], aesGcmIsSupported: true, allowWeakAlgorithms: true);

        CollectionAssert.AreEqual(new[] { "rsa-sha2-512", "rsa-sha2-256", "ssh-rsa" }, offer.ServerHostKey.ToArray());
    }

    [TestMethod]
    public void Default_NullHostKeyAlgorithms_AreRefused()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SshAlgorithmOffer.Default(null!, aesGcmIsSupported: true));
    }

    [TestMethod]
    public void Narrowed_GivenNames_AreOfferedAloneInTheGivenOrderOnceEachWithUnofferedNamesLeftOut()
    {
        var offer = SshAlgorithmOffer.Default(["rsa-sha2-256"], aesGcmIsSupported: true, allowWeakAlgorithms: true);

        var narrowed = offer.Narrowed(
            ["cast128-cbc", "blowfish-cbc", "cast128-cbc", "no-such-cipher"],
            ["hmac-ripemd160", "hmac-sha2-256"]);

        CollectionAssert.AreEqual(new[] { "cast128-cbc", "blowfish-cbc" }, narrowed.Cipher.ToArray());
        CollectionAssert.AreEqual(new[] { "hmac-ripemd160", "hmac-sha2-256" }, narrowed.Mac.ToArray());
        Assert.AreSame(offer.KeyExchange, narrowed.KeyExchange);
        Assert.IsTrue(narrowed.AllowsWeakAlgorithms);
    }

    [TestMethod]
    public void Narrowed_NoListGiven_KeepsTheOffersLists()
    {
        var offer = SshAlgorithmOffer.Default(["rsa-sha2-256"], aesGcmIsSupported: true);

        var narrowed = offer.Narrowed(null, null);

        Assert.AreSame(offer.Cipher, narrowed.Cipher);
        Assert.AreSame(offer.Mac, narrowed.Mac);
    }
}
