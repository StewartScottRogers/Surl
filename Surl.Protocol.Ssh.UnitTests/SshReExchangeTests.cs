using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Key re-exchange after the first <c>NEWKEYS</c> (RFC 4253 section 9; ADR-0051 decision 2.1),
/// started by the client's <c>KEXINIT</c> or by the server at its byte or time limit, and the
/// session going on under the new keys.
/// </summary>
[TestClass]
public sealed class SshReExchangeTests
{
    private const byte LocalExtensionMessage = 200;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("aes128-ctr", "hmac-sha2-256")]
    [DataRow("aes256-ctr", "hmac-sha2-512-etm@openssh.com")]
    [DataRow("aes256-gcm@openssh.com", "hmac-sha2-256")]
    public async Task ClientKexInitAfterNewKeys_ReKeysAndTheSessionGoesOnUnderTheNewKeys(string cipher, string mac)
    {
        var client = new SshTestTransportClient(cipher, mac, TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);
        client.Send([LocalExtensionMessage]);
        CollectionAssert.AreEqual(Concat([3], UInt32(0)), await client.ReceiveAsync());

        await client.ReExchangeAsync();
        client.Send([2]);
        client.Send([LocalExtensionMessage]);

        CollectionAssert.AreEqual(Concat([3], UInt32(1)), await client.ReceiveAsync(), "The sequence number starts again after NEWKEYS.");
        client.Connection.CloseClientWrites();
        await serving;
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH key re-exchange started by the client");
        Assert.HasCount(3, client.Log.Notes, "The re-exchange's negotiation is not noted again.");
    }

    [TestMethod]
    public async Task TwoReExchanges_EachGoOnUnderTheirOwnKeys()
    {
        var client = new SshTestTransportClient("aes128-gcm@openssh.com", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        await client.ReExchangeAsync();
        await client.ReExchangeAsync();
        client.Send([LocalExtensionMessage]);

        CollectionAssert.AreEqual(Concat([3], UInt32(0)), await client.ReceiveAsync());
        client.Connection.CloseClientWrites();
        await serving;
    }

    [TestMethod]
    public async Task ClientPastTheByteLimit_IsReKeyedByTheServerBeforeItsNextPacketIsRead()
    {
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256-etm@openssh.com", TestContext.CancellationToken);
        var serving = await client.OpenAsync(ServerWith(new SshReExchangeLimits(1, TimeSpan.FromHours(1))), TimeProvider.System);

        client.Send(Concat([2], String("the packet that passes the limit")));
        client.Send(Concat([2], String("skipped before the client's KEXINIT")));
        await client.ReExchangeAsync();
        client.Send([LocalExtensionMessage]);

        CollectionAssert.AreEqual(Concat([3], UInt32(0)), await client.ReceiveAsync());
        Assert.AreEqual(20, (await client.ReceiveAsync())[0], "The next packet passes the limit again.");
        client.Connection.CloseClientWrites();
        await serving;
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH key re-exchange started by the server");
    }

    [TestMethod]
    public async Task ServerPastTheByteLimit_ReKeysBeforeReadingOn()
    {
        var client = new SshTestTransportClient(
            "aes128-ctr",
            "hmac-sha2-256",
            TestContext.CancellationToken,
            macServerToClient: "hmac-sha2-512");
        var serving = await client.OpenAsync(ServerWith(new SshReExchangeLimits(60, TimeSpan.FromHours(1))), TimeProvider.System);

        client.Send([LocalExtensionMessage]);

        CollectionAssert.AreEqual(Concat([3], UInt32(0)), await client.ReceiveAsync(), "48 bytes read, 80 written.");
        await client.ReExchangeAsync();
        client.Connection.CloseClientWrites();
        await serving;
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH key re-exchange started by the server");
    }

    [TestMethod]
    public async Task KeysOlderThanTheInterval_AreReKeyedByTheServerBetweenPackets()
    {
        var clock = new ManualTimeProvider();
        var client = new SshTestTransportClient("aes256-gcm@openssh.com", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(ServerWith(new SshReExchangeLimits(long.MaxValue, TimeSpan.FromSeconds(10))), clock);
        client.Send([LocalExtensionMessage]);
        CollectionAssert.AreEqual(Concat([3], UInt32(0)), await client.ReceiveAsync());

        clock.Advance(TimeSpan.FromSeconds(10));
        client.Send([2]);
        await client.ReExchangeAsync();
        client.Send([LocalExtensionMessage]);

        CollectionAssert.AreEqual(Concat([3], UInt32(0)), await client.ReceiveAsync());
        client.Connection.CloseClientWrites();
        await serving;
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH key re-exchange started by the server");
    }

    [TestMethod]
    public async Task OtherMessageBeforeTheClientsKexInit_InAServerStartedReExchange_IsAnsweredDisconnect2()
    {
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(ServerWith(new SshReExchangeLimits(1, TimeSpan.FromHours(1))), TimeProvider.System);

        client.Send([2]);
        client.Send([LocalExtensionMessage]);

        Assert.AreEqual(20, (await client.ReceiveAsync())[0]);
        CollectionAssert.AreEqual(Concat([1], UInt32(2), String("Protocol error"), String(string.Empty)), await client.ReceiveAsync());
        await serving;
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "The client sent SSH message 200 during the key exchange.");
    }

    [TestMethod]
    public void Default_IsOneGibibyteOrOneHour()
    {
        Assert.AreEqual(new SshReExchangeLimits(1073741824, TimeSpan.FromHours(1)), SshReExchangeLimits.Default);
    }

    private static SshProtocolServer ServerWith(SshReExchangeLimits limits) => new(RsaHostKeys, RsaOffer, new FixedRandomSource(), limits);
}
