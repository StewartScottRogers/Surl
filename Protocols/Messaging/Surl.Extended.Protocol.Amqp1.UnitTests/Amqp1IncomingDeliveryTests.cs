namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>Which transfers continue a delivery (AMQP 1.0 Part 2 section 2.7.5).</summary>
[TestClass]
public sealed class Amqp1IncomingDeliveryTests
{
    [TestMethod]
    public void IsContinuedBy_TransferNamingTheDeliveryOrNothing_IsTrue()
    {
        var delivery = new Amqp1IncomingDelivery(3, [1, 2], 0);

        Assert.IsTrue(delivery.IsContinuedBy(new Amqp1Transfer(0)));
        Assert.IsTrue(delivery.IsContinuedBy(new Amqp1Transfer(0) { DeliveryId = 3, DeliveryTag = [1, 2] }));
    }

    [TestMethod]
    public void IsContinuedBy_TransferNamingAnotherDelivery_IsFalse()
    {
        var tagged = new Amqp1IncomingDelivery(3, [1, 2], 0);
        var untagged = new Amqp1IncomingDelivery(3, null, 0);

        Assert.IsFalse(tagged.IsContinuedBy(new Amqp1Transfer(0) { DeliveryId = 4 }));
        Assert.IsFalse(tagged.IsContinuedBy(new Amqp1Transfer(0) { DeliveryTag = [9] }));
        Assert.IsFalse(untagged.IsContinuedBy(new Amqp1Transfer(0) { DeliveryTag = [1] }));
    }
}
