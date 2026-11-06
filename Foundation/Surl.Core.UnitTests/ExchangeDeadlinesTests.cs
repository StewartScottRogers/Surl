namespace Surl.Core;

[TestClass]
public sealed class ExchangeDeadlinesTests
{
    private static readonly ConnectionLimits TenSecondsIdleThirtyAtMost = new(0, 0, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

    [TestMethod]
    public void Reason_WhileNothingFired_IsNull()
    {
        using var deadlines = new ExchangeDeadlines(TenSecondsIdleThirtyAtMost, new ManualTimeProvider(), CancellationToken.None);

        Assert.IsNull(deadlines.Reason);
        Assert.IsFalse(deadlines.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void PauseIdleClock_IdleTimeoutPassesWhilePaused_DoesNotCancel_AndRestartStartsItAfresh()
    {
        var time = new ManualTimeProvider();
        using var deadlines = new ExchangeDeadlines(TenSecondsIdleThirtyAtMost, time, CancellationToken.None);

        deadlines.PauseIdleClock();
        time.Advance(TimeSpan.FromSeconds(20));
        var cancelledWhilePaused = deadlines.Token.IsCancellationRequested;
        deadlines.RestartIdleClock();
        time.Advance(TimeSpan.FromSeconds(9));
        var cancelledBeforeTheIdleTimeout = deadlines.Token.IsCancellationRequested;
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.IsFalse(cancelledWhilePaused);
        Assert.IsFalse(cancelledBeforeTheIdleTimeout);
        Assert.AreEqual(ExchangeCancellation.IdleTimeout, deadlines.Reason);
    }

    [TestMethod]
    public void PauseIdleClock_NoIdleTimeout_DoesNothing()
    {
        using var deadlines = new ExchangeDeadlines(ConnectionLimits.None, new ManualTimeProvider(), CancellationToken.None);

        deadlines.PauseIdleClock();

        Assert.IsNull(deadlines.Reason);
    }

    [TestMethod]
    public void ShutdownToken_IsTheShutdownTokenItWasGiven()
    {
        using var shutdown = new CancellationTokenSource();
        using var deadlines = new ExchangeDeadlines(TenSecondsIdleThirtyAtMost, new ManualTimeProvider(), shutdown.Token);

        var shutdownToken = deadlines.ShutdownToken;

        Assert.AreEqual(shutdown.Token, shutdownToken);
    }

    [TestMethod]
    public void Reason_ShutdownFirst_IsShutdown()
    {
        using var shutdown = new CancellationTokenSource();
        using var deadlines = new ExchangeDeadlines(ConnectionLimits.None, new ManualTimeProvider(), shutdown.Token);

        shutdown.Cancel();

        Assert.IsTrue(deadlines.Token.IsCancellationRequested);
        Assert.AreEqual(ExchangeCancellation.Shutdown, deadlines.Reason);
    }

    [TestMethod]
    public void Reason_IdleTimeoutThenShutdown_StaysIdleTimeout()
    {
        var time = new ManualTimeProvider();
        using var shutdown = new CancellationTokenSource();
        using var deadlines = new ExchangeDeadlines(TenSecondsIdleThirtyAtMost, time, shutdown.Token);

        time.Advance(TimeSpan.FromSeconds(10));
        shutdown.Cancel();

        Assert.AreEqual(ExchangeCancellation.IdleTimeout, deadlines.Reason);
    }

    [TestMethod]
    public void Reason_ActivityKeepsItFromIdling_UntilTheMaximumDuration()
    {
        var time = new ManualTimeProvider();
        using var deadlines = new ExchangeDeadlines(TenSecondsIdleThirtyAtMost, time, CancellationToken.None);

        for (var second = 0; second < 29; second++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            deadlines.RestartIdleClock();
        }

        Assert.IsNull(deadlines.Reason);

        time.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(ExchangeCancellation.MaxExchangeDuration, deadlines.Reason);
    }

    [TestMethod]
    public void RestartIdleClock_NoIdleTimeout_DoesNothing()
    {
        var time = new ManualTimeProvider();
        using var deadlines = new ExchangeDeadlines(ConnectionLimits.None, time, CancellationToken.None);

        deadlines.RestartIdleClock();
        time.Advance(TimeSpan.FromDays(1));

        Assert.IsNull(deadlines.Reason);
    }

    [TestMethod]
    public void RestartIdleClock_AfterDispose_DoesNothing()
    {
        var deadlines = new ExchangeDeadlines(TenSecondsIdleThirtyAtMost, new ManualTimeProvider(), CancellationToken.None);
        deadlines.Dispose();

        deadlines.RestartIdleClock();

        Assert.IsNull(deadlines.Reason);
    }
}
