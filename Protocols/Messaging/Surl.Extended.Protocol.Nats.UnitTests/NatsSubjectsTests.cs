namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>The subject rules, written by hand from the subjects page.</summary>
[TestClass]
public sealed class NatsSubjectsTests
{
    [TestMethod]
    [DataRow("foo.bar", "foo.bar", true)]
    [DataRow("foo.*", "foo.bar", true)]
    [DataRow("foo.*", "foo.bar.baz", false)]
    [DataRow("foo.*", "foo", false)]
    [DataRow("foo.>", "foo.bar.baz", true)]
    [DataRow("foo.>", "foo", false)]
    [DataRow(">", "foo", true)]
    [DataRow("*.bar", "foo.bar", true)]
    [DataRow("foo.bar", "foo.baz", false)]
    [DataRow("foo", "foo.bar", false)]
    public void Matches_SubscriptionAndSubject_IsWhatTheSubjectsPageSays(string subscription, string subject, bool expected)
    {
        Assert.AreEqual(expected, NatsSubjects.Matches(subscription, subject));
    }

    [TestMethod]
    [DataRow("foo.*.>", true)]
    [DataRow("foo.bar", true)]
    [DataRow("foo*", false)]
    [DataRow("foo.b>", false)]
    public void HasOnlyWholeTokenWildcards_Subject_IsTrueOnlyForWholeTokenWildcards(string subject, bool expected)
    {
        Assert.AreEqual(expected, NatsSubjects.HasOnlyWholeTokenWildcards(subject));
    }
}
