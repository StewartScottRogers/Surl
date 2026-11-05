namespace Surl.Protocol.Http;

[TestClass]
public sealed class Http2SendWindowsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ChangeInitialStreamWindow_MovesEveryOpenStreamsWindowByTheDifference()
    {
        var windows = new Http2SendWindows();
        windows.OpenStream(1);

        var changed = windows.ChangeInitialStreamWindow(Http2SendWindows.DefaultInitialWindowSize + 10);
        windows.OpenStream(3);

        Assert.IsTrue(changed);
        Assert.AreEqual(Http2SendWindows.DefaultInitialWindowSize, await windows.TakeAsync(1, int.MaxValue, TestContext.CancellationToken), "The connection's window is what limits the first take.");
        Assert.IsTrue(windows.GrowConnectionWindow(100));
        Assert.AreEqual(10, await windows.TakeAsync(1, int.MaxValue, TestContext.CancellationToken));
        Assert.AreEqual(90, await windows.TakeAsync(3, int.MaxValue, TestContext.CancellationToken));
    }

    [TestMethod]
    public void ChangeInitialStreamWindow_OverflowingAnOpenStream_IsRefused()
    {
        var windows = new Http2SendWindows();
        windows.OpenStream(1);
        Assert.IsTrue(windows.GrowStreamWindow(1, int.MaxValue - Http2SendWindows.DefaultInitialWindowSize));

        Assert.IsFalse(windows.ChangeInitialStreamWindow(Http2SendWindows.DefaultInitialWindowSize + 1));
    }

    [TestMethod]
    public async Task TakeAsync_NoRoom_WaitsForAnUpdateAndIsToldWhenNoneCanCome()
    {
        var windows = new Http2SendWindows();
        windows.OpenStream(1);
        Assert.IsTrue(windows.ChangeInitialStreamWindow(0));

        var waiting = windows.TakeAsync(1, 5, TestContext.CancellationToken);
        Assert.IsFalse(waiting.IsCompleted);
        Assert.IsTrue(windows.GrowStreamWindow(1, 3));
        var taken = await waiting;
        windows.StopUpdates();
        var afterStop = await windows.TakeAsync(1, 5, TestContext.CancellationToken);

        Assert.AreEqual(3, taken);
        Assert.AreEqual(0, afterStop);
    }

    [TestMethod]
    public async Task TakeAsync_Cancelled_Throws()
    {
        var windows = new Http2SendWindows();
        windows.OpenStream(1);
        Assert.IsTrue(windows.ChangeInitialStreamWindow(0));
        using var cancellation = new CancellationTokenSource();

        var waiting = windows.TakeAsync(1, 5, cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);
    }

    [TestMethod]
    public void GrowStreamWindow_ClosedStreamIsIgnored()
    {
        var windows = new Http2SendWindows();
        windows.OpenStream(1);
        windows.CloseStream(1);

        Assert.IsTrue(windows.GrowStreamWindow(1, int.MaxValue));
    }
}
