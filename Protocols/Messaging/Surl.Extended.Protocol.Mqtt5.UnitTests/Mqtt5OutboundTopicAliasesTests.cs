namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The Topic Aliases the broker uses toward a client (ADR-0093 decision 4).
/// </summary>
[TestClass]
public sealed class Mqtt5OutboundTopicAliasesTests
{
    [TestMethod]
    public void Assign_ClientAllowsNone_SendsTheTopicNameWithNoAlias()
    {
        var aliases = new Mqtt5OutboundTopicAliases(0);

        Assert.AreEqual(("t", (Mqtt5Property?)null), aliases.Assign("t"));
        Assert.AreEqual(("t", (Mqtt5Property?)null), aliases.Assign("t"));
    }

    [TestMethod]
    public void Assign_MaximumUsedUp_ReassignsTheAliasAssignedLongestAgo()
    {
        var aliases = new Mqtt5OutboundTopicAliases(2);

        var first = aliases.Assign("a");
        var second = aliases.Assign("b");
        var repeat = aliases.Assign("a");
        var third = aliases.Assign("c");
        var afterReassignment = aliases.Assign("a");

        Assert.AreEqual(("a", 1u), (first.TopicName, first.Alias!.Integer));
        Assert.AreEqual(("b", 2u), (second.TopicName, second.Alias!.Integer));
        Assert.AreEqual((string.Empty, 1u), (repeat.TopicName, repeat.Alias!.Integer));
        Assert.AreEqual(("c", 1u), (third.TopicName, third.Alias!.Integer));
        Assert.AreEqual(("a", 2u), (afterReassignment.TopicName, afterReassignment.Alias!.Integer));
    }
}
