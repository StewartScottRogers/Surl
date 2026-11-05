namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// The container's id and idle time-out (ADR-0107 decision 3).
/// </summary>
[TestClass]
public sealed class Amqp1ContainerOptionsTests
{
    [TestMethod]
    public void Default_IdleTimeout_Is120SecondsAdvertisedAsHalf()
    {
        var options = Amqp1ContainerOptions.Default;

        Assert.AreEqual(TimeSpan.FromSeconds(120), options.SilenceLimit);
        Assert.AreEqual(60000u, options.AdvertisedIdleTimeOutMilliseconds);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "zero")]
    [DataRow(-1, DisplayName = "infinite")]
    public void IdleTimeoutOff_IsNeverSilentAndAdvertisesNone(int milliseconds)
    {
        var options = new Amqp1ContainerOptions { IdleTimeout = TimeSpan.FromMilliseconds(milliseconds) };

        Assert.AreEqual(Timeout.InfiniteTimeSpan, options.SilenceLimit);
        Assert.IsNull(options.AdvertisedIdleTimeOutMilliseconds);
    }

    [TestMethod]
    public void AdvertisedIdleTimeOut_BeyondAUint_IsUintMax()
    {
        var options = new Amqp1ContainerOptions { IdleTimeout = TimeSpan.FromDays(100) };

        Assert.AreEqual(uint.MaxValue, options.AdvertisedIdleTimeOutMilliseconds);
    }

    [TestMethod]
    public void NewContainerId_IsSurlAnd32LowerCaseHexDigitsAndUnique()
    {
        var first = Amqp1ContainerOptions.NewContainerId();

        Assert.MatchesRegex("^surl-[0-9a-f]{32}$", first);
        Assert.AreNotEqual(first, Amqp1ContainerOptions.NewContainerId());
        Assert.MatchesRegex("^surl-[0-9a-f]{32}$", new Amqp1ContainerOptions().ContainerId);
    }
}
