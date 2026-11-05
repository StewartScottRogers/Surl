namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>The heartbeat offered for an idle timeout (ADR-0112 decision 3).</summary>
[TestClass]
public sealed class Amqp0BrokerOptionsTests
{
    [TestMethod]
    public void Default_OffersSixtySecondsAndNoAnonymous()
    {
        Assert.AreEqual((ushort)60, Amqp0BrokerOptions.Default.OfferedHeartbeatSeconds);
        Assert.IsFalse(Amqp0BrokerOptions.Default.OffersAnonymous);
    }

    [TestMethod]
    [DataRow(0.0, 0, DisplayName = "off")]
    [DataRow(-0.001, 0, DisplayName = "infinite")]
    [DataRow(3.0, 1, DisplayName = "odd seconds round down")]
    [DataRow(1000000.0, 65535, DisplayName = "capped at 65535")]
    public void OfferedHeartbeatSeconds_IsHalfTheIdleTimeout(double idleSeconds, int expected) =>
        Assert.AreEqual((ushort)expected, new Amqp0BrokerOptions { IdleTimeout = TimeSpan.FromSeconds(idleSeconds) }.OfferedHeartbeatSeconds);
}
