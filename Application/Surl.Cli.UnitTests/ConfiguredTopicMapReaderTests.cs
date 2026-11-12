namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredTopicMapReader"/> reading <c>&lt;scheme&gt;:&lt;name&gt;=&lt;scheme&gt;:&lt;name&gt;</c> items
/// (ADR-0101 decisions 3 to 5, ADR-0102 decision 3.3).
/// </summary>
[TestClass]
public sealed class ConfiguredTopicMapReaderTests
{
    [TestMethod]
    public void Read_TwoSides_ReadsEachSchemeAndName()
    {
        var map = Accepted("amqp:orders=mqtt:shop/orders");

        Assert.AreEqual(new ConfiguredTopicMap("amqp", "orders", "mqtt", "shop/orders"), map);
        Assert.AreEqual("amqp:orders", map.Source);
    }

    [TestMethod]
    public void Read_UpperCaseSchemes_AreLowerCased() =>
        Assert.AreEqual(new ConfiguredTopicMap("kafka", "Orders", "mqtt", "x"), Accepted("KAFKA:Orders=Mqtt:x"));

    [TestMethod]
    public void Read_NameHoldingAColon_SplitsAtTheFirstColon() =>
        Assert.AreEqual(new ConfiguredTopicMap("amqp", "a:b", "mqtt", "c:d"), Accepted("amqp:a:b=mqtt:c:d"));

    [TestMethod]
    public void Read_EncodedSeparatorInTheName_IsDecodedAfterSplitting() =>
        Assert.AreEqual(new ConfiguredTopicMap("amqp", "a=b", "mqtt", "c;d"), Accepted("amqp:a%3Db=mqtt:c%3Bd"));

    [TestMethod]
    [DataRow("amqp:orders", "expected <scheme>:<name>=<scheme>:<name>", DisplayName = "No separator")]
    [DataRow("orders=mqtt:x", "expected <scheme>:<name>=<scheme>:<name>", DisplayName = "Left side without a scheme")]
    [DataRow("amqp:orders=x", "expected <scheme>:<name>=<scheme>:<name>", DisplayName = "Right side without a scheme")]
    [DataRow("=mqtt:x", "the name is empty", DisplayName = "Empty left side")]
    [DataRow("amqp:orders=", "an empty field", DisplayName = "Empty right side")]
    [DataRow(":orders=mqtt:x", "an empty field", DisplayName = "Empty left scheme")]
    [DataRow("amqp:=mqtt:x", "an empty field", DisplayName = "Empty left name")]
    [DataRow("amqp:orders=:x", "an empty field", DisplayName = "Empty right scheme")]
    [DataRow("amqp:orders=mqtt:", "an empty field", DisplayName = "Empty right name")]
    [DataRow("nosuch:orders=mqtt:x", "unknown scheme nosuch", DisplayName = "Unknown left scheme")]
    [DataRow("amqp:orders=NoSuch:x", "unknown scheme nosuch", DisplayName = "Unknown right scheme")]
    [DataRow("amqp:orders=mqtt:x;retain", "unknown flag retain", DisplayName = "A flag")]
    [DataRow("amqp:or%0Aders=mqtt:x", "a control character", DisplayName = "Control character")]
    public void Read_MalformedItem_IsRefusedWithTheAdrsText(string argument, string expected)
    {
        var failure = ConfiguredTopicMapReader.Read(argument, out var map);

        Assert.AreEqual(expected, failure);
        Assert.AreEqual(ConfiguredTopicMapReader.Empty, map);
    }

    [TestMethod]
    public void TopicMapGivenTwice_NamesTheSource() =>
        Assert.AreEqual("topic map amqp:orders is given twice", ConfiguredTopicMapReader.TopicMapGivenTwice("amqp:orders"));

    [TestMethod]
    public void Reading_ArgumentType_IsNamedByTheShape() =>
        Assert.AreEqual(ConfiguredTopicMapReader.Shape.Reading.Type, ConfiguredTopicMapReader.Reading.Type);

    private static ConfiguredTopicMap Accepted(string argument)
    {
        Assert.IsNull(ConfiguredTopicMapReader.Read(argument, out var map));
        return map;
    }
}
