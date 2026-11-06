namespace Surl.MessageBus;

[TestClass]
public sealed class NatsSubjectNamesTests
{
    [TestMethod]
    [DataRow("a.b.c", "a/b/c")]
    [DataRow("$SYS.uptime", "$SYS/uptime")]
    [DataRow("a*b.c>", "a*b/c>")]
    public void ToCanonicalTopic_Subject_IsLevelsAtSlashAndMapsBack(string subject, string topic)
    {
        var mapped = NatsSubjectNames.ToCanonicalTopic(subject);

        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Mapped, topic), mapped);
        Assert.AreEqual(subject, NatsSubjectNames.FromCanonicalTopic(mapped.Name).Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("a..b")]
    [DataRow(".a")]
    [DataRow("a.")]
    [DataRow("a.*")]
    [DataRow("a.>")]
    [DataRow("a b")]
    [DataRow("a\tb")]
    [DataRow("a/b")]
    [DataRow("a+b")]
    [DataRow("a#b")]
    [DataRow("a\u0000")]
    public void ToCanonicalTopic_NotAPublishSubjectTheBusCanHold_IsInvalidName(string? subject)
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.InvalidName, null), NatsSubjectNames.ToCanonicalTopic(subject));
    }

    [TestMethod]
    [DataRow("a.*.c", "a/+/c")]
    [DataRow("a.>", "a/+/#")]
    [DataRow(">", "+/#")]
    [DataRow("*.b.>", "+/b/+/#")]
    [DataRow("a.b", "a/b")]
    public void ToCanonicalFilter_Subject_IsTheCanonicalFilterAndMapsBack(string subject, string filter)
    {
        var mapped = NatsSubjectNames.ToCanonicalFilter(subject);

        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Mapped, filter), mapped);
        Assert.AreEqual(subject, NatsSubjectNames.FromCanonicalFilter(mapped.Name).Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("a.>.c")]
    [DataRow(">.a")]
    [DataRow("a..b")]
    [DataRow("a/b")]
    [DataRow("a+.b")]
    public void ToCanonicalFilter_NotASubscriptionSubjectTheBusCanHold_IsInvalidName(string? subject)
    {
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, NatsSubjectNames.ToCanonicalFilter(subject).Outcome);
    }

    [TestMethod]
    public void ToCanonicalFilter_FullWildcard_MatchesOneOrMoreLevelsButNotNone()
    {
        var levels = CanonicalTopicName.SplitLevels(NatsSubjectNames.ToCanonicalFilter("a.>").Name!);

        Assert.IsTrue(CanonicalTopicName.FilterMatches(levels, ["a", "b"]));
        Assert.IsTrue(CanonicalTopicName.FilterMatches(levels, ["a", "b", "c"]));
        Assert.IsFalse(CanonicalTopicName.FilterMatches(levels, ["a"]));
    }

    [TestMethod]
    [DataRow("a//b")]
    [DataRow("/a")]
    [DataRow("a/")]
    [DataRow("a.b/c")]
    [DataRow("a/*")]
    [DataRow("a/>")]
    [DataRow("a b/c")]
    public void FromCanonicalTopic_TopicNatsCannotName_IsUnreachable(string topic)
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Unreachable, null), NatsSubjectNames.FromCanonicalTopic(topic));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("a/+")]
    public void FromCanonicalTopic_NotCanonical_IsInvalidName(string? topic)
    {
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, NatsSubjectNames.FromCanonicalTopic(topic).Outcome);
    }

    [TestMethod]
    [DataRow("#")]
    [DataRow("a/#")]
    [DataRow("a/#/c")]
    [DataRow("a//+/#")]
    [DataRow("a/*")]
    [DataRow("a.b/+")]
    public void FromCanonicalFilter_FilterNatsCannotName_IsUnreachable(string filter)
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Unreachable, null), NatsSubjectNames.FromCanonicalFilter(filter));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("a+")]
    public void FromCanonicalFilter_NotCanonical_IsInvalidName(string? filter)
    {
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, NatsSubjectNames.FromCanonicalFilter(filter).Outcome);
    }
}
