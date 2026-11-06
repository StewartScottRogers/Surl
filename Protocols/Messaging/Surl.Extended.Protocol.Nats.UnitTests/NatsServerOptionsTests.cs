namespace Surl.Extended.Protocol.Nats.UnitTests;

[TestClass]
public sealed class NatsServerOptionsTests
{
    [TestMethod]
    public void PingInterval_Default_IsAQuarterOfTheIdleTimeout()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(30), NatsServerOptions.Default.PingInterval);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void PingInterval_IdleTimeoutOff_Is120Seconds(int idleTimeoutMilliseconds)
    {
        var options = new NatsServerOptions { IdleTimeout = TimeSpan.FromMilliseconds(idleTimeoutMilliseconds) };

        Assert.AreEqual(TimeSpan.FromSeconds(120), options.PingInterval);
    }

    [TestMethod]
    public void Default_RequiresLogins()
    {
        Assert.IsFalse(NatsServerOptions.Default.AllowsAnonymous);
        Assert.AreEqual(2, NatsServerOptions.MaxUnansweredPings);
    }
}
