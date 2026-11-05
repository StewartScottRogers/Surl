using Surl.MessageBus;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// One connection's judging of the <c>PUBLISH</c>es its client sends (ADR-0093 decision 4).
/// </summary>
[TestClass]
public sealed class Mqtt5InboundPublishesTests
{
    [TestMethod]
    public void Judge_TopicNameTheBusCannotMap_IsMalformed()
    {
        var inbound = new Mqtt5InboundPublishes(new TopicNameLookup([]), "mqtt5");

        var verdict = inbound.Judge(new Mqtt5PublishPacket(false, 0, false, "a+b", 0, [], []));

        Assert.AreEqual(Mqtt5PacketFault.Malformed("[MQTT-4.7.3-1]"), verdict.Fault);
        Assert.IsNull(verdict.Message);
    }

    [TestMethod]
    public void Judge_TopicAliasSet_IsDroppedFromTheForwardedProperties()
    {
        var inbound = new Mqtt5InboundPublishes(new TopicNameLookup([]), "mqtt5");
        var userProperty = Mqtt5Property.FromUserProperty("k", "v");

        var verdict = inbound.Judge(new Mqtt5PublishPacket(false, 0, false, "t", 0, [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.TopicAlias, 3), userProperty], [1]));

        Assert.AreEqual("t", verdict.Message!.Topic);
        Assert.AreSame(userProperty, verdict.Message.Properties.Single());
    }

    [TestMethod]
    public void Release_IdentifierAwaitingRelease_IsForgottenOnce()
    {
        var inbound = new Mqtt5InboundPublishes(new TopicNameLookup([]), "mqtt5");
        inbound.AwaitRelease(4);

        Assert.IsTrue(inbound.Release(4));
        Assert.IsFalse(inbound.Release(4));
    }
}
