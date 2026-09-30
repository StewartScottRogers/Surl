using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Compression as upstream curl's <c>--compressed-ssh</c> asks for it (ADR-0051, decisions 2 and
/// 9; RFC 4253 section 6.2; OpenSSH <c>PROTOCOL</c> section 2.2), driven by
/// <see cref="SshTestTransportClient"/>, which compresses and inflates with its own zlib streams.
/// </summary>
[TestClass]
public sealed class SshCompressionTests
{
    // The first byte of a zlib stream (RFC 1950): deflate with a 32 KiB window.
    private const byte ZlibHeaderFirstByte = 0x78;

    private static readonly byte[] ServiceRequest = Concat([5], String("ssh-userauth"));

    private static readonly byte[] ServiceAccept = Concat([6], String("ssh-userauth"));

    private static readonly byte[] NoneLogin = Concat([50], String("alice"), String("ssh-connection"), String("none"));

    // A zlib stream's first piece with no bytes in it: the header alone.
    private static readonly byte[] ZlibHeaderAlone = [ZlibHeaderFirstByte, 0x9C];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(0L)]
    [DataRow(35000L)]
    public async Task Zlib_CompressesBothWaysFromNewKeys_AndRoundTripsEveryPacket(long maxMessageBytes)
    {
        var client = NewClient("zlib");
        var serving = await client.OpenAsync(Server(), TimeProvider.System, ExchangeLimits.Default with { MaxMessageBytes = maxMessageBytes });

        client.Send(ServiceRequest);
        CollectionAssert.AreEqual(ServiceAccept, await client.ReceiveAsync());
        Assert.AreEqual(ZlibHeaderFirstByte, client.LastReceivedWirePayload[0], "The server's first packet after NEWKEYS starts its zlib stream.");
        client.Send(NoneLogin);
        CollectionAssert.AreEqual(new byte[] { 52 }, await client.ReceiveAsync());
        AssertCompressedWithoutHeader(client);
        for (var round = 0; round < 3; round++)
        {
            client.Send([200, .. new byte[1000]]);
            CollectionAssert.AreEqual(Concat([3], UInt32((uint)(2 + round))), await client.ReceiveAsync());
            AssertCompressedWithoutHeader(client);
        }

        await CloseAsync(client, serving);
        Assert.IsTrue(client.Log.Notes.Any(note => note.Contains("compression zlib/zlib", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task DelayedZlib_StaysUncompressedUntilUserAuthSuccess_ThenCompressesBothWays()
    {
        var client = NewClient("zlib@openssh.com");
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        client.Send(ServiceRequest);
        CollectionAssert.AreEqual(ServiceAccept, await client.ReceiveAsync());
        CollectionAssert.AreEqual(ServiceAccept, client.LastReceivedWirePayload, "Before the login nothing is compressed.");
        client.Send(NoneLogin);
        CollectionAssert.AreEqual(new byte[] { 52 }, await client.ReceiveAsync());
        CollectionAssert.AreEqual(new byte[] { 52 }, client.LastReceivedWirePayload, "USERAUTH_SUCCESS itself is not compressed.");
        client.StartDelayedCompression();
        client.Send([200]);

        CollectionAssert.AreEqual(Concat([3], UInt32(2)), await client.ReceiveAsync());
        Assert.AreEqual(ZlibHeaderFirstByte, client.LastReceivedWirePayload[0], "The server's first packet after USERAUTH_SUCCESS starts its zlib stream.");
        await CloseAsync(client, serving);
        Assert.IsTrue(client.Log.Notes.Any(note => note.Contains("compression zlib@openssh.com/zlib@openssh.com", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("zlib")]
    [DataRow("zlib@openssh.com")]
    public async Task ReExchangeAfterTheLogin_StartsANewZlibStreamEachWay(string compression)
    {
        var client = NewClient(compression);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);
        client.Send(ServiceRequest);
        await client.ReceiveAsync();
        client.Send(NoneLogin);
        await client.ReceiveAsync();
        client.StartDelayedCompression();

        await client.ReExchangeAsync();
        client.Send([200]);

        CollectionAssert.AreEqual(Concat([3], UInt32(0)), await client.ReceiveAsync());
        Assert.AreEqual(ZlibHeaderFirstByte, client.LastReceivedWirePayload[0], "After NEWKEYS the server starts a new zlib stream.");
        await CloseAsync(client, serving);
    }

    [TestMethod]
    public async Task PayloadInflatingPastMaxMessageBytes_IsDisconnect6()
    {
        var client = NewClient("zlib");
        var serving = await client.OpenAsync(Server(), TimeProvider.System, ExchangeLimits.Default with { MaxMessageBytes = 32768 });

        // 16 MiB that deflates to about 16 KiB: a packet under the limit whose payload is 512 times over it.
        client.Send([2, .. new byte[16 * 1024 * 1024]]);

        await AssertDisconnect6Async(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "An SSH packet's compressed payload decompresses past the 32768-byte message limit.");
    }

    [TestMethod]
    public async Task PayloadThatIsNotZlib_IsDisconnect6()
    {
        var client = NewClient("zlib");
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        client.Connection.Send(client.SealWirePayload([1, 2, 3, 4, 5]));

        await AssertDisconnect6Async(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "An SSH packet's compressed payload of 5 bytes does not decompress.");
    }

    [TestMethod]
    public async Task PayloadThatInflatesToNothing_IsDisconnect6()
    {
        var client = NewClient("zlib");
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        client.Connection.Send(client.SealWirePayload(ZlibHeaderAlone));

        await AssertDisconnect6Async(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), $"An SSH packet's compressed payload of {ZlibHeaderAlone.Length} bytes decompresses to no message.");
    }

    private static void AssertCompressedWithoutHeader(SshTestTransportClient client) =>
        Assert.AreNotEqual(ZlibHeaderFirstByte, client.LastReceivedWirePayload[0], "The later packets continue the one zlib stream.");

    private static async Task AssertDisconnect6Async(SshTestTransportClient client, Task serving)
    {
        CollectionAssert.AreEqual(Concat([1], UInt32(6), String("Compression error"), String(string.Empty)), await client.ReceiveAsync());
        await serving;
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH disconnect sent: 6 Compression error");
    }

    private static async Task CloseAsync(SshTestTransportClient client, Task serving)
    {
        client.Connection.CloseClientWrites();
        await serving;
    }

    private SshTestTransportClient NewClient(string compression) =>
        new("aes128-ctr", "hmac-sha2-256", TestContext.CancellationToken, compression: compression);
}
