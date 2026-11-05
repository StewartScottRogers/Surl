namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

[TestClass]
public sealed class Mqtt5PropertyTests
{
    [TestMethod]
    public void FromInteger_StringProperty_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ContentType, 1));
    }

    [TestMethod]
    public void FromText_IntegerProperty_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Mqtt5Property.FromText(Mqtt5PropertyIdentifier.TopicAlias, "x"));
    }

    [TestMethod]
    public void FromBinary_StringProperty_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Mqtt5Property.FromBinary(Mqtt5PropertyIdentifier.ReasonString, []));
    }

    [TestMethod]
    public void FromUserProperty_NameAndValue_HoldsBoth()
    {
        var property = Mqtt5Property.FromUserProperty("n", "v");

        Assert.AreEqual(Mqtt5PropertyIdentifier.UserProperty, property.Identifier);
        Assert.AreEqual(Mqtt5PropertyType.Utf8StringPair, property.Type);
        Assert.AreEqual("n", property.Text);
        Assert.AreEqual("v", property.PairValue);
    }
}
