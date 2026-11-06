namespace Surl.MessageBus;

[TestClass]
public sealed class AmqpTopicExchangeNamesTests
{
    [TestMethod]
    [DataRow("a.b.c", "a/b/c")]
    [DataRow("stock.*", "stock/*")]
    [DataRow("$SYS.uptime", "$SYS/uptime")]
    public void ToCanonicalTopic_RoutingKey_IsLevelsAtSlashAndMapsBack(string routingKey, string topic)
    {
        var mapped = AmqpTopicExchangeNames.ToCanonicalTopic(routingKey);

        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Mapped, topic), mapped);
        Assert.AreEqual(routingKey, AmqpTopicExchangeNames.FromCanonicalTopic(mapped.Name).Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("a..b")]
    [DataRow("a.#")]
    [DataRow("a/b")]
    [DataRow("a+b")]
    [DataRow("a\u0000")]
    public void ToCanonicalTopic_UnroutableRoutingKey_IsInvalidName(string? routingKey)
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.InvalidName, null), AmqpTopicExchangeNames.ToCanonicalTopic(routingKey));
    }

    [TestMethod]
    [DataRow("a.*.c", "a/+/c")]
    [DataRow("a.#", "a/#")]
    [DataRow("#.c", "#/c")]
    [DataRow("a.#.c", "a/#/c")]
    [DataRow("a*.b", "a*/b")]
    public void ToCanonicalFilter_BindingKey_IsTheCanonicalFilterAndMapsBack(string bindingKey, string filter)
    {
        var mapped = AmqpTopicExchangeNames.ToCanonicalFilter(bindingKey);

        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Mapped, filter), mapped);
        Assert.AreEqual(bindingKey, AmqpTopicExchangeNames.FromCanonicalFilter(mapped.Name).Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("a#")]
    [DataRow("a/b")]
    [DataRow("a..b")]
    [DataRow("a+")]
    public void ToCanonicalFilter_BindingKeyTheBusCannotHold_IsInvalidName(string? bindingKey)
    {
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, AmqpTopicExchangeNames.ToCanonicalFilter(bindingKey).Outcome);
    }

    [TestMethod]
    public void ToCanonicalFilter_MultiWordWildcard_MatchesZeroOrMoreWords()
    {
        var levels = CanonicalTopicName.SplitLevels(AmqpTopicExchangeNames.ToCanonicalFilter("a.#").Name!);

        Assert.IsTrue(CanonicalTopicName.FilterMatches(levels, ["a"]));
        Assert.IsTrue(CanonicalTopicName.FilterMatches(levels, ["a", "b", "c"]));
    }

    [TestMethod]
    [DataRow("a//b")]
    [DataRow("/a")]
    [DataRow("a.b/c")]
    public void FromCanonicalTopic_TopicAmqpCannotName_IsUnreachable(string topic)
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Unreachable, null), AmqpTopicExchangeNames.FromCanonicalTopic(topic));
    }

    [TestMethod]
    public void FromCanonicalTopic_AtMaxKeyBytes_IsMapped()
    {
        var topic = new string('a', AmqpTopicExchangeNames.MaxKeyBytes);

        Assert.AreEqual(topic, AmqpTopicExchangeNames.FromCanonicalTopic(topic).Name);
    }

    [TestMethod]
    public void FromCanonicalTopic_PastMaxKeyBytes_IsUnreachable()
    {
        var topic = new string('é', 128);

        Assert.AreEqual(TopicNameMappingOutcome.Unreachable, AmqpTopicExchangeNames.FromCanonicalTopic(topic).Outcome);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("a/+")]
    public void FromCanonicalTopic_NotCanonical_IsInvalidName(string? topic)
    {
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, AmqpTopicExchangeNames.FromCanonicalTopic(topic).Outcome);
    }

    [TestMethod]
    [DataRow("a/*")]
    [DataRow("a.b/#")]
    [DataRow("/#")]
    public void FromCanonicalFilter_FilterAmqpCannotName_IsUnreachable(string filter)
    {
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Unreachable, null), AmqpTopicExchangeNames.FromCanonicalFilter(filter));
    }

    [TestMethod]
    public void FromCanonicalFilter_PastMaxKeyBytes_IsUnreachable()
    {
        var filter = new string('a', AmqpTopicExchangeNames.MaxKeyBytes) + "/#";

        Assert.AreEqual(TopicNameMappingOutcome.Unreachable, AmqpTopicExchangeNames.FromCanonicalFilter(filter).Outcome);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("a+")]
    public void FromCanonicalFilter_NotCanonical_IsInvalidName(string? filter)
    {
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, AmqpTopicExchangeNames.FromCanonicalFilter(filter).Outcome);
    }
}
