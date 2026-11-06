namespace Surl.MessageBus;

[TestClass]
public sealed class CanonicalTopicNameTests
{
    [TestMethod]
    [DataRow("a")]
    [DataRow("a/b/c")]
    [DataRow("/")]
    [DataRow("a//b")]
    [DataRow("$SYS/uptime")]
    [DataRow("Entwürfe/📧")]
    public void IsValidTopic_CanonicalName_IsTrue(string name)
    {
        Assert.IsTrue(CanonicalTopicName.IsValidTopic(name));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("a/+")]
    [DataRow("a/#")]
    [DataRow("a+b")]
    [DataRow("nul\u0000")]
    public void IsValidTopic_NotCanonical_IsFalse(string? name)
    {
        Assert.IsFalse(CanonicalTopicName.IsValidTopic(name));
    }

    [TestMethod]
    public void IsValidTopic_LoneSurrogate_IsFalse()
    {
        var name = "lone " + (char)0xD800 + " surrogate";

        Assert.IsFalse(CanonicalTopicName.IsValidTopic(name));
    }

    [TestMethod]
    public void IsValidTopic_AtMaxBytes_IsTrue()
    {
        Assert.IsTrue(CanonicalTopicName.IsValidTopic(new string('a', CanonicalTopicName.MaxBytes)));
    }

    [TestMethod]
    public void IsValidTopic_MoreCharactersThanMaxBytes_IsFalse()
    {
        Assert.IsFalse(CanonicalTopicName.IsValidTopic(new string('a', CanonicalTopicName.MaxBytes + 1)));
    }

    [TestMethod]
    public void IsValidTopic_FewCharactersButMoreUtf8BytesThanMaxBytes_IsFalse()
    {
        var name = new string('é', 40_000);

        Assert.IsFalse(CanonicalTopicName.IsValidTopic(name));
    }

    [TestMethod]
    [DataRow("a/b")]
    [DataRow("+")]
    [DataRow("#")]
    [DataRow("a/+/c")]
    [DataRow("a/#")]
    [DataRow("#/b")]
    [DataRow("+/+/#")]
    public void IsValidFilter_CanonicalFilter_IsTrue(string filter)
    {
        Assert.IsTrue(CanonicalTopicName.IsValidFilter(filter));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("a/b+")]
    [DataRow("a/#b")]
    [DataRow("++")]
    [DataRow("nul\u0000/#")]
    public void IsValidFilter_NotCanonical_IsFalse(string? filter)
    {
        Assert.IsFalse(CanonicalTopicName.IsValidFilter(filter));
    }

    [TestMethod]
    [DataRow("a/b", "a/b")]
    [DataRow("a/+", "a/b")]
    [DataRow("a/+", "a/")]
    [DataRow("+/+", "/")]
    [DataRow("#", "a/b/c")]
    [DataRow("a/#", "a")]
    [DataRow("a/#", "a/b/c")]
    [DataRow("a/#/c", "a/c")]
    [DataRow("a/#/c", "a/b/x/c")]
    [DataRow("#/c", "c")]
    [DataRow("$SYS/#", "$SYS/uptime")]
    [DataRow("+/+/#/#", "a/b")]
    public void FilterMatches_MatchingTopic_IsTrue(string filter, string topic)
    {
        Assert.IsTrue(Matches(filter, topic));
    }

    [TestMethod]
    [DataRow("a/b", "a/c")]
    [DataRow("a/b", "a/b/c")]
    [DataRow("a/b/c", "a/b")]
    [DataRow("a/+", "a")]
    [DataRow("a/+", "a/b/c")]
    [DataRow("A", "a")]
    [DataRow("a/#/c", "a/b/d")]
    [DataRow("#", "$SYS/uptime")]
    [DataRow("+/uptime", "$SYS/uptime")]
    public void FilterMatches_OtherTopic_IsFalse(string filter, string topic)
    {
        Assert.IsFalse(Matches(filter, topic));
    }

    [TestMethod]
    public void FilterMatches_ManyMultiLevelWildcardsAgainstALongTopic_AnswersWithoutBacktracking()
    {
        var filter = string.Join('/', Enumerable.Repeat("#", 200)) + "/x";
        var topic = string.Join('/', Enumerable.Repeat("a", 200));

        Assert.IsFalse(Matches(filter, topic));
    }

    private static bool Matches(string filter, string topic) =>
        CanonicalTopicName.FilterMatches(CanonicalTopicName.SplitLevels(filter), CanonicalTopicName.SplitLevels(topic));
}
