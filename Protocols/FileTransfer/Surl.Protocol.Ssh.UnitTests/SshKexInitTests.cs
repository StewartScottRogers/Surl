using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshKexInitTests
{
    [TestMethod]
    public void Parse_RecordedCurlKexInit_ReadsEveryList()
    {
        var request = RecordedFixture.ReadRequestBytes("sftp-insecure");
        var payload = request[(ClientLine.Length + 5)..^request[ClientLine.Length + 4]];

        var kexInit = SshKexInit.Parse(payload);

        CollectionAssert.AreEqual(payload[1..17], kexInit.Cookie);
        Assert.AreEqual("diffie-hellman-group-exchange-sha256", kexInit.KeyExchange[0]);
        Assert.AreEqual("kex-strict-c-v00@openssh.com", kexInit.KeyExchange[^1]);
        Assert.AreEqual("rsa-sha2-512", kexInit.ServerHostKey[0]);
        Assert.AreEqual("chacha20-poly1305@openssh.com", kexInit.CipherServerToClient[0]);
        Assert.AreEqual("hmac-sha2-256", kexInit.MacServerToClient[0]);
        CollectionAssert.AreEqual(new[] { "none" }, kexInit.CompressionClientToServer.ToArray());
        Assert.IsEmpty(kexInit.LanguagesClientToServer);
        Assert.IsEmpty(kexInit.LanguagesServerToClient);
        Assert.IsFalse(kexInit.FirstKexPacketFollows);
    }

    [TestMethod]
    public void Parse_BytesAfterTheReservedField_AreIgnored()
    {
        var payload = Concat(ClientKexInitPayload(firstKexPacketFollows: true), [1, 2, 3]);

        var kexInit = SshKexInit.Parse(payload);

        Assert.IsTrue(kexInit.FirstKexPacketFollows);
    }

    [TestMethod]
    public void Parse_EndingBeforeTheReservedField_IsRefusedDisconnect2()
    {
        var payload = ClientKexInitPayload()[..^1];

        var refusal = Assert.ThrowsExactly<SshDisconnectRequiredException>(() => SshKexInit.Parse(payload));

        Assert.AreEqual(SshDisconnectReason.ProtocolError, refusal.Reason);
    }

    [TestMethod]
    public void ToPayload_ServerKexInit_IsTheCookieAndOfferInBothDirections()
    {
        var offer = new SshAlgorithmOffer(["k"], ["h"], ["c1", "c2"], ["m"], ["none"]);

        var payload = SshKexInit.ForServer(offer, new FixedRandomSource()).ToPayload();

        CollectionAssert.AreEqual(
            KexInitPayload(false, "k", "h", "c1,c2", "c1,c2", "m", "m", "none", "none", string.Empty, string.Empty)[17..],
            payload[17..]);
        CollectionAssert.AreEqual(Enumerable.Repeat(RandomByte, 16).ToArray(), payload[1..17]);
    }
}
