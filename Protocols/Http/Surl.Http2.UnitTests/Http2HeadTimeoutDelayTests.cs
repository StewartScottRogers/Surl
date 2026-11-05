namespace Surl.Http2;

[TestClass]
public sealed class Http2HeadTimeoutDelayTests
{
    [TestMethod]
    public void For_TimeoutATimerCanWaitFor_IsTheTimeout()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(30), Http2HeadTimeoutDelay.For(TimeSpan.FromSeconds(30)));
    }

    [TestMethod]
    public void For_TimeoutLongerThanATimerCanWait_IsInfinite()
    {
        Assert.AreEqual(Timeout.InfiniteTimeSpan, Http2HeadTimeoutDelay.For(TimeSpan.FromDays(100)));
    }
}
