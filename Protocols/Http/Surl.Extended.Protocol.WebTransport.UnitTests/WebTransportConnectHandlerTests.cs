using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// How an extended CONNECT is answered (ADR-0117 decisions 0 and 1; WTH3 3.1 and 3.2), over the
/// in-memory QUIC test connection, every client byte written from the specifications.
/// </summary>
[TestClass]
public sealed class WebTransportConnectHandlerTests
{
    private const ulong H3RequestIncomplete = 0x10d;
    private const ulong H3RequestRejected = 0x10b;

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task ServeSessionAsync_ConnectToTheAcceptedPath_IsAnswered200AndTheStreamStaysOpen()
    {
        using var client = new Http3TestQuicClient();
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token);

        Assert.AreEqual("200", response.Status);
        Assert.HasCount(1, response.Fields, "WTH3 3.2: 200 and no other field.");
        Assert.IsFalse(response.Fin, "A 2xx establishes the session; the CONNECT stream stays open.");
        Assert.Contains("WebTransport session 0 on /echo was accepted.", log.Notes);
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectBelowTheAcceptedPathFromTheOwnOrigin_IsAnswered200()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/echo/room?x=1", ("origin", "https://127.0.0.1:18050"));

        Assert.AreEqual("200", response.Status);
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectToAnotherPath_IsAnswered405WithAllowConnectAndFin()
    {
        using var client = new Http3TestQuicClient();
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/echoes");
        await client.UntilAsync(() => client.Response(0).Fin, Token);

        Assert.AreEqual("405", response.Status);
        CollectionAssert.AreEqual(new[] { "allow: CONNECT" }, response.RegularFields);
        Assert.IsEmpty(client.Response(0).Body);
        Assert.Contains("WebTransport CONNECT 0 to /echoes was answered 405.", log.Notes);
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectFromAnotherOrigin_IsAnswered403AndFin()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/echo", ("origin", "https://elsewhere.example"));
        await client.UntilAsync(() => client.Response(0).Fin, Token);

        Assert.AreEqual("403", response.Status);
        Assert.IsEmpty(response.RegularFields);
    }

    [TestMethod]
    [DataRow("AC7CF00001", DisplayName = "No SETTINGS_H3_DATAGRAM")]
    [DataRow("3301", DisplayName = "No SETTINGS_WT_ENABLED")]
    [DataRow("3300AC7CF00001", DisplayName = "SETTINGS_H3_DATAGRAM = 0")]
    public async Task ServeSessionAsync_ClientSettingsLackARequirement_ResetsTheConnectWithRequirementsNotMet(string settingsHex)
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token, clientSettings: UnitTestWebTransport.Settings(settingsHex));

        await UnitTestWebTransport.ConnectAsync(client, Token);
        await client.UntilAsync(() => client.StopSendingCode(0) is not null, Token);

        Assert.AreEqual(WebTransportHttp3Codes.RequirementsNotMet, client.ResetCode(0));
        Assert.AreEqual(WebTransportHttp3Codes.RequirementsNotMet, client.StopSendingCode(0));
    }

    [TestMethod]
    public async Task ServeSessionAsync_ClientSettingsNeverArrive_ResetsTheConnectWithRequestIncompleteAfterTheHeadTimeout()
    {
        using var client = new Http3TestQuicClient();
        var (_, time) = await UnitTestWebTransport.StartAsync(client, Token, openControlStream: false, limits: ExchangeLimits.Default with { HeadTimeout = TimeSpan.FromSeconds(5) });

        client.Send(0, UnitTestWebTransport.Connect(client));
        await UnitTestWebTransport.AdvanceUntilAsync(client, time, () => client.ResetCode(0) is not null, Token);

        Assert.AreEqual(H3RequestIncomplete, client.ResetCode(0));
        Assert.IsEmpty(client.StreamSent(0).Data, "WTH3 3.1: nothing is answered before the client's SETTINGS.");
    }

    [TestMethod]
    public async Task ServeSessionAsync_ConnectBeforeTheClientSettings_IsAnsweredOnceTheyArrive()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token, openControlStream: false);

        client.Send(0, UnitTestWebTransport.Connect(client));
        client.Send(Http3TestQuicClient.ControlStreamId, [0x00, .. UnitTestWebTransport.WebTransportSettings]);
        await client.UntilAsync(() => client.StreamSent(0).Data.Length > 0, Token);

        Assert.AreEqual("200", client.Response(0).Status);
    }

    [TestMethod]
    public async Task ServeSessionAsync_SecondSessionWithoutFlowControl_IsResetRequestRejected()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(4, UnitTestWebTransport.Connect(client));
        await client.UntilAsync(() => client.ResetCode(4) is not null && client.StopSendingCode(4) is not null, Token);

        Assert.AreEqual(H3RequestRejected, client.ResetCode(4), "WTH3 5.1: one session at a time without flow control.");
        Assert.AreEqual(H3RequestRejected, client.StopSendingCode(4));
        Assert.IsEmpty(client.StreamSent(4).Data);
    }

    [TestMethod]
    public async Task ServeSessionAsync_SeventeenthSessionWithFlowControl_IsResetRequestRejected()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token, clientSettings: UnitTestWebTransport.FlowControlSettings());

        for (ulong stream = 0; stream < 64; stream += 4)
        {
            client.Send(stream, UnitTestWebTransport.Connect(client));
        }

        await client.UntilAsync(() => Enumerable.Range(0, 16).All(index => client.StreamSent((ulong)index * 4).Data.Length > 0), Token);
        client.Send(64, UnitTestWebTransport.Connect(client));
        await client.UntilAsync(() => client.ResetCode(64) is not null, Token);

        Assert.IsTrue(Enumerable.Range(0, 16).All(index => client.Response((ulong)index * 4).Status == "200"), "WTH3 5.2: 16 sessions at a time with flow control.");
        Assert.AreEqual(H3RequestRejected, client.ResetCode(64));
    }

    [TestMethod]
    public async Task ServeSessionAsync_SessionEnded_FreesItsPlaceForTheNextConnect()
    {
        using var client = new Http3TestQuicClient();
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);
        client.Send(0, [], fin: true);
        await client.UntilAsync(() => log.Notes.Contains("WebTransport session 0 has ended."), Token);

        client.Send(4, UnitTestWebTransport.Connect(client));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length > 0, Token);

        Assert.AreEqual("200", client.Response(4).Status);
    }

    [TestMethod]
    public async Task ServeSessionAndServeStreamWithoutSession_NullArguments_Throw()
    {
        var context = new ExchangeContext(1, new ListenUrl("webtransport", "127.0.0.1", 1), new System.Net.IPEndPoint(0, 1), new System.Net.IPEndPoint(0, 2), new RecordingExchangeLog(), TimeProvider.System, CancellationToken.None);
        var handler = new WebTransportConnectHandler("/", new WebTransportConnectAuthentication(new AnonymousAuthenticationPolicy().StartHttpConnection(null)), WebTransportFlowControlSettings.Default, new WebTransportSessionDirectory(context), context);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => handler.ServeSessionAsync(null!, Token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => handler.ServeStreamWithoutSessionAsync(null!, Token));
        Assert.AreEqual("webtransport-h3", handler.Protocol);
        Assert.AreEqual(0x41UL, handler.BidirectionalStreamSignal);
        Assert.AreEqual(0x54UL, handler.UnidirectionalStreamType);
    }
}
