namespace Surl.Core;

[TestClass]
public sealed class InFlightExchangesTests
{
    [TestMethod]
    public async Task WaitForAllAsync_NoExchangeStarted_CompletesWithoutGivingUp()
    {
        var inFlight = new InFlightExchanges();
        using var giveUp = new CancellationTokenSource();

        await inFlight.WaitForAllAsync(TimeSpan.FromSeconds(5), new ManualTimeProvider(), giveUp).WaitAsync(Patience.Timeout);

        Assert.IsFalse(giveUp.IsCancellationRequested);
    }

    [TestMethod]
    public async Task WaitForAllAsync_ExchangeThatThrows_StillCountsAsEnded()
    {
        var inFlight = new InFlightExchanges();
        using var giveUp = new CancellationTokenSource();
        var waiting = Task.CompletedTask;

        inFlight.Start(() => throw new InvalidOperationException("boom"));
        waiting = inFlight.WaitForAllAsync(TimeSpan.Zero, new ManualTimeProvider(), giveUp);

        await waiting.WaitAsync(Patience.Timeout);
        Assert.IsTrue(waiting.IsCompletedSuccessfully);
    }
}
