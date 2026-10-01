using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Rtsp.RtspServerHarness;

namespace Surl.Protocol.Rtsp;

[TestClass]
public sealed class RtspLimitTests
{
    private const string UnreadableBody = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nTransfer-Encoding: chunked\r\n\r\n";

    private static readonly TimeSpan HeadTimeout = ExchangeLimits.Default.HeadTimeout;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PartialHead_AfterTheHeadTimeout_Is408WithNoCSeqAndCloses()
    {
        var clock = new ManualTimeProvider(Now - HeadTimeout);
        var connection = new InMemoryConnection([Ascii("OPTIONS * RTSP/1.0\r\nCSeq:")], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        clock.Advance(HeadTimeout - TimeSpan.FromSeconds(1));
        Assert.IsFalse(serving.IsCompleted, "One second of the head timeout is left.");
        clock.Advance(TimeSpan.FromSeconds(1));
        await AdvanceUntilCompletedAsync(clock, serving);

        Assert.AreEqual(ResponseHead("408 Request Time-out", null), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("RTSP request refused: 408 Request Time-out: no request head was read (HeadTimedOut)", log.Notes[0]);
        Assert.AreEqual("Stopped draining the unread request bytes at the 1-second drain limit.", log.Notes[1]);
    }

    [TestMethod]
    public async Task NoBytes_AfterTheHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        clock.Advance(HeadTimeout);
        await serving;

        Assert.IsEmpty(connection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { "No request head was read: HeadTimedOutBeforeAnyByte; closed with no bytes." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task NextHead_IsTimedFromItsFirstByte()
    {
        var clock = new ManualTimeProvider(Now - HeadTimeout);
        var connection = new InMemoryConnection([RecordedRequest("options-plain"), Ascii("OPTIONS *")], peerHalfClosesWhenExhausted: false);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, TestContext.CancellationToken));

        Assert.AreEqual(1, clock.ActiveTimerCount, "The second head's first bytes started its timer.");
        clock.Advance(HeadTimeout);
        await AdvanceUntilCompletedAsync(clock, serving);

        var written = Latin1(connection.WrittenBytes);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 1\r\nDate: Mon, 28 Sep 2026 11:59:30 GMT\r\n", written, "The first request was answered.");
        Assert.EndsWith("\r\n\r\n" + ResponseHead("408 Request Time-out", null), written);
    }

    [TestMethod]
    public async Task Drain_ClientSendsMoreThanTheByteLimit_StopsAtTheByteLimit()
    {
        var body = new byte[RtspUnreadRequestDrainer.MaxDrainBytes];

        var (connection, log) = await ServeAsync([Ascii(UnreadableBody), body, Ascii("left")], TestContext.CancellationToken);

        Assert.AreEqual("left", Latin1(await ReadWhatIsLeftAsync(connection, TestContext.CancellationToken)));
        Assert.AreEqual("Stopped draining the unread request bytes at the 1048576-byte drain limit.", log.Notes[1]);
    }

    [TestMethod]
    public async Task Drain_ClientNeverHalfCloses_StopsAtTheTimeLimit()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([Ascii(UnreadableBody), new byte[100]], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        await WaitForTimersAsync(clock, 1);
        clock.Advance(RtspUnreadRequestDrainer.MaxDrainTime - TimeSpan.FromTicks(1));
        Assert.IsFalse(serving.IsCompleted, "One tick of the drain's time limit is left.");
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        Assert.AreEqual(1, RtspUnreadRequestDrainer.MaxDrainTime.TotalSeconds);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("Stopped draining the unread request bytes at the 1-second drain limit.", log.Notes[1]);
    }

    [TestMethod]
    public async Task Drain_ConnectionFailsWhileDraining_NotesItAndEnds()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([Ascii(UnreadableBody)], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        await WaitForTimersAsync(clock, 1);
        connection.Abort();
        await serving;

        Assert.AreEqual("The connection failed while its unread request bytes were drained (The connection was aborted.).", log.Notes[1]);
    }

    [TestMethod]
    public async Task Drain_ExchangeCancelledWhileDraining_Throws()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([Ascii(UnreadableBody)], peerHalfClosesWhenExhausted: false);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, exchange.Token));

        await WaitForTimersAsync(clock, 1);
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task RefusalThatCannotBeWritten_IsGivenUpAfterOneSecondWithoutAbort()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new StalledWriteConnection(Ascii(UnreadableBody));
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        Assert.AreEqual(1, clock.ActiveTimerCount, "Only the refusal's write deadline is running.");
        clock.Advance(TimeSpan.FromSeconds(1));
        await serving;

        Assert.IsFalse(connection.Aborted);
        Assert.IsFalse(connection.WritesCompleted, "The close is left to the engine's dispose.");
        Assert.AreEqual("The 400 was not written within its 1-second write deadline; the connection is closed without it.", log.Notes[1]);
    }

    [TestMethod]
    public async Task ExchangeCancelledWhileARefusalIsWritten_Throws()
    {
        var connection = new StalledWriteConnection(Ascii(UnreadableBody));
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), exchange.Token));

        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_Writes503WithNoCSeqAndHalfCloses(ConnectionRefusal refusal)
    {
        var connection = new InMemoryConnection([]);

        await Server().WriteRefusalAsync(connection, refusal, TestContext.CancellationToken);

        Assert.AreEqual("RTSP/1.0 503 Service Unavailable\r\nServer: surl\r\n\r\n", Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public void Schemes_IsRtspOnly()
    {
        CollectionAssert.AreEqual(new[] { "rtsp" }, Server().Schemes.ToArray());
    }

    [TestMethod]
    public async Task NullArguments_AreRefused()
    {
        var connection = new InMemoryConnection([]);
        var context = Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentNullException>(() => new RtspProtocolServer(null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Server().ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Server().ServeAsync(connection, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Server().WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ServeAsync_UsesTheConnectionsLocalEndPointNotTheContexts()
    {
        var (connection, _) = await ServeAsync(
            [Ascii("DESCRIBE rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 1\r\n\r\n")],
            TestContext.CancellationToken,
            localEndPoint: new IPEndPoint(IPAddress.Parse("192.0.2.9"), 554));

        Assert.Contains("\r\no=- 0 0 IN IP4 192.0.2.9\r\n", Latin1(connection.WrittenBytes));
    }
}
