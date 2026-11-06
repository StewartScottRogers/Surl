namespace Surl.MessageBus;

[TestClass]
public sealed class MqttTopicNamesTests
{
    [TestMethod]
    [DataRow("a/b")]
    [DataRow("$SYS/uptime")]
    [DataRow("a//b")]
    public void ToCanonicalTopic_MqttTopic_IsTheSameNameAndMapsBack(string topic)
    {
        var mapped = MqttTopicNames.ToCanonicalTopic(topic);

        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Mapped, topic), mapped);
        Assert.AreEqual(topic, MqttTopicNames.FromCanonicalTopic(mapped.Name).Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("a/+")]
    [DataRow("a/#")]
    public void ToCanonicalTopic_NotAnMqttTopic_IsInvalidName(string? topic)
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.InvalidName, null), MqttTopicNames.ToCanonicalTopic(topic));
    }

    [TestMethod]
    public void FromCanonicalTopic_NotCanonical_IsInvalidName()
    {
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, MqttTopicNames.FromCanonicalTopic("a/+").Outcome);
    }

    [TestMethod]
    [DataRow("a/+/c")]
    [DataRow("a/#")]
    [DataRow("#")]
    [DataRow("$share/g/a")]
    public void ToCanonicalFilter_MqttFilter_IsTheSameFilterAndMapsBack(string filter)
    {
        var mapped = MqttTopicNames.ToCanonicalFilter(filter);

        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Mapped, filter), mapped);
        Assert.AreEqual(filter, MqttTopicNames.FromCanonicalFilter(mapped.Name).Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("a/#/c")]
    [DataRow("a+")]
    public void ToCanonicalFilter_NotAnMqttFilter_IsInvalidName(string? filter)
    {
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, MqttTopicNames.ToCanonicalFilter(filter).Outcome);
    }

    [TestMethod]
    public void FromCanonicalFilter_MultiLevelWildcardBeforeTheLastLevel_IsUnreachable()
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Unreachable, null), MqttTopicNames.FromCanonicalFilter("a/#/c"));
    }

    [TestMethod]
    public void FromCanonicalFilter_NotCanonical_IsInvalidName()
    {
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, MqttTopicNames.FromCanonicalFilter("a+").Outcome);
    }

    [TestMethod]
    public void ToCanonicalSharedSubscription_SharedFilter_IsTheFilterInTheShareNamesGroupAndMapsBack()
    {
        var mapped = MqttTopicNames.ToCanonicalSharedSubscription("$share/workers/a/+/c");

        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Mapped, "a/+/c", "workers"), mapped);
        Assert.AreEqual("$share/workers/a/+/c", MqttTopicNames.FromCanonicalSharedSubscription(mapped.Name, mapped.Group!).Name);
    }

    [TestMethod]
    public void ToCanonicalSharedSubscription_UnsharedFilter_HasNoGroup()
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Mapped, "a/#"), MqttTopicNames.ToCanonicalSharedSubscription("a/#"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("$share/g")]
    [DataRow("$share//a")]
    [DataRow("$share/g+/a")]
    [DataRow("$share/g#/a")]
    [DataRow("$share/g/")]
    [DataRow("$share/g/a/#/c")]
    public void ToCanonicalSharedSubscription_InvalidSharedSubscription_IsInvalidName(string? filter)
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.InvalidName, null), MqttTopicNames.ToCanonicalSharedSubscription(filter));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("g/h")]
    [DataRow("g+")]
    [DataRow("g#")]
    public void FromCanonicalSharedSubscription_GroupMqttCannotName_IsUnreachable(string group)
    {
        Assert.AreEqual(TopicNameMappingOutcome.Unreachable, MqttTopicNames.FromCanonicalSharedSubscription("a", group).Outcome);
    }

    [TestMethod]
    public void FromCanonicalSharedSubscription_FilterMqttCannotName_IsTheFiltersRefusal()
    {
        Assert.AreEqual(TopicNameMappingOutcome.Unreachable, MqttTopicNames.FromCanonicalSharedSubscription("a/#/c", "g").Outcome);
    }

    [TestMethod]
    public void FromCanonicalSharedSubscription_NullGroup_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => MqttTopicNames.FromCanonicalSharedSubscription("a", null!));
    }
}
