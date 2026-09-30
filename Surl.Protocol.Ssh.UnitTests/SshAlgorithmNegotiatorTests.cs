namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshAlgorithmNegotiatorTests
{
    private static readonly SshKexInit Server = SshKexInit.ForServer(SshTestExchange.RsaOffer, new SshTestExchange.FixedRandomSource());

    [TestMethod]
    public void Negotiate_EachList_AgreesTheFirstClientNameTheServerOffers()
    {
        var client = Client(
            keyExchange: ["diffie-hellman-group1-sha1", "ecdh-sha2-nistp384", "curve25519-sha256"],
            hostKey: ["ssh-rsa", "rsa-sha2-256", "rsa-sha2-512"],
            cipher: ["aes256-cbc", "aes128-ctr", "aes256-ctr"],
            mac: ["hmac-sha1", "hmac-sha2-512", "hmac-sha2-256"],
            compression: ["zlib@openssh.com", "none"]);

        var agreed = SshAlgorithmNegotiator.Negotiate(client, Server);

        Assert.AreEqual(
            new SshNegotiatedAlgorithms(
                "ecdh-sha2-nistp384",
                "rsa-sha2-256",
                "aes128-ctr",
                "aes128-ctr",
                "hmac-sha2-512",
                "hmac-sha2-512",
                "zlib@openssh.com",
                "zlib@openssh.com",
                StrictKeyExchange: false,
                ClientGuessIsWrong: false),
            agreed);
    }

    [TestMethod]
    [DataRow("aes256-gcm@openssh.com")]
    [DataRow("chacha20-poly1305@openssh.com")]
    public void Negotiate_AeadCipher_AgreesNoMacEvenWithNoMacInCommon(string cipher)
    {
        var client = Client(cipher: [cipher], mac: ["hmac-md5"]);

        var agreed = SshAlgorithmNegotiator.Negotiate(client, Server);

        Assert.IsNull(agreed.MacClientToServer);
        Assert.IsNull(agreed.MacServerToClient);
    }

    [TestMethod]
    public void Negotiate_ServerStrictMarker_IsNeverAgreedAsAMethod()
    {
        var client = Client(keyExchange: [SshAlgorithmOffer.StrictKeyExchangeServerMarker]);

        var refusal = Assert.ThrowsExactly<SshDisconnectRequiredException>(() => SshAlgorithmNegotiator.Negotiate(client, Server));

        Assert.AreEqual("SSH no common kex algorithm; client offered kex-strict-s-v00@openssh.com", refusal.Message);
    }

    [TestMethod]
    public void Negotiate_ClientStrictMarker_TurnsStrictKeyExchangeOn()
    {
        var client = Client(keyExchange: ["curve25519-sha256", SshAlgorithmOffer.StrictKeyExchangeClientMarker]);

        var agreed = SshAlgorithmNegotiator.Negotiate(client, Server);

        Assert.IsTrue(agreed.StrictKeyExchange);
    }

    [TestMethod]
    [DataRow("kex", DisplayName = "Key exchange")]
    [DataRow("host key", DisplayName = "Host key")]
    [DataRow("cipher", DisplayName = "Cipher")]
    [DataRow("MAC", DisplayName = "MAC")]
    [DataRow("compression", DisplayName = "Compression")]
    public void Negotiate_ListWithNothingInCommon_IsRefusedDisconnect3(string kind)
    {
        string[] offered = ["weird\x01name", "other"];
        var client = Client(
            keyExchange: kind == "kex" ? offered : null,
            hostKey: kind == "host key" ? offered : null,
            cipher: kind == "cipher" ? offered : null,
            mac: kind == "MAC" ? offered : null,
            compression: kind == "compression" ? offered : null);

        var refusal = Assert.ThrowsExactly<SshDisconnectRequiredException>(() => SshAlgorithmNegotiator.Negotiate(client, Server));

        Assert.AreEqual(SshDisconnectReason.KeyExchangeFailed, refusal.Reason);
        Assert.AreEqual("No common algorithm", refusal.Description);
        Assert.AreEqual($@"SSH no common {kind} algorithm; client offered weird\x01name,other", refusal.Message);
    }

    [TestMethod]
    public void Negotiate_EmptyClientList_IsRefused()
    {
        var client = Client(hostKey: []);

        var refusal = Assert.ThrowsExactly<SshDisconnectRequiredException>(() => SshAlgorithmNegotiator.Negotiate(client, Server));

        Assert.AreEqual("SSH no common host key algorithm; client offered ", refusal.Message);
    }

    [TestMethod]
    [DataRow("curve25519-sha256", "rsa-sha2-512", false, DisplayName = "Both first choices match the server's")]
    [DataRow("ecdh-sha2-nistp256", "rsa-sha2-512", true, DisplayName = "Another key exchange method first")]
    [DataRow("curve25519-sha256", "rsa-sha2-256", true, DisplayName = "Another host-key algorithm first")]
    public void Negotiate_GuessedPacketFollows_IsWrongUnlessBothFirstChoicesMatch(string keyExchange, string hostKey, bool wrong)
    {
        var client = Client(keyExchange: [keyExchange, "curve25519-sha256"], hostKey: [hostKey, "rsa-sha2-512"], firstKexPacketFollows: true);

        var agreed = SshAlgorithmNegotiator.Negotiate(client, Server);

        Assert.AreEqual(wrong, agreed.ClientGuessIsWrong);
    }

    private static SshKexInit Client(
        string[]? keyExchange = null,
        string[]? hostKey = null,
        string[]? cipher = null,
        string[]? mac = null,
        string[]? compression = null,
        bool firstKexPacketFollows = false)
    {
        cipher ??= ["aes256-ctr"];
        mac ??= ["hmac-sha2-256"];
        compression ??= ["none"];

        return new SshKexInit(
            new byte[SshKexInit.CookieLength],
            keyExchange ?? ["curve25519-sha256"],
            hostKey ?? ["rsa-sha2-512"],
            cipher,
            cipher,
            mac,
            mac,
            compression,
            compression,
            [],
            [],
            firstKexPacketFollows);
    }
}
