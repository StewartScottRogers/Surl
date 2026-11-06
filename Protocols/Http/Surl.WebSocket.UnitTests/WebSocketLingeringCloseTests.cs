using System.Net;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.WebSocket;

[TestClass]
public sealed class WebSocketLingeringCloseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 30, 0, TimeSpan.Zero);

    private static readonly TimeSpan LingerEndTimeout = TimeSpan.FromSeconds(10);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LingerAsync_ClientSendsThenCloses_ReadsItAllAndNotesNothing()
    {
        var connection = new InMemoryConnection([new byte[] { 1, 2, 3 }, new byte[] { 4 }]);
        var log = new RecordingExchangeLog();
        var linger = Linger(connection, log, TimeProvider.System);

        await linger.LingerAsync(TestContext.CancellationToken).WaitAsync(LingerEndTimeout, TestContext.CancellationToken);

        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task LingerAsync_ClientNeverCloses_StopsAtMaxLingerTimeAndNotesIt()
    {
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var clock = new ManualTimeProvider(Now);
        var lingering = Linger(connection, log, clock).LingerAsync(TestContext.CancellationToken);

        while (clock.ActiveTimerCount == 0)
        {
            await Task.Yield();
        }

        clock.Advance(WebSocketLingeringClose.MaxLingerTime);
        await lingering.WaitAsync(LingerEndTimeout, TestContext.CancellationToken);

        Assert.AreEqual("Stopped reading what the client still sent at the 1-second lingering close.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task LingerAsync_ConnectionFails_NotesTheFailure()
    {
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        connection.Abort();
        var log = new RecordingExchangeLog();

        await Linger(connection, log, TimeProvider.System).LingerAsync(TestContext.CancellationToken).WaitAsync(LingerEndTimeout, TestContext.CancellationToken);

        Assert.AreEqual("The connection failed during the lingering close (The connection was aborted.).", log.Notes[^1]);
    }

    [TestMethod]
    public async Task LingerAsync_EndingTokenCancelled_Throws()
    {
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        using var ending = new CancellationTokenSource();
        await ending.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Linger(connection, new RecordingExchangeLog(), TimeProvider.System).LingerAsync(ending.Token));
    }

    private static WebSocketLingeringClose Linger(InMemoryConnection connection, IExchangeLog log, TimeProvider clock)
    {
        var context = new ExchangeContext(
            1,
            new ListenUrl("ws", "127.0.0.1", 18301).WithBoundPort(18301),
            new IPEndPoint(IPAddress.Loopback, 18301),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            clock,
            CancellationToken.None);

        return new WebSocketLingeringClose(new HttpConnectionReader(connection, 8192), context);
    }
}
