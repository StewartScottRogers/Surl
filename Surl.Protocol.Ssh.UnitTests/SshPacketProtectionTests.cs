using System.Numerics;
using System.Security.Cryptography;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Every cipher and MAC combination BL-161 builds, run by <see cref="SshTestTransportClient"/>
/// against the server after <c>NEWKEYS</c>: packets of several lengths go both ways, and a
/// flipped ciphertext or MAC byte ends the connection with <c>DISCONNECT</c> 5.
/// </summary>
[TestClass]
public sealed class SshPacketProtectionTests
{
    private const byte LocalExtensionMessage = 200;

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> CipherAndMacCombinations =>
        from cipher in new[] { "aes128-ctr", "aes192-ctr", "aes256-ctr" }
        from mac in new[] { "hmac-sha2-256", "hmac-sha2-512", "hmac-sha2-256-etm@openssh.com", "hmac-sha2-512-etm@openssh.com" }
        select new object[] { cipher, mac };

    public static IEnumerable<object[]> EveryProtection =>
        CipherAndMacCombinations.Concat(
        [
            ["aes128-gcm@openssh.com", "hmac-sha2-256"],
            ["aes256-gcm@openssh.com", "hmac-sha2-256"],
            ["chacha20-poly1305@openssh.com", "hmac-sha2-256"],
        ]);

    public static IEnumerable<object[]> EveryProtectionAndTamperedByte =>
        from protection in EveryProtection
        from tampered in new[] { "ciphertext", "MAC" }
        select new object[] { protection[0], protection[1], tampered };

    [TestMethod]
    [DynamicData(nameof(EveryProtection))]
    public async Task PacketsBothWays_UnderEachProtection_AreOpenedAndAnsweredInOrder(string cipher, string mac)
    {
        var client = new SshTestTransportClient(cipher, mac, TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        int[] payloadLengths = [1, 10, 11, 12, 27, 100, 1000];
        foreach (var length in payloadLengths)
        {
            client.Send(Concat([LocalExtensionMessage], new byte[length - 1]));
            client.Send([2]);
        }

        foreach (var length in payloadLengths)
        {
            CollectionAssert.AreEqual(Concat([3], UInt32(client.ReceiveSequenceNumber * 2)), await client.ReceiveAsync());
        }

        client.Connection.CloseClientWrites();
        await serving;

        Assert.AreEqual(
            $"SSH negotiated kex ecdh-sha2-nistp256, host key rsa-sha2-512, cipher {cipher}/{cipher}, "
            + $"MAC {(cipher.EndsWith("@openssh.com", StringComparison.Ordinal) ? "implicit/implicit" : $"{mac}/{mac}")}, compression none/none, strict kex on",
            client.Log.Notes[1]);
        Assert.HasCount(2, client.Log.Notes);
    }

    [TestMethod]
    [DynamicData(nameof(EveryProtectionAndTamperedByte))]
    public async Task FlippedByte_UnderEachProtection_IsAnsweredDisconnect5(string cipher, string mac, string tampered)
    {
        var client = new SshTestTransportClient(cipher, mac, TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);
        client.Send([2]);
        var packet = client.Seal(Concat([LocalExtensionMessage], new byte[40]));

        packet[tampered == "MAC" ? ^1 : 20] ^= 0x01;
        client.Connection.Send(packet);

        CollectionAssert.AreEqual(Concat([1], UInt32(5), String("MAC error"), String(string.Empty)), await client.ReceiveAsync());
        await serving;
        Assert.IsTrue(client.Connection.WritesCompleted);
        CollectionAssert.AreEqual(
            new[] { "The MAC of the client's SSH packet 1 does not verify.", "SSH disconnect sent: 5 MAC error" },
            client.Log.Notes.Skip(2).ToArray());
    }

    [TestMethod]
    public async Task FlippedLengthByteThatStillAligns_UnderChaCha20Poly1305_IsAnsweredDisconnect5()
    {
        var client = new SshTestTransportClient("chacha20-poly1305@openssh.com", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);
        client.Send([2]);
        var packet = client.Seal(Concat([LocalExtensionMessage], new byte[45]));

        packet[3] ^= 0x08;
        client.Connection.Send(packet);

        CollectionAssert.AreEqual(Concat([1], UInt32(5), String("MAC error"), String(string.Empty)), await client.ReceiveAsync());
        await serving;
        CollectionAssert.AreEqual(
            new[] { "The MAC of the client's SSH packet 1 does not verify.", "SSH disconnect sent: 5 MAC error" },
            client.Log.Notes.Skip(2).ToArray(),
            "packet_length 56 read as 48: the tag is taken from the ciphertext and does not verify.");
    }

    [TestMethod]
    public async Task FlippedLengthByteThatNoLongerAligns_UnderChaCha20Poly1305_IsAnsweredDisconnect2()
    {
        var client = new SshTestTransportClient("chacha20-poly1305@openssh.com", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);
        var packet = client.Seal(Concat([LocalExtensionMessage], new byte[45]));

        packet[3] ^= 0x01;
        client.Connection.Send(packet);

        CollectionAssert.AreEqual(Concat([1], UInt32(2), String("Protocol error"), String(string.Empty)), await client.ReceiveAsync());
        await serving;
        Assert.AreEqual("An SSH packet announced 61 bytes, not a multiple of the 8-byte block size.", client.Log.Notes[2]);
    }

    [TestMethod]
    public async Task DifferentProtectionEachWay_IsUsedEachWay()
    {
        var client = new SshTestTransportClient(
            "aes192-ctr",
            "hmac-sha2-512-etm@openssh.com",
            TestContext.CancellationToken,
            cipherServerToClient: "aes256-gcm@openssh.com",
            macServerToClient: "hmac-sha2-256");
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        client.Send([LocalExtensionMessage, 1, 2, 3]);

        CollectionAssert.AreEqual(Concat([3], UInt32(0)), await client.ReceiveAsync());
        client.Connection.CloseClientWrites();
        await serving;
    }

    [TestMethod]
    public async Task ServiceRequest_IsAnsweredServiceAcceptUnderTheKeys()
    {
        var client = new SshTestTransportClient("aes256-ctr", "hmac-sha2-256-etm@openssh.com", TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        client.Send(Concat([5], String("ssh-userauth")));

        CollectionAssert.AreEqual(Concat([6], String("ssh-userauth")), await client.ReceiveAsync());
        client.Connection.CloseClientWrites();
        await serving;
    }

    [TestMethod]
    public async Task IgnoreDebugAndUnimplemented_AfterNewKeys_AreSkippedWithNoAnswer()
    {
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        client.Send(Concat([2], String("x")));
        client.Send(Concat([4, 0], String("debug"), String(string.Empty)));
        client.Send(Concat([3], UInt32(9)));
        client.Send([21]);

        CollectionAssert.AreEqual(Concat([3], UInt32(3)), await client.ReceiveAsync(), "NEWKEYS outside a key exchange is not known here.");
        client.Connection.CloseClientWrites();
        await serving;
    }

    [TestMethod]
    public async Task ClientDisconnect_AfterNewKeys_IsNotedAndNotAnswered()
    {
        var client = new SshTestTransportClient("aes128-gcm@openssh.com", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        client.Send(Concat([1], UInt32(11), String("bye"), String(string.Empty)));
        await serving;

        Assert.AreEqual("SSH disconnect received: 11 bye", client.Log.Notes[^1]);
        await client.Connection.DisposeAsync();
        Assert.IsNull(await client.Connection.ReadServerBytesAsync(1, TestContext.CancellationToken), "Nothing more was written.");
        Assert.IsFalse(client.Connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ClientClosesPartWayThroughAProtectedPacket_IsNotedWithNoReply()
    {
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        client.Connection.Send(client.Seal([LocalExtensionMessage])[..20]);
        client.Connection.CloseClientWrites();
        await serving;

        Assert.AreEqual("The client closed the connection part way through an SSH packet.", client.Log.Notes[^1]);
        await client.Connection.DisposeAsync();
        Assert.IsNull(await client.Connection.ReadServerBytesAsync(1, TestContext.CancellationToken), "Nothing more was written.");
    }

    [TestMethod]
    public async Task PacketLengthZero_UnderALengthInTheClear_IsAnsweredDisconnect2()
    {
        var client = new SshTestTransportClient("aes128-gcm@openssh.com", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        client.Connection.Send(UInt32(0));

        CollectionAssert.AreEqual(Concat([1], UInt32(2), String("Protocol error"), String(string.Empty)), await client.ReceiveAsync());
        await serving;
        Assert.AreEqual("An SSH packet announced no bytes after its length field.", client.Log.Notes[2]);
    }

    [TestMethod]
    [DataRow("aes128-gcm@openssh.com", "hmac-sha2-256", 20u, "An SSH packet announced 24 bytes, not a multiple of the 16-byte block size.", DisplayName = "GCM aligns packet_length alone")]
    [DataRow("aes128-ctr", "hmac-sha2-256-etm@openssh.com", 36u, "An SSH packet announced 40 bytes, not a multiple of the 16-byte block size.", DisplayName = "EtM aligns packet_length alone")]
    public async Task PacketLengthNotAligned_UnderALengthInTheClear_IsAnsweredDisconnect2(string cipher, string mac, uint packetLength, string note)
    {
        var client = new SshTestTransportClient(cipher, mac, TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        client.Connection.Send(UInt32(packetLength));

        CollectionAssert.AreEqual(Concat([1], UInt32(2), String("Protocol error"), String(string.Empty)), await client.ReceiveAsync());
        await serving;
        Assert.AreEqual(note, client.Log.Notes[2]);
    }

    [TestMethod]
    [DataRow("aes128-cbc", "aes128-ctr", DisplayName = "Client to server not built")]
    [DataRow("aes128-ctr", "aes128-cbc", DisplayName = "Server to client not built")]
    public async Task CipherNotBuiltInOneDirection_IsAnsweredDisconnect11AfterNewKeysWithNoKeys(string cipherClientToServer, string cipherServerToClient)
    {
        var log = new RecordingExchangeLog();
        using var client = new SshTestKeyExchangeClient(
            "ecdh-sha2-nistp256",
            "rsa-sha2-512",
            cipher: cipherClientToServer,
            cipherServerToClient: cipherServerToClient);
        var connection = new InMemoryConnection([client.InboundBytes()]);

        await Server(OfferWithAnUnbuiltCipher).ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        var disconnect = ServerDisconnectPacket(11, "Packet protection not implemented");
        CollectionAssert.AreEqual(disconnect, connection.WrittenBytes[^disconnect.Length..]);
        Assert.AreEqual(
            $"The SSH packet protection {cipherClientToServer}/{cipherServerToClient} is not built yet; the connection was ended after NEWKEYS.",
            log.Notes[2]);
    }

    [TestMethod]
    public void Create_MacNotBuiltBesideAnAesCtrCipher_IsNull()
    {
        var keys = new SshKeyDerivation(HashAlgorithmName.SHA256, BigInteger.One, new byte[32], new byte[32]);

        var protection = SshPacketProtection.Create("aes128-ctr", "hmac-sha1", keys, clientToServer: true);

        Assert.IsNull(protection);
    }

    [TestMethod]
    public void HmacForName_NoMacOrOneNotBuilt_IsNull()
    {
        Assert.IsNull(SshHmac.ForName(null));
        Assert.IsNull(SshHmac.ForName("hmac-md5"));
        Assert.AreEqual(new SshHmac(HashAlgorithmName.SHA512, 64, true), SshHmac.ForName("hmac-sha2-512-etm@openssh.com"));
    }

    [TestMethod]
    public void None_FramesTheWayRfc4253SaysBeforeNewKeys()
    {
        var none = SshPacketProtection.None;
        byte[] packet = [0, 0, 0, 12, 10, 21, .. new byte[10]];

        Assert.AreEqual(8, none.BlockSize);
        Assert.AreEqual(4, none.HeadLength);
        Assert.AreEqual(0, none.TagLength);
        Assert.AreSame(packet, none.Seal(0, packet));
    }

    [TestMethod]
    [DataRow(1, 10, DisplayName = "4 + 1 + 1 + 10 = 16 bytes aligned with packet_length")]
    [DataRow(11, 16, DisplayName = "4 + 1 + 11 would need 0; they get 16")]
    public void PaddingLengthFor_AesCtrWithEncryptAndMac_FillsSixteenByteBlocksWithTheLength(int payloadLength, int expected)
    {
        var protection = new SshCipherAndMacProtection(new SshAesCtr(new byte[16], new byte[16]), SshHmac.ForName("hmac-sha2-256")!, new byte[32]);

        Assert.AreEqual(expected, SshPacketWriter.PaddingLengthFor(payloadLength, protection));
    }
}
