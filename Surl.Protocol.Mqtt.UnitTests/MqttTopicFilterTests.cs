namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class MqttTopicFilterTests
{
    [TestMethod]
    [DataRow("t", true)]
    [DataRow("a/b/c", true)]
    [DataRow("/", true)]
    [DataRow("", false)]
    [DataRow("a/+", false)]
    [DataRow("a/#", false)]
    public void IsValidTopicName_FollowsSection47(string topic, bool expected)
    {
        Assert.AreEqual(expected, MqttTopicFilter.IsValidTopicName(topic));
    }

    [TestMethod]
    [DataRow("t", true)]
    [DataRow("#", true)]
    [DataRow("+", true)]
    [DataRow("a/+/b", true)]
    [DataRow("a/#", true)]
    [DataRow("+/+", true)]
    [DataRow("", false)]
    [DataRow("a#", false)]
    [DataRow("a/#/b", false)]
    [DataRow("a+", false)]
    [DataRow("a/b+/c", false)]
    public void IsValidFilter_FollowsSection47(string filter, bool expected)
    {
        Assert.AreEqual(expected, MqttTopicFilter.IsValidFilter(filter));
    }

    [TestMethod]
    [DataRow("t", "t", true)]
    [DataRow("t", "T", false)]
    [DataRow("a/+", "a/1", true)]
    [DataRow("a/+", "a/1/2", false)]
    [DataRow("a/+", "a", false)]
    [DataRow("a/#", "a", true)]
    [DataRow("a/#", "a/1/2", true)]
    [DataRow("a/#", "b/1", false)]
    [DataRow("#", "a/b", true)]
    [DataRow("+/b", "a/b", true)]
    [DataRow("a/b", "a", false)]
    [DataRow("#", "$SYS/x", false)]
    [DataRow("+/x", "$SYS/x", false)]
    [DataRow("$SYS/#", "$SYS/x", true)]
    public void Matches_FollowsSection47(string filter, string topic, bool expected)
    {
        Assert.AreEqual(expected, MqttTopicFilter.Matches(filter, topic));
    }
}
