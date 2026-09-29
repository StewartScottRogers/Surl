namespace Surl.Core;

[TestClass]
public sealed class ConnectionLimitsTests
{
    [TestMethod]
    public void Default_HoldsAdr0006sNumbers()
    {
        var limits = ConnectionLimits.Default;

        Assert.AreEqual(1024, limits.MaxConnections);
        Assert.AreEqual(100, limits.MaxConnectionsPerAddress);
        Assert.AreEqual(TimeSpan.FromSeconds(120), limits.IdleTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(3600), limits.MaxExchangeDuration);
    }

    [TestMethod]
    public void None_IsZeroEverywhere()
    {
        Assert.AreEqual(new ConnectionLimits(0, 0, TimeSpan.Zero, TimeSpan.Zero), ConnectionLimits.None);
    }

    [TestMethod]
    public void MaxTimeout_IsTheLongestCancellationTokenSourceDelay()
    {
        Assert.AreEqual(TimeSpan.FromMilliseconds(4294967294), ConnectionLimits.MaxTimeout);

        using var source = new CancellationTokenSource(ConnectionLimits.MaxTimeout);

        Assert.IsFalse(source.IsCancellationRequested);
    }

    [TestMethod]
    public void Constructor_LongestTimeouts_AreAccepted()
    {
        var limits = new ConnectionLimits(1, 1, ConnectionLimits.MaxTimeout, ConnectionLimits.MaxTimeout);

        Assert.AreEqual(ConnectionLimits.MaxTimeout, limits.IdleTimeout);
        Assert.AreEqual(ConnectionLimits.MaxTimeout, limits.MaxExchangeDuration);
    }

    [TestMethod]
    public void Constructor_NegativeMaxConnections_ThrowsNamingIt()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ConnectionLimits(-1, 0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.AreEqual(nameof(ConnectionLimits.MaxConnections), exception.ParamName);
    }

    [TestMethod]
    public void With_NegativeMaxConnectionsPerAddress_ThrowsNamingIt()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ConnectionLimits.Default with { MaxConnectionsPerAddress = -1 });

        Assert.AreEqual(nameof(ConnectionLimits.MaxConnectionsPerAddress), exception.ParamName);
    }

    [TestMethod]
    public void With_NegativeIdleTimeout_ThrowsNamingIt()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ConnectionLimits.Default with { IdleTimeout = TimeSpan.FromTicks(-1) });

        Assert.AreEqual(nameof(ConnectionLimits.IdleTimeout), exception.ParamName);
    }

    [TestMethod]
    public void With_MaxExchangeDurationAboveMaxTimeout_ThrowsNamingIt()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ConnectionLimits.Default with { MaxExchangeDuration = ConnectionLimits.MaxTimeout + TimeSpan.FromMilliseconds(1) });

        Assert.AreEqual(nameof(ConnectionLimits.MaxExchangeDuration), exception.ParamName);
    }
}
