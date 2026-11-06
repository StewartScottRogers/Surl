namespace Surl.Extended.Protocol.WebTransport;

/// <summary>A client limit on what the server sends (ADR-0117 decision 4; WTH3 5.6), and the blocked reports it makes.</summary>
[TestClass]
public sealed class WebTransportSendCreditTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task TakeAsync_CreditFree_TakesWhatIsWantedUpToTheLimit()
    {
        var credit = new WebTransportSendCredit(5);
        var reports = new List<ulong>();

        var first = await credit.TakeAsync(3, Report(reports), Token);
        var second = await credit.TakeAsync(5, Report(reports), Token);

        Assert.AreEqual(3UL, first);
        Assert.AreEqual(2UL, second);
        Assert.IsEmpty(reports);
    }

    [TestMethod]
    public async Task TakeAsync_NoCredit_ReportsTheLimitOnceAndWaitsForItToBeRaised()
    {
        var credit = new WebTransportSendCredit(0);
        var reports = new List<ulong>();

        var first = credit.TakeAsync(1, Report(reports), Token);
        var second = credit.TakeAsync(1, Report(reports), Token);
        var raised = credit.TryRaise(1);
        var taken = await Task.WhenAny(first, second);
        var waiting = taken == first ? second : first;
        await WaitUntilAsync(() => reports.Count == 2);
        credit.TryRaise(2);

        Assert.IsTrue(raised);
        Assert.AreEqual(1UL, await taken);
        Assert.AreEqual(1UL, await waiting);
        CollectionAssert.AreEqual(new[] { 0UL, 1UL }, reports, "WTH3 5.6.4: one blocked report for each limit value.");
    }

    [TestMethod]
    [DataRow(4UL)]
    [DataRow(3UL)]
    public void TryRaise_NotAboveTheLast_IsRefused(ulong newLimit)
    {
        var credit = new WebTransportSendCredit(4);

        var raised = credit.TryRaise(newLimit);

        Assert.IsFalse(raised);
    }

    [TestMethod]
    public async Task TakeAsync_SessionEndsWhileBlocked_IsCancelled()
    {
        var credit = new WebTransportSendCredit(0);
        using var ending = new CancellationTokenSource();

        var taking = credit.TakeAsync(1, _ => Task.CompletedTask, ending.Token);
        await ending.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => taking);
    }

    private static Func<ulong, Task> Report(List<ulong> reports) => limit =>
    {
        lock (reports)
        {
            reports.Add(limit);
        }

        return Task.CompletedTask;
    };

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        while (!condition())
        {
            await Task.Yield();
            Token.ThrowIfCancellationRequested();
        }
    }
}
