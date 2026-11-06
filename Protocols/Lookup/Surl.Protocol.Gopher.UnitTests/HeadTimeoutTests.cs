using Surl.Protocol.Abstractions;
using static Surl.Protocol.Gopher.GopherTestExchange;

namespace Surl.Protocol.Gopher;

[TestClass]
public sealed class HeadTimeoutTests
{
    private static readonly TimeSpan HeadTimeout = ExchangeLimits.Default.HeadTimeout;
    private static readonly TimeSpan JustUnderHeadTimeout = HeadTimeout - TimeSpan.FromTicks(1);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PartialSelector_AfterHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Ascii("/file")], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, log, TestContext.CancellationToken));
        clock.Advance(JustUnderHeadTimeout);
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("closed-with-no-bytes", "stdout.bin"), connection.WrittenBytes);
        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("No selector was read (HeadTimedOut); the connection was closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task NoSelector_AfterHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, new RecordingExchangeLog(), TestContext.CancellationToken));
        clock.Advance(HeadTimeout);
        await serving;

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task SelectorBeforeHeadTimeout_IsAnsweredAndLeavesNoTimerRunning()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([Ascii("/file.txt\r\n")]);

        await Server().ServeAsync(connection, Context(clock, new RecordingExchangeLog(), TestContext.CancellationToken));

        Assert.AreEqual(FileBody, Utf8(connection.WrittenBytes));
        Assert.AreEqual(0, clock.PendingTimerCount);
    }

    [TestMethod]
    public void RecordedCloseWithNoBytes_WasAcceptedByUpstreamCurl()
    {
        Assert.AreEqual("/file.txt\r\n", Utf8(RecordedFixture.ReadRequestBytes("closed-with-no-bytes")));
        Assert.IsEmpty(RecordedFixture.ReadBytes("closed-with-no-bytes", "stdout.bin"));
        Assert.AreEqual("0", Utf8(RecordedFixture.ReadBytes("closed-with-no-bytes", "exitcode.txt")).Trim());
        Assert.IsEmpty(RecordedFixture.ReadBytes("closed-with-no-bytes", "stderr.txt"));
    }
}
