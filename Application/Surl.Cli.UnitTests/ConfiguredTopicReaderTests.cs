namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredTopicReader"/> reading <c>&lt;filter&gt;[;retain][;qos=&lt;0|1|2&gt;]</c> items (ADR-0101
/// decisions 3 to 5, ADR-0102 decision 3.1).
/// </summary>
[TestClass]
public sealed class ConfiguredTopicReaderTests
{
    [TestMethod]
    public void Read_FilterAlone_IsNotRetainedAndHasNoQos() =>
        Assert.AreEqual(new ConfiguredTopic("shop/orders", IsRetained: false, MaxQos: null), Accepted("shop/orders"));

    [TestMethod]
    public void Read_Retain_IsRetained() =>
        Assert.AreEqual(new ConfiguredTopic("shop/#", IsRetained: true, MaxQos: null), Accepted("shop/#;retain"));

    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("1", 1)]
    [DataRow("2", 2)]
    public void Read_Qos_IsTheMaxQos(string qos, int expected) =>
        Assert.AreEqual(expected, Accepted("t;qos=" + qos).MaxQos);

    [TestMethod]
    public void Read_RetainAndQosInAnyOrderAndCase_AreBothRead() =>
        Assert.AreEqual(new ConfiguredTopic("+/x", IsRetained: true, MaxQos: 1), Accepted("+/x;QOS=1;Retain"));

    [TestMethod]
    public void Read_PercentEncodedFilter_IsDecoded() =>
        Assert.AreEqual("a;b%", Accepted("a%3Bb%25").Filter);

    [TestMethod]
    [DataRow("", "the name is empty", DisplayName = "Empty filter")]
    [DataRow(";retain", "the name is empty", DisplayName = "Only a flag")]
    [DataRow("t;", "an empty field", DisplayName = "Trailing semicolon")]
    [DataRow("t;qos=3", "is badly used here", DisplayName = "QoS 3")]
    [DataRow("t;qos=01", "is badly used here", DisplayName = "QoS with a leading zero")]
    [DataRow("t;qos=x", "is badly used here", DisplayName = "QoS not a number")]
    [DataRow("t;qos=", "key qos needs a value", DisplayName = "QoS empty")]
    [DataRow("t;qos", "key qos needs a value", DisplayName = "QoS without a value")]
    [DataRow("t;retain=1", "flag retain takes no value", DisplayName = "Retain with a value")]
    [DataRow("t;retain;retain", "key retain is given twice", DisplayName = "Retain twice")]
    [DataRow("t;durable", "unknown flag durable", DisplayName = "Unknown flag")]
    [DataRow("t;ttl=5", "unknown key ttl", DisplayName = "Unknown key")]
    [DataRow("a%01b", "a control character", DisplayName = "Control character")]
    [DataRow("a%zz", "a malformed percent escape", DisplayName = "Malformed escape")]
    public void Read_MalformedItem_IsRefusedWithTheAdrsText(string argument, string expected)
    {
        var failure = ConfiguredTopicReader.Read(argument, out var topic);

        Assert.AreEqual(expected, failure);
        Assert.AreEqual(ConfiguredTopicReader.Empty, topic);
    }

    [TestMethod]
    public void TopicGivenTwice_NamesTheFilter() =>
        Assert.AreEqual("topic shop/# is given twice", ConfiguredTopicReader.TopicGivenTwice("shop/#"));

    [TestMethod]
    public void Reading_ArgumentType_IsNamedByTheShape() =>
        Assert.AreEqual(ConfiguredTopicReader.Shape.Reading.Type, ConfiguredTopicReader.Reading.Type);

    private static ConfiguredTopic Accepted(string argument)
    {
        Assert.IsNull(ConfiguredTopicReader.Read(argument, out var topic));
        return topic;
    }
}
