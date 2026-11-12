using System.Net;
using System.Security.Cryptography;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>The server <c>surl</c> registers for <c>webtransport</c> (BL-461): its scheme, its arguments, the certificate it waits for and the connections it serves.</summary>
[TestClass]
public sealed class WebTransportProtocolServerTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public void Schemes_IsWebTransportOnly()
    {
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero);

        CollectionAssert.AreEqual(new[] { "webtransport" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ConstructorAndMethods_NullArguments_Throw()
    {
        var policy = new AnonymousAuthenticationPolicy();
        var server = new WebTransportProtocolServer(policy, () => null, TimeSpan.Zero);
        using var client = new Http3TestQuicClient();

        Assert.ThrowsExactly<ArgumentNullException>(() => new WebTransportProtocolServer(null!, () => null, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WebTransportProtocolServer(policy, null!, TimeSpan.Zero));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, NewContext(new ManualTimeProvider(Now), Token)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(client.Flow, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, Token).AsTask());
    }

    [TestMethod]
    public async Task ServeAsync_NoCertificateYet_ReturnsAtOnceAndSendsNothing()
    {
        using var client = new Http3TestQuicClient();
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero);

        await server.ServeAsync(client.Flow, NewContext(new ManualTimeProvider(Now), Token)).WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.IsEmpty(client.Flow.SentDatagrams);
    }

    [TestMethod]
    public async Task ServeAsync_CertificateServed_AcceptsAWebTransportSessionOnAnyPath()
    {
        using var client = new Http3TestQuicClient();
        var serving = StartServing(client, NewContext(new ManualTimeProvider(Now), Token), TimeSpan.FromSeconds(30));
        await client.OpenAsync(Token, UnitTestWebTransport.WebTransportSettings);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/any/path");

        Assert.AreEqual("200", response.Status);
        Assert.IsFalse(serving.IsCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_ClientsFourthUnidirectionalStream_IsTakenAndAnswered()
    {
        using var client = new Http3TestQuicClient();
        var serving = StartServing(client, NewContext(new ManualTimeProvider(Now), Token), TimeSpan.FromSeconds(30));
        await client.OpenAsync(Token, UnitTestWebTransport.WebTransportSettings);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(14, Convert.FromHexString(UnitTestWebTransport.UnidirectionalHeaderHex + "64617461"), fin: true);
        await client.UntilAsync(() => client.StreamSent(UnitTestWebTransport.ServerAnswerStreamId).Fin, Token);

        Assert.AreEqual(UnitTestWebTransport.UnidirectionalHeaderHex + "64617461", UnitTestWebTransport.SentHex(client, UnitTestWebTransport.ServerAnswerStreamId), "The control and two QPACK streams are the client's first three; a session's stream is its fourth.");
        Assert.IsFalse(serving.IsCompleted);
    }

    [TestMethod]
    [DataRow(-1, DisplayName = "No head timeout")]
    [DataRow(0, DisplayName = "A head timeout longer than a timer can wait")]
    [DataRow(30, DisplayName = "A head timeout")]
    public async Task ServeAsync_AnyHeadTimeout_StillCompletesTheHandshake(int seconds)
    {
        using var client = new Http3TestQuicClient();
        var headTimeout = seconds switch
        {
            -1 => Timeout.InfiniteTimeSpan,
            0 => TimeSpan.FromDays(60),
            _ => TimeSpan.FromSeconds(seconds),
        };
        var context = NewContext(new ManualTimeProvider(Now), Token) with { Limits = ExchangeLimits.Default with { HeadTimeout = headTimeout } };
        _ = StartServing(client, context, TimeSpan.Zero);
        await client.OpenAsync(Token, UnitTestWebTransport.WebTransportSettings);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token);

        Assert.AreEqual("200", response.Status);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledBeforeTheHandshakeCompletes_StopsTheConnection()
    {
        using var client = new Http3TestQuicClient();
        using var exchange = new CancellationTokenSource();
        var serving = StartServing(client, NewContext(new ManualTimeProvider(Now), exchange.Token), TimeSpan.Zero);

        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving.WaitAsync(TimeSpan.FromSeconds(30), Token));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledWhileServing_LeavesTheEndingToTheHttp3Server()
    {
        using var client = new Http3TestQuicClient();
        using var exchange = new CancellationTokenSource();
        var time = new ManualTimeProvider(Now);
        var serving = StartServing(client, NewContext(time, exchange.Token), TimeSpan.Zero);
        await client.OpenAsync(Token, UnitTestWebTransport.WebTransportSettings);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        await exchange.CancelAsync();
        await UnitTestWebTransport.AdvanceUntilAsync(client, time, () => client.CloseCode is not null, Token);
        time.Advance(TimeSpan.FromMinutes(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), Token);

        Assert.AreEqual(0x100UL, client.CloseCode);
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_AVersion1Initial_SendsOneInitialPacket(ConnectionRefusal refusal)
    {
        using var client = new Http3TestQuicClient();
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero);

        await server.WriteRefusalAsync(client.Flow, refusal, Token);

        Assert.HasCount(1, client.Flow.SentDatagrams);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_FirstDatagramNotAVersion1Initial_SendsNoBytes()
    {
        using var client = new Http3TestQuicClient();
        var first = client.Quic.InitialDatagrams[0].ToArray();
        first[4] = 0x02;
        await using var flow = new InMemoryDatagramFlow(first);
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => null, TimeSpan.Zero);

        await server.WriteRefusalAsync(flow, ConnectionRefusal.TooManyConnections, Token);

        Assert.IsEmpty(flow.SentDatagrams);
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static ExchangeContext NewContext(TimeProvider time, CancellationToken token) => new(
        1,
        new ListenUrl("webtransport", "127.0.0.1", 18050).WithBoundPort(18050),
        new IPEndPoint(IPAddress.Loopback, 18050),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        time,
        token);

    private static Task StartServing(Http3TestQuicClient client, ExchangeContext context, TimeSpan idleTimeout)
    {
        var certificate = new WebTransportServerCertificate([client.SelfSignedCertificate()], client.Key);
        var server = new WebTransportProtocolServer(new AnonymousAuthenticationPolicy(), () => certificate, idleTimeout);
        return server.ServeAsync(client.Flow, context);
    }
}
