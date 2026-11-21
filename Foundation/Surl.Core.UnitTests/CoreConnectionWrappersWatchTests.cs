using Surl.Protocol.Abstractions;

namespace Surl.Core;

// Core's IConnection wrappers forward WatchForPeerCloseAsync (ADR-0141, amendment 2, decision 2).
[TestClass]
public sealed class CoreConnectionWrappersWatchTests
{
    private static readonly ConnectionLimits TenSecondsIdle = new(0, 0, TimeSpan.FromSeconds(10), TimeSpan.Zero);

    [TestMethod]
    public async Task IdleClockRestartingConnection_Watch_ForwardsTheOutcomeAndLeavesTheIdleClockRunning()
    {
        var time = new ManualTimeProvider();
        using var deadlines = new ExchangeDeadlines(TenSecondsIdle, time, CancellationToken.None);
        var watched = new IdleClockRestartingConnection(new InMemoryConnection(["abc"u8.ToArray()], peerResetsWhenExhausted: true), deadlines);
        time.Advance(TimeSpan.FromSeconds(5));

        var outcome = await watched.WatchForPeerCloseAsync(1024, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.AreEqual(PeerWatchOutcome.PeerReset, outcome);
        Assert.AreEqual(ExchangeCancellation.IdleTimeout, deadlines.Reason);
    }

    [TestMethod]
    public async Task RecordingConnection_Watch_ForwardsTheOutcome()
    {
        var recording = new RecordingConnection(new InMemoryConnection([]), new RecordingExchangeLog());

        var outcome = await recording.WatchForPeerCloseAsync(1024, CancellationToken.None);

        Assert.AreEqual(PeerWatchOutcome.PeerClosed, outcome);
    }

    [TestMethod]
    public async Task DroppingAfterBytesConnection_Watch_ForwardsTheOutcome()
    {
        var connection = new DroppingAfterBytesConnection(
            new InMemoryConnection([], peerResetsWhenExhausted: true), new ExchangeDropBudget(10, () => { }));

        var outcome = await connection.WatchForPeerCloseAsync(1024, CancellationToken.None);

        Assert.AreEqual(PeerWatchOutcome.PeerReset, outcome);
    }
}
