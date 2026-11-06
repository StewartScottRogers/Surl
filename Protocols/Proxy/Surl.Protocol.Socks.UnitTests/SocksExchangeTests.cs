using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Socks.SocksTestKit;

namespace Surl.Protocol.Socks;

/// <summary>
/// The SOCKS exchange's answers to requests curl's client never sends but a peer may: other
/// versions, other commands, refused targets, over-long names, truncated and stalled requests
/// (ADR-0121 decisions 2, 5 and 6).
/// </summary>
[TestClass]
public sealed class SocksExchangeTests
{
    private static readonly byte[] Loopback = [127, 0, 0, 1];

    [TestMethod]
    public async Task ServeAsync_Socks5Greeting_AnswersNoAcceptableMethodAndCloses()
    {
        var client = ConnectionOf([0x05, 0x02, 0x00, 0x01]);

        await ServeAsync(client, new InMemoryOriginConnector());

        CollectionAssert.AreEqual(new byte[] { 0x05, 0xFF }, client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
    }

    [TestMethod]
    [DataRow("socks5")]
    [DataRow("socks5h")]
    [DataRow("SOCKS5H")]
    public async Task ServeAsync_Socks4RequestOnASocks5Listener_AnswersRejectedAndCloses(string scheme)
    {
        var client = HeldOpenConnectionOf(Socks4Request(0x01, 8080, Loopback, string.Empty));
        var origins = new InMemoryOriginConnector();
        var log = new RecordingExchangeLog();

        await ServeAsync(client, origins, scheme, log: log);

        CollectionAssert.AreEqual(Rejected, client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
        Assert.IsEmpty(origins.Requests);
        Assert.Contains("Answered 5B: a SOCKS4 request on a SOCKS5 listener.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_Socks4RequestToAListenerWithAccounts_AnswersRejected()
    {
        var client = ConnectionOf(Socks4Request(0x01, 8080, Loopback, "alice"));
        var origins = new InMemoryOriginConnector();
        var log = new RecordingExchangeLog();

        await ServeAsync(client, origins, "socks4", new ScriptedProxyAccounts(), log);

        CollectionAssert.AreEqual(Rejected, client.WrittenBytes);
        Assert.IsEmpty(origins.Requests);
        Assert.Contains("Answered 5B: the listener has proxy accounts, which SOCKS4 cannot prove.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_Socks4RequestToAListenerWithAccountsAndAllowAnonymous_IsConnected()
    {
        var origin = ConnectionOf();
        var client = ConnectionOf(Socks4Request(0x01, 8080, Loopback, string.Empty));
        var accounts = new ScriptedProxyAccounts(anonymous: PasswordLoginVerdict.AcceptedUnchecked);

        await ServeAsync(client, new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, origin), "socks4a", accounts);

        CollectionAssert.AreEqual(Granted, client.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_ClosedBeforeAnyByte_WritesNothing()
    {
        var client = ConnectionOf();
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector(), log);

        Assert.IsEmpty(client.WrittenBytes);
        Assert.Contains("curl closed the connection before sending a SOCKS version.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_FirstByteIsNoVersion_ClosesWithoutAReply()
    {
        var client = ConnectionOf(Latin1Bytes("GET / HTTP/1.1\r\n\r\n"));
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector(), log);

        Assert.IsEmpty(client.WrittenBytes);
        Assert.Contains("The first byte 0x47 is no SOCKS version; the connection closes without a reply.", log.Notes);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(8)]
    [DataRow(15)]
    public async Task ServeAsync_RequestEndsEarly_ClosesWithoutAReply(int bytesSent)
    {
        byte[] socks4a = [.. Socks4Request(0x01, 8080, [0, 0, 0, 1], "alice"), .. Latin1Bytes("origin.test"), 0x00];
        var client = ConnectionOf(socks4a[..bytesSent]);
        var origins = new InMemoryOriginConnector();

        await ServeAsync(client, origins);

        Assert.IsEmpty(client.WrittenBytes);
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_UserIdOf255Bytes_IsConnected()
    {
        var origin = ConnectionOf();
        var client = ConnectionOf(Socks4Request(0x01, 8080, Loopback, new string('u', 255)));

        await ServeAsync(client, new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, origin));

        CollectionAssert.AreEqual(Granted, client.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_UserIdPast255Bytes_AnswersRejectedWithoutReadingFurther()
    {
        var client = HeldOpenConnectionOf(Socks4Request(0x01, 8080, Loopback, new string('u', 256))[..^1]);
        var origins = new InMemoryOriginConnector();
        var log = new RecordingExchangeLog();

        await ServeAsync(client, origins, log);

        CollectionAssert.AreEqual(Rejected, client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
        Assert.IsEmpty(origins.Requests);
        Assert.Contains("Answered 5B: the user ID or host name runs past 255 bytes.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_Socks4aHostNamePast255Bytes_AnswersRejected()
    {
        var client = ConnectionOf([.. Socks4Request(0x01, 8080, [0, 0, 0, 1], string.Empty), .. Latin1Bytes(new string('h', 256)), 0x00]);
        var origins = new InMemoryOriginConnector();

        await ServeAsync(client, origins);

        CollectionAssert.AreEqual(Rejected, client.WrittenBytes);
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    [DataRow((byte)0x02, 8080, new byte[] { 127, 0, 0, 1 }, "", "the command 0x02 is not CONNECT")]
    [DataRow((byte)0x01, 8080, new byte[] { 0, 0, 0, 0 }, "", "the address 0.0.0.0 is not allowed (NotAllowed)")]
    [DataRow((byte)0x01, 0, new byte[] { 127, 0, 0, 1 }, "", "port 0 cannot be connected (NotAllowed)")]
    [DataRow((byte)0x01, 8080, new byte[] { 0, 0, 0, 1 }, "\0", "the SOCKS4a host name is empty (HostNotFound)")]
    public async Task ServeAsync_RequestRefusedBeforeConnecting_AnswersRejectedAndTriesNoOrigin(byte command, int port, byte[] address, string trailing, string why)
    {
        var client = ConnectionOf(Socks4Request(command, port, address, string.Empty, Latin1Bytes(trailing)));
        var origins = new InMemoryOriginConnector();
        var log = new RecordingExchangeLog();

        await ServeAsync(client, origins, log);

        CollectionAssert.AreEqual(Rejected, client.WrittenBytes);
        Assert.IsEmpty(origins.Requests);
        Assert.Contains($"Answered 5B: {why}.", log.Notes);
    }

    [TestMethod]
    public void RefusalBeforeConnecting_ConnectToAnOrigin_IsNull()
    {
        var request = new Socks4Request(Socks4Request.ConnectCommand, 8080, IPAddress.Loopback, string.Empty, null);

        Assert.IsNull(SocksExchange.RefusalBeforeConnecting(request));
    }

    [TestMethod]
    public async Task ServeAsync_RequestStallsPastTheHeadTimeout_ClosesWithoutAReply()
    {
        var clock = new ManualTimeProvider(Now);
        var client = HeldOpenConnectionOf([0x04, 0x01, 0x1F]);
        var log = new RecordingExchangeLog();
        var serving = new SocksProtocolServer(new InMemoryOriginConnector()).ServeAsync(client, Context(log, clock));

        while (clock.ActiveTimerCount == 0)
        {
            await Task.Delay(1);
        }

        clock.Advance(TimeSpan.FromSeconds(30));
        await serving;

        Assert.IsEmpty(client.WrittenBytes);
        Assert.Contains("The SOCKS4 request was not complete within the head timeout; the connection closes without a reply.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledMidRequest_LetsTheCancellationThrough()
    {
        using var cancellation = new CancellationTokenSource();
        var clock = new ManualTimeProvider(Now);
        var client = HeldOpenConnectionOf([0x04, 0x01]);
        var serving = new SocksProtocolServer(new InMemoryOriginConnector()).ServeAsync(client, Context(new RecordingExchangeLog(), clock, cancellation.Token));

        while (clock.ActiveTimerCount == 0)
        {
            await Task.Delay(1);
        }

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task ServeAsync_OriginResetsDuringTheRelay_AbortsBothConnections()
    {
        var client = HeldOpenConnectionOf(Socks4Request(0x01, 8080, Loopback, string.Empty));
        var origin = new FailingConnection(ConnectionOf(), resetsInsteadOfClosing: true);
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, origin), log);

        Assert.IsTrue(client.Aborted);
        Assert.Contains("The relay to 127.0.0.1:8080 failed; both connections were aborted.", log.Notes);
    }
}
