using Surl.Protocol.Abstractions;
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

    private const uint ClientChannel = 7;

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
    [DataRow(LocalExtensionMessage)]
    [DataRow((byte)7)]
    public async Task OtherMessageBeforeTheClientsKexInit_InAServerStartedReExchange_IsAnsweredAfterNewKeysWithItsSequenceNumber(byte messageNumber)
    {
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(ServerWith(new SshReExchangeLimits(1, TimeSpan.FromHours(1))), TimeProvider.System);

        client.Send([2]);
        client.Send([messageNumber]);
        client.Send([2]);
        var keyExchangePackets = await client.ReExchangeAsync();

        Assert.HasCount(3, keyExchangePackets, "KEXINIT, the method's reply and NEWKEYS, and nothing else.");
        CollectionAssert.AreEqual(Concat([3], UInt32(1)), await client.ReceiveAsync(), "The held message's own sequence number, from before NEWKEYS.");
        Assert.AreEqual(20, (await client.ReceiveAsync())[0], "The answer passes the limit again.");
        client.Connection.CloseClientWrites();
        await serving;
    }

    [TestMethod]
    [DataRow((byte)21)]
    [DataRow((byte)30)]
    [DataRow((byte)49)]
    public async Task KeyExchangeMessageBeforeTheClientsKexInit_InAServerStartedReExchange_IsAnsweredDisconnect2(byte messageNumber)
    {
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(ServerWith(new SshReExchangeLimits(1, TimeSpan.FromHours(1))), TimeProvider.System);

        client.Send([2]);
        client.Send([messageNumber]);

        Assert.AreEqual(20, (await client.ReceiveAsync())[0]);
        CollectionAssert.AreEqual(Concat([1], UInt32(2), String("Protocol error"), String(string.Empty)), await client.ReceiveAsync());
        await serving;
        CollectionAssert.Contains(client.Log.Notes.ToArray(), $"The client sent SSH message {messageNumber} before its KEXINIT in the key re-exchange.");
    }

    [TestMethod]
    public async Task MessagesPastTheHeldBytes_BeforeTheClientsKexInit_AreAnsweredDisconnect2()
    {
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(ServerWith(new SshReExchangeLimits(1, TimeSpan.FromHours(1), HeldBytes: 10)), TimeProvider.System);

        client.Send([2]);
        client.Send(Concat([LocalExtensionMessage], UInt32(0)));
        client.Send(Concat([LocalExtensionMessage], UInt32(1), [0]));

        Assert.AreEqual(20, (await client.ReceiveAsync())[0]);
        CollectionAssert.AreEqual(Concat([1], UInt32(2), String("Protocol error"), String(string.Empty)), await client.ReceiveAsync());
        await serving;
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "The client sent more than 10 bytes of messages before its KEXINIT in the key re-exchange.");
    }

    [TestMethod]
    public async Task ChannelDataBeforeTheClientsKexInit_WhenTheServerPassesItsByteLimitMidTransfer_IsDeliveredAndAnsweredUnderTheNewKeys()
    {
        var handlers = new SshTestChannelHandlers
        {
            Run = async (channel, cancellationToken) =>
            {
                var received = 0;
                var buffer = new byte[65536];
                int read;
                while ((read = await channel.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    received += read;
                }

                await channel.WriteAsync(Ascii($"got {received}"), cancellationToken);

                return 0;
            },
        };
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256", TestContext.CancellationToken);
        var server = new SshProtocolServer(RsaHostKeys, RsaOffer, new AnonymousAuthenticationPolicy(), new FixedRandomSource(), new SshReExchangeLimits(40000, TimeSpan.FromHours(1)), handlers);
        var serving = await client.OpenAsync(server, TimeProvider.System);
        await LogInAndStartScpAsync(client);

        client.Send(ChannelData(new byte[32768]));
        client.Send(ChannelData(new byte[32768]));
        var serverKexInit = await ReceiveSkippingWindowAdjustsAsync(client);
        client.Send(ChannelData(Ascii("sent before the client's KEXINIT")));
        var keyExchangePackets = await client.ReExchangeAsync(serverKexInit);
        client.Send(Concat([96], UInt32(0)));

        CollectionAssert.AreEqual(new byte[] { 20, 31, 21 }, keyExchangePackets.Select(packet => packet[0]).ToArray(), "Nothing but the key exchange between the server's KEXINIT and its NEWKEYS.");
        CollectionAssert.AreEqual(Concat([94], UInt32(ClientChannel), String("got 65568")), await ReceiveSkippingWindowAdjustsAsync(client));
        Assert.AreEqual(98, (await client.ReceiveAsync())[0], "exit-status");
        client.Connection.CloseClientWrites();
        await serving;
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH key re-exchange started by the server");
    }

    private static byte[] ChannelData(byte[] data) => Concat([94], UInt32(0), UInt32((uint)data.Length), data);

    private static async Task<byte[]> ReceiveSkippingWindowAdjustsAsync(SshTestTransportClient client)
    {
        while (true)
        {
            var payload = await client.ReceiveAsync();
            if (payload[0] != 93)
            {
                return payload;
            }
        }
    }

    private static async Task LogInAndStartScpAsync(SshTestTransportClient client)
    {
        client.Send(Concat([5], String("ssh-userauth")));
        CollectionAssert.AreEqual(Concat([6], String("ssh-userauth")), await client.ReceiveAsync());
        client.Send(Concat([50], String("alice"), String("ssh-connection"), String("none")));
        CollectionAssert.AreEqual(new byte[] { 52 }, await client.ReceiveAsync());
        client.Send(Concat([90], String("session"), UInt32(ClientChannel), UInt32(2097152), UInt32(32768)));
        Assert.AreEqual(91, (await client.ReceiveAsync())[0]);
        client.Send(Concat([98], UInt32(0), String("exec"), [0], String("scp -t /x")));
    }

    [TestMethod]
    public void Default_IsOneGibibyteOrOneHour()
    {
        Assert.AreEqual(new SshReExchangeLimits(1073741824, TimeSpan.FromHours(1)), SshReExchangeLimits.Default);
    }

    private static SshProtocolServer ServerWith(SshReExchangeLimits limits) => new(RsaHostKeys, RsaOffer, new AnonymousAuthenticationPolicy(), new FixedRandomSource(), limits);
}
