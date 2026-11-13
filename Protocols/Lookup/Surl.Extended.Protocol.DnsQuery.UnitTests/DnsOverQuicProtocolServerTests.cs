using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.DnsQuery.DnsTestQueries;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The DNS server over QUIC through the datagram seam (ADR-0092 decision 5), over the in-memory
/// QUIC test connection (<see cref="DnsOverQuicTestClient"/>), whose ClientHello offers ALPN
/// <c>doq</c> and <c>h3-29</c>.
/// </summary>
[TestClass]
public sealed class DnsOverQuicProtocolServerTests
{
    private const ushort TypeA = 1;

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);

        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsOverQuicProtocolServer(null!, [], key, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsOverQuicProtocolServer(new DnsZoneSet(), null!, key, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsOverQuicProtocolServer(new DnsZoneSet(), [], null!, TimeSpan.Zero));
    }

    [TestMethod]
    public void Schemes_IsDns()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);

        CollectionAssert.AreEqual(new[] { "dns" }, new DnsOverQuicProtocolServer(new DnsZoneSet(), [], key, TimeSpan.Zero).Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        using var client = new DnsOverQuicTestClient();
        var server = Server(client);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(client.Flow, null!));
    }

    [TestMethod]
    public void Options_AreAdr0092sTransportParametersWithDoqOnly()
    {
        using var client = new DnsOverQuicTestClient();

        var options = Server(client, TimeSpan.FromSeconds(120)).Options(Context() with { Limits = ExchangeLimits.Default with { HeadTimeout = TimeSpan.FromSeconds(30) } });

        CollectionAssert.AreEqual(new[] { "doq" }, options.ApplicationProtocols.ToArray());
        Assert.AreEqual(100UL, options.InitialMaxStreamsBidi);
        Assert.AreEqual(0UL, options.InitialMaxStreamsUni);
        Assert.IsNull(options.MaxDatagramFrameSize);
        Assert.AreEqual(TimeSpan.FromSeconds(120), options.IdleTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(30), options.HandshakeTimeout);
    }

    [TestMethod]
    [DataRow(-1.0, DisplayName = "infinite")]
    [DataRow(60.0 * 24 * 60 * 60, DisplayName = "longer than a timer can wait")]
    public void Options_HeadTimeoutNoTimerCanWaitFor_IsNoHandshakeTimeout(double seconds)
    {
        using var client = new DnsOverQuicTestClient();
        var headTimeout = seconds < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(seconds);

        var options = Server(client).Options(Context() with { Limits = ExchangeLimits.Default with { HeadTimeout = headTimeout } });

        Assert.IsNull(options.HandshakeTimeout);
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_FirstInitial_SendsOneInitialHoldingConnectionCloseConnectionRefused(ConnectionRefusal refusal)
    {
        using var client = new DnsOverQuicTestClient();
        var server = Server(client);

        await server.WriteRefusalAsync(client.Flow, refusal, Token);

        client.Quic.Receive(client.Flow.SentDatagrams);
        Assert.AreEqual(0x02UL, client.TransportCloseCode);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_FirstDatagramNotAVersion1Initial_SendsNoBytes()
    {
        using var client = new DnsOverQuicTestClient();
        var server = Server(client);
        await using var flow = new InMemoryDatagramFlow(new byte[1200]);

        await server.WriteRefusalAsync(flow, ConnectionRefusal.TooManyConnections, Token);

        Assert.IsEmpty(flow.SentDatagrams);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullFlow_Throws()
    {
        using var client = new DnsOverQuicTestClient();
        var server = Server(client);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, Token).AsTask());
    }

    [TestMethod]
    public async Task ServeAsync_QueryOverDoq_IsAnsweredOnItsStream()
    {
        using var client = new DnsOverQuicTestClient();
        client.Watch(Server(client).ServeAsync(client.Flow, Context()));
        await client.CompleteHandshakeAsync(Token);

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0));
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual(IPAddress.Parse("192.0.2.80"), ((DnsAddressData)client.Answers(0).Single().Answers.Single().Data).Address);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledOnceServing_ClosesTheConnectionWithNoError()
    {
        using var client = new DnsOverQuicTestClient();
        using var exchange = new CancellationTokenSource();
        client.Watch(Server(client).ServeAsync(client.Flow, Context(exchange.Token)));
        await client.CompleteHandshakeAsync(Token);
        client.SendQuery(0, Query("web.example.test", TypeA, id: 0));
        await client.UntilAsync(() => client.HasEnded(0), Token);

        await exchange.CancelAsync();
        await client.FinishAsync(Token);

        Assert.AreEqual((ulong)DnsOverQuicErrorCode.NoError, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledBeforeTheHandshakeCompletes_StopsAtOnce()
    {
        using var client = new DnsOverQuicTestClient();
        using var exchange = new CancellationTokenSource();
        var serving = Server(client).ServeAsync(client.Flow, Context(exchange.Token));
        await client.UntilAsync(() => client.Quic.ClientFinished is not null, Token);

        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving.WaitAsync(TimeSpan.FromSeconds(30), Token));
    }

    private static DnsOverQuicProtocolServer Server(DnsOverQuicTestClient client, TimeSpan? idleTimeout = null) =>
        new(DnsTestZones.LoadExample(), [client.SelfSignedCertificate()], client.Key, idleTimeout ?? TimeSpan.Zero);

    private static ExchangeContext Context(CancellationToken cancellationToken = default) =>
        DnsOverQuicConnectionServerTests.Context(new RecordingExchangeLog(), cancellationToken);
}
