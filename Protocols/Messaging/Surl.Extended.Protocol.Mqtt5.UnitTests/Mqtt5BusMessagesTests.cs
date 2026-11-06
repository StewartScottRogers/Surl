using Surl.MessageBus;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// How an MQTT 5 Application Message travels on the shared message bus (ADR-0100 decision 3.4).
/// </summary>
[TestClass]
public sealed class Mqtt5BusMessagesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void ToBusMessage_NoProperties_CarriesTopicPayloadQosAndRetainOnly()
    {
        var message = Mqtt5BusMessages.ToBusMessage(new("t", [1, 2], 2, true, []), Now);

        Assert.AreEqual("t", message.Topic);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, message.Payload.ToArray());
        Assert.AreEqual(BusQualityOfService.ExactlyOnce, message.QualityOfService);
        Assert.IsTrue(message.Retain);
        Assert.IsNull(message.ExpiresAt);
        Assert.IsNull(message.ContentType);
        Assert.IsNull(message.ResponseTopic);
        Assert.IsNull(message.CorrelationData);
        Assert.IsEmpty(message.Headers);
        Assert.IsFalse(message.Utf8Payload);
    }

    [TestMethod]
    public void ToBusMessage_EveryForwardedProperty_BecomesTheBussField()
    {
        Mqtt5Property[] properties =
        [
            Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.PayloadFormatIndicator, 1),
            Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.MessageExpiryInterval, 30),
            Mqtt5Property.FromText(Mqtt5PropertyIdentifier.ContentType, "text/plain"),
            Mqtt5Property.FromText(Mqtt5PropertyIdentifier.ResponseTopic, "r"),
            Mqtt5Property.FromBinary(Mqtt5PropertyIdentifier.CorrelationData, [9]),
            Mqtt5Property.FromUserProperty("a", "1"),
            Mqtt5Property.FromUserProperty("a", "2"),
        ];

        var message = Mqtt5BusMessages.ToBusMessage(new("t", [], 0, false, properties), Now);

        Assert.IsTrue(message.Utf8Payload);
        Assert.AreEqual(Now.AddSeconds(30), message.ExpiresAt);
        Assert.AreEqual("text/plain", message.ContentType);
        Assert.AreEqual("r", message.ResponseTopic);
        CollectionAssert.AreEqual(new byte[] { 9 }, message.CorrelationData!.Value.ToArray());
        CollectionAssert.AreEqual(new[] { new BusMessageHeader("a", "1"), new BusMessageHeader("a", "2") }, message.Headers.ToArray());
    }

    [TestMethod]
    public void PropertiesOf_NothingSet_IsEmpty()
    {
        Assert.IsEmpty(Mqtt5BusMessages.PropertiesOf(new BusMessage("t", Array.Empty<byte>()), Now));
    }

    [TestMethod]
    public void PropertiesOf_EveryField_InTheDocumentedOrderWithTheExpiryLeftRoundedUp()
    {
        var message = new BusMessage("t", Array.Empty<byte>())
        {
            Utf8Payload = true,
            ExpiresAt = Now.AddSeconds(2.5),
            ResponseTopic = "r",
            CorrelationData = new byte[] { 9 },
            Headers = [new("a", "1"), new("b", "2")],
            ContentType = "text/plain",
        };

        var properties = Mqtt5BusMessages.PropertiesOf(message, Now);

        CollectionAssert.AreEqual(
            new[]
            {
                Mqtt5PropertyIdentifier.PayloadFormatIndicator,
                Mqtt5PropertyIdentifier.MessageExpiryInterval,
                Mqtt5PropertyIdentifier.ResponseTopic,
                Mqtt5PropertyIdentifier.CorrelationData,
                Mqtt5PropertyIdentifier.UserProperty,
                Mqtt5PropertyIdentifier.UserProperty,
                Mqtt5PropertyIdentifier.ContentType,
            },
            properties.Select(property => property.Identifier).ToArray());
        Assert.AreEqual(3u, properties[1].Integer);
        Assert.AreEqual("r", properties[2].Text);
        CollectionAssert.AreEqual(new byte[] { 9 }, properties[3].Binary);
        Assert.AreEqual(("b", "2"), (properties[5].Text, properties[5].PairValue));
        Assert.AreEqual("text/plain", properties[6].Text);
    }
}
