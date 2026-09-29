namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class ExchangeLimitsTests
{
    [TestMethod]
    public void Default_HoldsTheAdr0006Defaults()
    {
        var limits = ExchangeLimits.Default;

        Assert.AreEqual(TimeSpan.FromSeconds(30), limits.HeadTimeout);
        Assert.AreEqual(102400L, limits.MaxRequestHeadBytes);
        Assert.AreEqual(8192L, limits.MaxLineBytes);
        Assert.AreEqual(1048576L, limits.MaxMessageBytes);
        Assert.AreEqual(104857600L, limits.MaxUploadBytes);
    }

    [TestMethod]
    public void Constructor_GivenValues_KeepsThem()
    {
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(5), 1, 2, 3, 4);

        Assert.AreEqual(TimeSpan.FromSeconds(5), limits.HeadTimeout);
        Assert.AreEqual(1L, limits.MaxRequestHeadBytes);
        Assert.AreEqual(2L, limits.MaxLineBytes);
        Assert.AreEqual(3L, limits.MaxMessageBytes);
        Assert.AreEqual(4L, limits.MaxUploadBytes);
    }

    [TestMethod]
    public void Constructor_ZeroSizesAndInfiniteHeadTimeout_AreAccepted()
    {
        var limits = new ExchangeLimits(Timeout.InfiniteTimeSpan, 0, 0, 0, 0);

        Assert.AreEqual(Timeout.InfiniteTimeSpan, limits.HeadTimeout);
        Assert.AreEqual(0L, limits.MaxRequestHeadBytes);
        Assert.AreEqual(0L, limits.MaxLineBytes);
        Assert.AreEqual(0L, limits.MaxMessageBytes);
        Assert.AreEqual(0L, limits.MaxUploadBytes);
    }

    [TestMethod]
    public void Constructor_ZeroHeadTimeout_IsAccepted()
    {
        var limits = new ExchangeLimits(TimeSpan.Zero, 1, 1, 1, 1);

        Assert.AreEqual(TimeSpan.Zero, limits.HeadTimeout);
    }

    [TestMethod]
    public void Constructor_NegativeHeadTimeout_ThrowsNamingHeadTimeout()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ExchangeLimits(TimeSpan.FromSeconds(-1), 1, 1, 1, 1));

        Assert.AreEqual(nameof(ExchangeLimits.HeadTimeout), exception.ParamName);
    }

    [TestMethod]
    public void Constructor_NegativeMaxRequestHeadBytes_ThrowsNamingIt()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ExchangeLimits(TimeSpan.Zero, -1, 1, 1, 1));

        Assert.AreEqual(nameof(ExchangeLimits.MaxRequestHeadBytes), exception.ParamName);
    }

    [TestMethod]
    public void Constructor_NegativeMaxLineBytes_ThrowsNamingIt()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ExchangeLimits(TimeSpan.Zero, 1, -1, 1, 1));

        Assert.AreEqual(nameof(ExchangeLimits.MaxLineBytes), exception.ParamName);
    }

    [TestMethod]
    public void Constructor_NegativeMaxMessageBytes_ThrowsNamingIt()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ExchangeLimits(TimeSpan.Zero, 1, 1, -1, 1));

        Assert.AreEqual(nameof(ExchangeLimits.MaxMessageBytes), exception.ParamName);
    }

    [TestMethod]
    public void Constructor_NegativeMaxUploadBytes_ThrowsNamingIt()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ExchangeLimits(TimeSpan.Zero, 1, 1, 1, -1));

        Assert.AreEqual(nameof(ExchangeLimits.MaxUploadBytes), exception.ParamName);
    }

    [TestMethod]
    public void With_NegativeSize_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ExchangeLimits.Default with { MaxUploadBytes = -1 });

        Assert.AreEqual(nameof(ExchangeLimits.MaxUploadBytes), exception.ParamName);
    }

    [TestMethod]
    public void With_ZeroSize_ChangesOnlyThatLimit()
    {
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 0 };

        Assert.AreEqual(0L, limits.MaxUploadBytes);
        Assert.AreEqual(ExchangeLimits.Default.MaxMessageBytes, limits.MaxMessageBytes);
        Assert.AreEqual(1048576L, ExchangeLimits.Default.MaxMessageBytes);
        Assert.AreEqual(104857600L, ExchangeLimits.Default.MaxUploadBytes);
    }
}
