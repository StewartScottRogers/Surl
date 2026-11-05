namespace Surl.Http3;

[TestClass]
public sealed class Http3HeadTimeoutDelayTests
{
    [TestMethod]
    public void For_TimeoutATimerCanWaitFor_IsTheTimeout()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(30), Http3HeadTimeoutDelay.For(TimeSpan.FromSeconds(30)));
    }

    [TestMethod]
    public void For_TimeoutLongerThanATimerCanWait_IsInfinite()
    {
        Assert.AreEqual(Timeout.InfiniteTimeSpan, Http3HeadTimeoutDelay.For(TimeSpan.FromDays(100)));
    }
}
