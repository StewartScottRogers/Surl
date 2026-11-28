namespace Surl.MessageBus;

/// <summary>
/// Waiting for the client connections that joined before one to catch up with what their clients
/// sent (<see cref="PublishingConnections"/>). No test advances a clock except to end the longest wait.
/// </summary>
[TestClass]
public sealed class PublishingConnectionsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void MaxWaitForEarlierConnections_IsTwoSeconds() =>
        Assert.AreEqual(TimeSpan.FromSeconds(2), PublishingConnections.MaxWaitForEarlierConnections);

    [TestMethod]
    public async Task WaitForEarlierConnectionsAsync_ForTheOldestConnection_IsCompleteAtOnce()
    {
        var connections = NewConnections();
        using var oldest = connections.Join();
        using var later = connections.Join();

        var waiting = oldest.WaitForEarlierConnectionsAsync(TimeProvider.System, TestContext.CancellationToken);

        Assert.IsTrue(waiting.IsCompleted);
        Assert.IsTrue(await waiting);
    }

    [TestMethod]
    public async Task WaitForEarlierConnectionsAsync_WithTwoEarlierHandling_CompletesOnceBothWaitForTheirClients()
    {
        var connections = NewConnections();
        using var first = connections.Join();
        using var second = connections.Join();
        using var newest = connections.Join();
        var firstRead = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRead = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var waiting = newest.WaitForEarlierConnectionsAsync(TimeProvider.System, TestContext.CancellationToken).AsTask();
        var firstReading = first.CountAsCaughtUpWhileAsync(new ValueTask<int>(firstRead.Task));
        var completedAfterOne = waiting.IsCompleted;
        var secondReading = second.CountAsCaughtUpWhileAsync(new ValueTask<int>(secondRead.Task));
        var allCaughtUp = await waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        firstRead.SetResult(1);
        secondRead.SetResult(2);

        Assert.IsFalse(completedAfterOne);
        Assert.IsTrue(allCaughtUp);
        Assert.AreEqual(1, await firstReading);
        Assert.AreEqual(2, await secondReading);
    }

    [TestMethod]
    public async Task CountAsCaughtUpWhileAsync_AReadThatReturns_LeavesTheConnectionHandlingAgain()
    {
        var connections = NewConnections();
        using var earlier = connections.Join();
        using var later = connections.Join();
        var read = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = earlier.CountAsCaughtUpWhileAsync(new ValueTask<int>(read.Task));
        var caughtUpWhileReading = earlier.CaughtUp.IsCompleted;

        read.SetResult(5);
        await reading;

        Assert.IsTrue(caughtUpWhileReading);
        Assert.IsFalse(earlier.CaughtUp.IsCompleted);
    }

    [TestMethod]
    public async Task CountAsCaughtUpWhileAsync_ATaskAlreadyComplete_LeavesAnEarlierWaitWaiting()
    {
        var connections = NewConnections();
        using var earlier = connections.Join();
        var before = earlier.CaughtUp;

        var result = await earlier.CountAsCaughtUpWhileAsync(ValueTask.FromResult(3));

        Assert.AreEqual(3, result);
        Assert.AreSame(before, earlier.CaughtUp);
        Assert.IsFalse(before.IsCompleted);
    }

    [TestMethod]
    public async Task Dispose_AnEarlierConnectionStillHandling_CompletesTheWaitForIt()
    {
        var connections = NewConnections();
        var earlier = connections.Join();
        using var later = connections.Join();
        var waiting = later.WaitForEarlierConnectionsAsync(TimeProvider.System, TestContext.CancellationToken).AsTask();

        earlier.Dispose();

        Assert.IsTrue(await waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task WaitForEarlierConnectionsAsync_AfterThisConnectionHasLeft_WaitsForNone()
    {
        var connections = NewConnections();
        using var earlier = connections.Join();
        var left = connections.Join();
        left.Dispose();

        Assert.IsTrue(await left.WaitForEarlierConnectionsAsync(TimeProvider.System, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task WaitForEarlierConnectionsAsync_AnEarlierConnectionStillHandlingAfterTwoSeconds_IsFalse()
    {
        var clock = new FiringTimeProvider();
        var connections = NewConnections();
        using var earlier = connections.Join();
        using var later = connections.Join();
        var waiting = later.WaitForEarlierConnectionsAsync(clock, TestContext.CancellationToken).AsTask();

        clock.Fire();

        Assert.IsFalse(await waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        Assert.AreEqual(TimeSpan.FromSeconds(2), clock.DueTime);
    }

    [TestMethod]
    public async Task WaitForEarlierConnectionsAsync_Cancelled_Throws()
    {
        var connections = NewConnections();
        using var earlier = connections.Join();
        using var later = connections.Join();
        using var cancellation = new CancellationTokenSource();
        var waiting = later.WaitForEarlierConnectionsAsync(TimeProvider.System, cancellation.Token).AsTask();

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);
    }

    [TestMethod]
    public async Task WaitForEarlierConnectionsAsync_NoClock_Throws()
    {
        using var connection = NewConnections().Join();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => connection.WaitForEarlierConnectionsAsync(null!, TestContext.CancellationToken).AsTask());
    }

    private static PublishingConnections NewConnections() => new MessageBus(TimeProvider.System).PublishingConnections;
}
