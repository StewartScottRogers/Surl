namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// The answers of the node directory seam (ADR-0107 decision 4).
/// </summary>
[TestClass]
public sealed class Amqp1NodeResolutionTests
{
    [TestMethod]
    public void Found_CarriesTheAddress()
    {
        Assert.AreEqual(new Amqp1NodeResolution(Amqp1NodeOutcome.Found, "/queue/orders"), Amqp1NodeResolution.Found("/queue/orders"));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1NodeResolution.Found(null!));
    }

    [TestMethod]
    public void Refusals_CarryNoAddress()
    {
        Assert.AreEqual(new Amqp1NodeResolution(Amqp1NodeOutcome.NotFound, null), Amqp1NodeResolution.NotFound);
        Assert.AreEqual(new Amqp1NodeResolution(Amqp1NodeOutcome.Unauthorized, null), Amqp1NodeResolution.Unauthorized);
        Assert.AreEqual(new Amqp1NodeResolution(Amqp1NodeOutcome.ResourceLimitExceeded, null), Amqp1NodeResolution.ResourceLimitExceeded);
    }

    [TestMethod]
    public void Request_WithoutAnAddress_IsDynamic()
    {
        Assert.IsTrue(new Amqp1NodeRequest(null, true, null).IsDynamic);
        Assert.IsFalse(new Amqp1NodeRequest("/queue/orders", true, null).IsDynamic);
    }
}
