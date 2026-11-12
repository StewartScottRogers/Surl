namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredTopicAccessReader"/> reading <c>&lt;filter&gt;=&lt;member&gt;:&lt;pub|sub|pubsub&gt;,...</c> items
/// (ADR-0101 decisions 3 to 5, ADR-0102 decision 3.4).
/// </summary>
[TestClass]
public sealed class ConfiguredTopicAccessReaderTests
{
    [TestMethod]
    public void Read_OneGrant_ReadsTheFilterMemberAndAccess()
    {
        var access = Accepted("shop/#=alice:pub");

        Assert.AreEqual("shop/#", access.Filter);
        CollectionAssert.AreEqual(new[] { new ConfiguredGrant(new("alice", IsGroup: false), "pub") }, access.Grants.ToArray());
    }

    [TestMethod]
    public void Read_ManyGrantsWithGroups_AreReadInOrderAccessLowerCased() =>
        CollectionAssert.AreEqual(
            new[]
            {
                new ConfiguredGrant(new("alice", IsGroup: false), "pub"),
                new ConfiguredGrant(new("ops", IsGroup: true), "sub"),
                new ConfiguredGrant(new("everyone", IsGroup: true), "pubsub"),
            },
            Accepted("$queue/orders=alice:PUB,@ops:sub,@everyone:PubSub").Grants.ToArray());

    [TestMethod]
    public void Read_MemberHoldingAColon_SplitsAtTheLastColon() =>
        Assert.AreEqual(new ConfiguredMember("dom:alice", IsGroup: false), Accepted("t=dom:alice:sub").Grants.Single().Member);

    [TestMethod]
    public void Read_PercentEncodedFields_AreDecoded()
    {
        var access = Accepted("a%3Db=%40alice:pub");

        Assert.AreEqual("a=b", access.Filter);
        Assert.AreEqual(new ConfiguredMember("@alice", IsGroup: false), access.Grants.Single().Member);
    }

    [TestMethod]
    public void Read_SameAccountAndGroupName_AreTwoMembers() =>
        Assert.HasCount(2, Accepted("t=ops:pub,@ops:sub").Grants);

    [TestMethod]
    [DataRow("t", "expected <filter>=<member>:<pub|sub|pubsub>,...", DisplayName = "No separator")]
    [DataRow("t=alice", "expected <filter>=<member>:<pub|sub|pubsub>,...", DisplayName = "No access")]
    [DataRow("=alice:pub", "the name is empty", DisplayName = "Empty filter")]
    [DataRow("t=:pub", "an empty field", DisplayName = "Empty member")]
    [DataRow("t=alice:", "an empty field", DisplayName = "Empty access")]
    [DataRow("t=alice:rw", "is badly used here", DisplayName = "Unknown access word")]
    [DataRow("t=alice:pub,alice:sub", "member alice is given twice", DisplayName = "An account twice")]
    [DataRow("t=@ops:pub,@ops:pubsub", "member @ops is given twice", DisplayName = "A group twice")]
    [DataRow("t=alice:pub;retain", "unknown flag retain", DisplayName = "A flag")]
    [DataRow("t=al%09ice:pub", "a control character", DisplayName = "Control character")]
    public void Read_MalformedItem_IsRefusedWithTheAdrsText(string argument, string expected) =>
        Assert.AreEqual(expected, ConfiguredTopicAccessReader.Read(argument, out _));

    [TestMethod]
    public void Reading_ArgumentType_IsNamedByTheShape() =>
        Assert.AreEqual(ConfiguredTopicAccessReader.Shape.Reading.Type, ConfiguredTopicAccessReader.Reading.Type);

    private static ConfiguredTopicAccess Accepted(string argument)
    {
        Assert.IsNull(ConfiguredTopicAccessReader.Read(argument, out var access));
        return access;
    }
}
