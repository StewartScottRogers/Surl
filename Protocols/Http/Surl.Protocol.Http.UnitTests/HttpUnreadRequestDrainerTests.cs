using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpUnreadRequestDrainerTests
{
    private const string RefusedPostHead = "POST /file.txt HTTP/1.1\r\nHost: h\r\nContent-Length: 2000000\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DrainAsync_RecordedRefusedPost_ReadsTheBodyBeforeTheConnectionIsDisposed()
    {
        var (head, body) = SplitAfterHead(RecordedFixture.ReadRequestBytes("post-refused-405"));

        var (connection, log) = await ServeAsync([head, body], TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedResponse("post-refused-405"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.IsEmpty(await ReadWhatIsLeftAsync(connection, TestContext.CancellationToken), "The body was read before the close.");
        CollectionAssert.AreEqual(new[] { "POST /file.txt: the method is not served; answered 405 and closed." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task DrainAsync_ClientSendsMoreThanTheByteLimit_StopsAtTheByteLimit()
    {
        var body = new byte[HttpUnreadRequestDrainer.MaxDrainBytes];

        var (connection, log) = await ServeAsync([Ascii(RefusedPostHead), body, Ascii("left")], TestContext.CancellationToken);

        Assert.AreEqual("left", Latin1(await ReadWhatIsLeftAsync(connection, TestContext.CancellationToken)));
        Assert.AreEqual("Stopped draining the unread request bytes at the 1048576-byte drain limit.", log.Notes[1]);
    }

    [TestMethod]
    public async Task DrainAsync_ClientNeverHalfCloses_StopsAtTheTimeLimit()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([Ascii(RefusedPostHead), new byte[100]], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        await WaitForTimersAsync(clock, 1);
        clock.Advance(HttpUnreadRequestDrainer.MaxDrainTime - TimeSpan.FromTicks(1));
        Assert.IsFalse(serving.IsCompleted, "One tick of the drain's time limit is left.");
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        Assert.AreEqual(1, HttpUnreadRequestDrainer.MaxDrainTime.TotalSeconds);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("Stopped draining the unread request bytes at the 1-second drain limit.", log.Notes[1]);
    }

    [TestMethod]
    public async Task DrainAsync_ConnectionFailsWhileDraining_NotesItAndEnds()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([Ascii(RefusedPostHead)], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        await WaitForTimersAsync(clock, 1);
        connection.Abort();
        await serving;

        Assert.AreEqual("The connection failed while its unread request bytes were drained (The connection was aborted.).", log.Notes[1]);
    }

    [TestMethod]
    public async Task DrainAsync_ExchangeCancelledWhileDraining_Throws()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([Ascii(RefusedPostHead)], peerHalfClosesWhenExhausted: false);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, exchange.Token));

        await WaitForTimersAsync(clock, 1);
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task DrainAsync_ResponseThatClosesAfterAnUnreadableBody_DrainsTheBody()
    {
        var request = "GET /file.txt HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: gzip\r\n\r\n";

        var (connection, _) = await ServeAsync([Ascii(request), Ascii("gzip bytes")], TestContext.CancellationToken);

        Assert.EndsWith(FileBody, Latin1(connection.WrittenBytes));
        Assert.IsEmpty(await ReadWhatIsLeftAsync(connection, TestContext.CancellationToken));
    }

    private static (byte[] Head, byte[] Body) SplitAfterHead(byte[] request)
    {
        var headLength = Latin1(request).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;

        return (request[..headLength], request[headLength..]);
    }
}
