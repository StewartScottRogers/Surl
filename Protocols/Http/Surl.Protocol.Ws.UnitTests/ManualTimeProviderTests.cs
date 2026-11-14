namespace Surl.Protocol.Ws;

[TestClass]
public sealed class ManualTimeProviderTests
{
    // BL-1110: a disposed timer leaves ActiveTimerCount but never CreatedTimerCount, which is how
    // a test tells a farewell's write deadline from the lingering close's timer after it.
    [TestMethod]
    public void CreatedTimerCount_CountsDisposedTimersToo_WhereActiveTimerCountDoesNot()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);

        var first = clock.CreateTimer(_ => { }, null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
        first.Dispose();
        using var second = clock.CreateTimer(_ => { }, null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);

        Assert.AreEqual(2, clock.CreatedTimerCount);
        Assert.AreEqual(1, clock.ActiveTimerCount);
    }
}
