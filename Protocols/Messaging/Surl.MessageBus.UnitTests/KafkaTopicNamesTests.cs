namespace Surl.MessageBus;

[TestClass]
public sealed class KafkaTopicNamesTests
{
    [TestMethod]
    [DataRow("a.b.c", "a/b/c")]
    [DataRow("a..b", "a//b")]
    [DataRow("Orders_2026-11", "Orders_2026-11")]
    public void ToCanonicalTopic_KafkaTopic_IsLevelsAtSlashAndMapsBack(string kafkaTopic, string topic)
    {
        var mapped = KafkaTopicNames.ToCanonicalTopic(kafkaTopic);

        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Mapped, topic), mapped);
        Assert.AreEqual(kafkaTopic, KafkaTopicNames.FromCanonicalTopic(mapped.Name).Name);
    }

    [TestMethod]
    public void ToCanonicalTopic_AtMaxTopicLength_IsMapped()
    {
        var kafkaTopic = new string('a', KafkaTopicNames.MaxTopicLength);

        Assert.AreEqual(kafkaTopic, KafkaTopicNames.ToCanonicalTopic(kafkaTopic).Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow("a b")]
    [DataRow("é")]
    [DataRow("a/b")]
    [DataRow("a+")]
    public void ToCanonicalTopic_NotAKafkaTopicName_IsInvalidName(string? kafkaTopic)
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.InvalidName, null), KafkaTopicNames.ToCanonicalTopic(kafkaTopic));
    }

    [TestMethod]
    public void ToCanonicalTopic_PastMaxTopicLength_IsInvalidName()
    {
        var kafkaTopic = new string('a', KafkaTopicNames.MaxTopicLength + 1);

        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, KafkaTopicNames.ToCanonicalTopic(kafkaTopic).Outcome);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(-1)]
    public void ToCanonicalTopic_PartitionOtherThanZero_IsUnknownPartition(int partition)
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.UnknownPartition, null), KafkaTopicNames.ToCanonicalTopic("a.b", partition));
    }

    [TestMethod]
    public void ToCanonicalTopic_InvalidNameAndOtherPartition_IsInvalidName()
    {
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, KafkaTopicNames.ToCanonicalTopic("a b", 1).Outcome);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("//")]
    [DataRow("a.b/c")]
    [DataRow("$SYS/uptime")]
    [DataRow("a b")]
    [DataRow("é")]
    public void FromCanonicalTopic_TopicKafkaCannotName_IsUnreachable(string topic)
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Unreachable, null), KafkaTopicNames.FromCanonicalTopic(topic));
    }

    [TestMethod]
    public void FromCanonicalTopic_PastMaxTopicLength_IsUnreachable()
    {
        var topic = new string('a', KafkaTopicNames.MaxTopicLength + 1);

        Assert.AreEqual(TopicNameMappingOutcome.Unreachable, KafkaTopicNames.FromCanonicalTopic(topic).Outcome);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("a/+")]
    public void FromCanonicalTopic_NotCanonical_IsInvalidName(string? topic)
    {
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, KafkaTopicNames.FromCanonicalTopic(topic).Outcome);
    }
}
