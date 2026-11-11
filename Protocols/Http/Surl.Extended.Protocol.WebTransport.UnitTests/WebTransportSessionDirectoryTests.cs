using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// Streams that name no open session (ADR-0117 decision 3; WTH3 4.6), over the in-memory QUIC test
/// connection: held until their session is settled, else reset and stopped.
/// </summary>
[TestClass]
public sealed class WebTransportSessionDirectoryTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task ServeStreamAsync_StreamBeforeItsConnect_IsHeldAndEchoedOnceTheSessionIsAccepted()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "70696E67"), fin: true);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);
        await client.UntilAsync(() => client.StreamSent(4).Fin, Token);

        Assert.AreEqual("70696E67", UnitTestWebTransport.SentHex(client, 4));
    }

    [TestMethod]
    public async Task ServeStreamAsync_UnidirectionalStreamWhoseSessionNeverComes_IsStoppedWithBufferedStreamRejectedAfterTheHeadTimeout()
    {
        using var client = new Http3TestQuicClient();
        var (log, time) = await UnitTestWebTransport.StartAsync(client, Token, openQpackStreams: false, limits: ExchangeLimits.Default with { HeadTimeout = TimeSpan.FromSeconds(5) });

        client.Send(Http3TestQuicClient.EncoderStreamId, Convert.FromHexString(UnitTestWebTransport.UnidirectionalHeaderHex + "78"));
        await UnitTestWebTransport.AdvanceUntilAsync(client, time, () => client.StopSendingCode(Http3TestQuicClient.EncoderStreamId) is not null, Token);

        Assert.AreEqual(WebTransportHttp3Codes.BufferedStreamRejected, client.StopSendingCode(Http3TestQuicClient.EncoderStreamId));
        Assert.Contains("Stream 6 names session 0, which is not open; it was reset and stopped with 0x3994bd84.", log.Notes);
    }

    [TestMethod]
    public async Task ServeStreamAsync_StreamWhoseConnectWasRefused_IsResetWithBufferedStreamRejected()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"));
        await UnitTestWebTransport.ConnectAsync(client, Token, "/elsewhere");
        await client.UntilAsync(() => client.ResetCode(4) is not null && client.StopSendingCode(4) is not null, Token);

        Assert.AreEqual(WebTransportHttp3Codes.BufferedStreamRejected, client.ResetCode(4));
        Assert.AreEqual(WebTransportHttp3Codes.BufferedStreamRejected, client.StopSendingCode(4));
    }

    [TestMethod]
    public async Task ServeStreamAsync_StreamNamingAnOrdinaryRequest_IsResetWithBufferedStreamRejected()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);
        client.Send(0, client.Request("GET", "/echo"), fin: true);
        await client.UntilAsync(() => client.Response(0).Fin, Token);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"));
        await client.UntilAsync(() => client.ResetCode(4) is not null, Token);

        Assert.AreEqual(WebTransportHttp3Codes.BufferedStreamRejected, client.ResetCode(4));
    }

    [TestMethod]
    public async Task ServeStreamAsync_StreamNamingAnEndedSession_IsResetWithSessionGone()
    {
        using var client = new Http3TestQuicClient();
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);
        client.Send(0, [], fin: true);
        await client.UntilAsync(() => client.StreamSent(0).Fin && log.Notes.Contains("WebTransport session 0 has ended."), Token);
        // Surl.Http3 forgets the session just after its handler returns.
        await Task.Delay(100, Token);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"));
        await client.UntilAsync(() => client.ResetCode(4) is not null && client.StopSendingCode(4) is not null, Token);

        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.ResetCode(4));
        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.StopSendingCode(4));
    }

    [TestMethod]
    public async Task ServeStreamAsync_SeventeenthStreamWaiting_IsResetAtOnce()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);

        var streamIds = Enumerable.Range(1, WebTransportSessionDirectory.HeldStreamCapacity + 1).Select(index => (ulong)index * 4).ToList();
        foreach (var streamId in streamIds)
        {
            client.Send(streamId, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"));
        }

        await client.UntilAsync(() => streamIds.Any(streamId => client.ResetCode(streamId) is not null), Token);

        Assert.AreEqual(1, streamIds.Count(streamId => client.ResetCode(streamId) == WebTransportHttp3Codes.BufferedStreamRejected), "16 wait for session 0; the 17th is refused.");
    }

    [TestMethod]
    public void TryOpenSession_AtTheLimitThenAfterASessionEnds_IsRefusedThenAllowed()
    {
        var context = new ExchangeContext(1, new ListenUrl("webtransport", "127.0.0.1", 1), new System.Net.IPEndPoint(0, 1), new System.Net.IPEndPoint(0, 2), new RecordingExchangeLog(), TimeProvider.System, CancellationToken.None);
        var directory = new WebTransportSessionDirectory(context);
        var first = directory.TryOpenSession(1);

        var atLimit = directory.TryOpenSession(1);
        directory.End(0);
        var afterEnd = directory.TryOpenSession(1);

        Assert.IsTrue(first);
        Assert.IsFalse(atLimit, "ADR-0117 decision 5.");
        Assert.IsTrue(afterEnd);
    }
}
