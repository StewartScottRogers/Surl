using Surl.Protocol.Abstractions;
using static Surl.Protocol.Socks.SocksTestKit;

namespace Surl.Protocol.Socks;

/// <summary>
/// The SOCKS server against requests recorded from pinned upstream curl 8.21.0
/// (Fixtures/README.md; ADR-0121 decision 2).
/// </summary>
[TestClass]
public sealed class SocksProtocolServerTests
{
    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SocksProtocolServer(null!));
    }

    [TestMethod]
    public void Constructor_NullConnectorWithAccounts_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SocksProtocolServer(null!, new ScriptedProxyAccounts()));
    }

    [TestMethod]
    public void Schemes_AreTheFourSocksSchemes()
    {
        var server = new SocksProtocolServer(new InMemoryOriginConnector());

        CollectionAssert.AreEqual(new[] { "socks4", "socks4a", "socks5", "socks5h" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullConnection_Throws()
    {
        var server = new SocksProtocolServer(new InMemoryOriginConnector());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog())));
    }

    [TestMethod]
    public async Task ServeAsync_NullContext_Throws()
    {
        var server = new SocksProtocolServer(new InMemoryOriginConnector());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(ConnectionOf(), null!));
    }

    [TestMethod]
    [DataRow("socks4-connect", 9, "127.0.0.1")]
    [DataRow("socks4-user-id", 14, "127.0.0.1")]
    [DataRow("socks4a-host-name", 21, "origin.test")]
    public async Task ServeAsync_RecordedConnect_AnswersGrantedAndRelaysBothWays(string fixture, int requestBytes, string host)
    {
        var recorded = Fixture(fixture, "request.bin");
        var client = ConnectionOf(recorded[..requestBytes], recorded[requestBytes..]);
        var origin = ConnectionOf(Latin1Bytes(OriginResponse));
        var origins = new InMemoryOriginConnector().ScriptConnection(host, 8080, origin);

        await ServeAsync(client, origins);

        CollectionAssert.AreEqual(Granted.Concat(Latin1Bytes(OriginResponse)).ToArray(), client.WrittenBytes);
        CollectionAssert.AreEqual(recorded[requestBytes..], origin.WrittenBytes, "curl's GET reached the origin unchanged.");
        Assert.IsTrue(origin.WritesCompleted, "curl's half-close was passed on to the origin.");
        Assert.IsTrue(client.WritesCompleted, "The origin's half-close was passed on to curl.");
        Assert.AreEqual(TimeSpan.FromSeconds(30), origins.Requests.Single().Timeout, "The origin's connect has the head timeout.");
    }

    [TestMethod]
    public async Task ServeAsync_RequestAndTunnelledBytesInOneRead_ForwardsTheTunnelledBytes()
    {
        var recorded = Fixture("socks4-connect", "request.bin");
        var client = ConnectionOf(recorded);
        var origin = ConnectionOf(Latin1Bytes(OriginResponse));

        await ServeAsync(client, new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, origin));

        CollectionAssert.AreEqual(recorded[9..], origin.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedUserId_IsLoggedAndNotChecked()
    {
        var recorded = Fixture("socks4-user-id", "request.bin");
        var log = new RecordingExchangeLog();
        var origin = ConnectionOf(Latin1Bytes(OriginResponse));

        await ServeAsync(ConnectionOf(recorded), new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, origin), log);

        Assert.Contains("SOCKS4 user ID alice", log.Notes);
    }

    [TestMethod]
    [DataRow(OriginConnectionFailure.Refused)]
    [DataRow(OriginConnectionFailure.Unreachable)]
    [DataRow(OriginConnectionFailure.NotAllowed)]
    [DataRow(OriginConnectionFailure.HostNotFound)]
    [DataRow(OriginConnectionFailure.TimedOut)]
    public async Task ServeAsync_OriginNotConnected_AnswersRejectedAndCloses(OriginConnectionFailure failure)
    {
        var recorded = Fixture("socks4-rejected", "request.bin");
        var client = ConnectionOf(recorded);
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector().ScriptFailure("127.0.0.1", 8080, failure), log);

        CollectionAssert.AreEqual(Rejected, client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
        Assert.Contains($"Answered 5B: the origin 127.0.0.1:8080 could not be used ({failure}).", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionResets_EndsTheExchangeWithANote()
    {
        var client = new FailingConnection(ConnectionOf(), resetsInsteadOfClosing: true);
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector(), log);

        Assert.Contains("A connection failed mid-exchange; the exchange ended.", log.Notes);
    }
}
