namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredMessagingConflicts"/>: the duplicates among <c>--topic</c>, <c>--queue</c>,
/// <c>--topic-map</c> and <c>--topic-access</c> (ADR-0101 decision 5 row 14).
/// </summary>
[TestClass]
public sealed class ConfiguredMessagingConflictsTests
{
    [TestMethod]
    public void Find_NothingGiven_FindsNone() =>
        Assert.IsNull(ConfiguredMessagingConflicts.Find([], [], [], []));

    [TestMethod]
    public void Find_DistinctItems_FindsNone() =>
        Assert.IsNull(ConfiguredMessagingConflicts.Find(
            [Topic("--topic", "a/#"), Topic("--topic", "A/#")],
            [Queue("--queue", "orders"), Queue("--queue", "a/#")],
            [Map("--topic-map", "amqp", "orders"), Map("--topic-map", "kafka", "orders")],
            [Access("--topic-access", "a/#"), Access("--topic-access", "$queue/orders")]));

    [TestMethod]
    public void Find_TopicTwice_NamesTheLaterOption() =>
        Assert.AreEqual(
            new ConfiguredMessagingConflicts.Refusal("topic", "topic a/# is given twice"),
            ConfiguredMessagingConflicts.Find([Topic("--topic", "a/#"), Topic("topic", "a/#")], [], [], []));

    [TestMethod]
    public void Find_QueueTwice_IsRefused() =>
        Assert.AreEqual(
            new ConfiguredMessagingConflicts.Refusal("--queue", "queue orders is given twice"),
            ConfiguredMessagingConflicts.Find([], [Queue("--queue", "orders"), Queue("--queue", "orders")], [], []));

    [TestMethod]
    public void Find_TopicMapSourceTwice_IsRefused() =>
        Assert.AreEqual(
            new ConfiguredMessagingConflicts.Refusal("--topic-map", "topic map amqp:orders is given twice"),
            ConfiguredMessagingConflicts.Find([], [], [Map("--topic-map", "amqp", "orders"), Map("--topic-map", "amqp", "orders")], []));

    [TestMethod]
    public void Find_TopicAccessFilterTwice_IsRefused() =>
        Assert.AreEqual(
            new ConfiguredMessagingConflicts.Refusal("--topic-access", "topic a/# is given twice"),
            ConfiguredMessagingConflicts.Find([], [], [], [Access("--topic-access", "a/#"), Access("--topic-access", "a/#")]));

    [TestMethod]
    public void Find_SeveralRepeats_RefusesTheTopicFirst() =>
        Assert.AreEqual(
            "topic t is given twice",
            ConfiguredMessagingConflicts.Find(
                [Topic("--topic", "t"), Topic("--topic", "t")],
                [Queue("--queue", "q"), Queue("--queue", "q")],
                [],
                [])?.Reason);

    [TestMethod]
    public void Find_NullList_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfiguredMessagingConflicts.Find(null!, [], [], []));

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Find_OtherNullList_Throws(int which) =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfiguredMessagingConflicts.Find(
            [],
            which == 1 ? null! : [],
            which == 2 ? null! : [],
            which == 3 ? null! : []));

    private static (string, ConfiguredTopic) Topic(string optionName, string filter) =>
        (optionName, new ConfiguredTopic(filter, IsRetained: false, MaxQos: null));

    private static (string, ConfiguredQueue) Queue(string optionName, string name) =>
        (optionName, new ConfiguredQueue(name, IsDurable: false));

    private static (string, ConfiguredTopicMap) Map(string optionName, string scheme, string name) =>
        (optionName, new ConfiguredTopicMap(scheme, name, "mqtt", "x"));

    private static (string, ConfiguredTopicAccess) Access(string optionName, string filter) =>
        (optionName, new ConfiguredTopicAccess(filter, [new ConfiguredGrant(new("alice", IsGroup: false), "pub")]));
}
