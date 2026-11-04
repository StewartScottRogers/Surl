using System.Text;

namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class MqttRetainedMessagesTests
{
    [TestMethod]
    public void Retain_NullTopic_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MqttRetainedMessages().Retain(null!, "x"u8));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("a/+")]
    [DataRow("#")]
    public void Retain_InvalidTopic_Throws(string topic)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MqttRetainedMessages().Retain(topic, "x"u8));
    }

    [TestMethod]
    public void Retain_SecondPayload_ReplacesTheFirst()
    {
        var retained = new MqttRetainedMessages();

        retained.Retain("t", "one"u8);
        retained.Retain("t", "two"u8);

        Assert.AreEqual("two", Encoding.ASCII.GetString(retained.MatchingAny(["t"]).Single().Value));
    }

    [TestMethod]
    public void Retain_EmptyPayloadOnAnUnknownTopic_KeepsNothing()
    {
        var retained = new MqttRetainedMessages();

        retained.Retain("t", ReadOnlySpan<byte>.Empty);

        Assert.AreEqual(0, retained.MatchingAny(["#"]).Count);
    }

    [TestMethod]
    public void Constructor_Defaults_Are10000TopicsAnd100MiB()
    {
        var retained = new MqttRetainedMessages();

        Assert.AreEqual(10_000, retained.MaxTopics);
        Assert.AreEqual(104_857_600, retained.MaxTotalPayloadBytes);
    }

    [TestMethod]
    [DataRow(0, 1L)]
    [DataRow(1, 0L)]
    public void Constructor_BoundBelowOne_Throws(int maxTopics, long maxTotalPayloadBytes)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MqttRetainedMessages(maxTopics, maxTotalPayloadBytes));
    }

    [TestMethod]
    public void Retain_NewTopicPastMaxTopics_IsRefusedAndKeepsTheStoreUnchanged()
    {
        var retained = new MqttRetainedMessages(maxTopics: 2);
        retained.Retain("a", "1"u8);
        retained.Retain("b", "2"u8);

        var kept = retained.Retain("c", "3"u8);

        Assert.IsFalse(kept);
        CollectionAssert.AreEqual(new[] { "a", "b" }, retained.MatchingAny(["#"]).Select(message => message.Key).ToArray());
    }

    [TestMethod]
    public void Retain_ReplacingATopicAtMaxTopics_IsKept()
    {
        var retained = new MqttRetainedMessages(maxTopics: 1);
        retained.Retain("a", "1"u8);

        var kept = retained.Retain("a", "2"u8);

        Assert.IsTrue(kept);
        Assert.AreEqual("2", Encoding.ASCII.GetString(retained.MatchingAny(["a"]).Single().Value));
    }

    [TestMethod]
    public void Retain_PastMaxTotalPayloadBytes_IsRefused()
    {
        var retained = new MqttRetainedMessages(maxTotalPayloadBytes: 5);
        retained.Retain("a", "123"u8);

        Assert.IsFalse(retained.Retain("b", "123"u8));
        Assert.IsTrue(retained.Retain("b", "12"u8));
    }

    [TestMethod]
    public void Retain_ReplacementCountsOnlyTheDifference()
    {
        var retained = new MqttRetainedMessages(maxTotalPayloadBytes: 5);
        retained.Retain("a", "1234"u8);

        Assert.IsTrue(retained.Retain("a", "12345"u8));
    }

    [TestMethod]
    public void Retain_EmptyPayload_FreesItsTopicAndBytes()
    {
        var retained = new MqttRetainedMessages(maxTopics: 1, maxTotalPayloadBytes: 3);
        retained.Retain("a", "123"u8);

        retained.Retain("a", ReadOnlySpan<byte>.Empty);

        Assert.IsTrue(retained.Retain("b", "123"u8));
    }

    [TestMethod]
    public void MatchingAny_OverlappingFilters_ReturnsEachTopicOnceInOrdinalOrder()
    {
        var retained = new MqttRetainedMessages();
        retained.Retain("b", "1"u8);
        retained.Retain("B", "2"u8);
        retained.Retain("a", "3"u8);

        var topics = retained.MatchingAny(["#", "b", "+"]).Select(message => message.Key).ToArray();

        CollectionAssert.AreEqual(new[] { "B", "a", "b" }, topics);
    }
}
