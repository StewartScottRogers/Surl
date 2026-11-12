namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredShareAccessReader"/> reading <c>&lt;share&gt;=&lt;member&gt;:&lt;r|rw&gt;,...</c> items
/// (ADR-0101 decisions 3 to 5, ADR-0102 decision 2.2).
/// </summary>
[TestClass]
public sealed class ConfiguredShareAccessReaderTests
{
    [TestMethod]
    public void Read_OneGrant_ReadsTheShareMemberAndAccess()
    {
        var access = Accepted("team=alice:rw");

        Assert.AreEqual("team", access.Share);
        CollectionAssert.AreEqual(new[] { new ConfiguredGrant(new("alice", IsGroup: false), "rw") }, access.Grants.ToArray());
    }

    [TestMethod]
    public void Read_ManyGrantsWithAGroup_AreReadInOrderAccessLowerCased() =>
        CollectionAssert.AreEqual(
            new[]
            {
                new ConfiguredGrant(new("alice", IsGroup: false), "r"),
                new ConfiguredGrant(new("ops", IsGroup: true), "rw"),
                new ConfiguredGrant(new("everyone", IsGroup: true), "r"),
            },
            Accepted("team=alice:R,@ops:rw,@everyone:r").Grants.ToArray());

    [TestMethod]
    public void Read_MemberHoldingAColon_SplitsAtTheLastColon() =>
        Assert.AreEqual(new ConfiguredMember("dom:alice", IsGroup: false), Accepted("team=dom:alice:r").Grants.Single().Member);

    [TestMethod]
    public void Read_PercentEncodedFields_AreDecoded()
    {
        var access = Accepted("a%3Db=%40alice:r,c%2Cd:rw");

        Assert.AreEqual("a=b", access.Share);
        CollectionAssert.AreEqual(
            new[] { new ConfiguredGrant(new("@alice", IsGroup: false), "r"), new ConfiguredGrant(new("c,d", IsGroup: false), "rw") },
            access.Grants.ToArray());
    }

    [TestMethod]
    [DataRow("team", "expected <share>=<member>:<r|rw>,...", DisplayName = "No separator")]
    [DataRow("team=alice", "expected <share>=<member>:<r|rw>,...", DisplayName = "No access")]
    [DataRow("=alice:r", "the name is empty", DisplayName = "Empty share")]
    [DataRow("team=", "expected <share>=<member>:<r|rw>,...", DisplayName = "No grant")]
    [DataRow("team=alice:r,", "expected <share>=<member>:<r|rw>,...", DisplayName = "Trailing comma")]
    [DataRow("team=:r", "an empty field", DisplayName = "Empty member")]
    [DataRow("team=alice:", "an empty field", DisplayName = "Empty access")]
    [DataRow("team=alice:w", "is badly used here", DisplayName = "Unknown access word")]
    [DataRow("team=alice:r,alice:rw", "member alice is given twice", DisplayName = "An account twice")]
    [DataRow("team=@ops:r,@ops:r", "member @ops is given twice", DisplayName = "A group twice")]
    [DataRow("team=alice:r;readonly", "unknown flag readonly", DisplayName = "A flag")]
    [DataRow("team=al%09ice:r", "a control character", DisplayName = "Control character")]
    public void Read_MalformedItem_IsRefusedWithTheAdrsText(string argument, string expected) =>
        Assert.AreEqual(expected, ConfiguredShareAccessReader.Read(argument, out _));

    [TestMethod]
    public void Read_SameAccountAndGroupName_AreTwoMembers() =>
        Assert.HasCount(2, Accepted("team=ops:r,@ops:rw").Grants);

    [TestMethod]
    public void Reading_ArgumentType_IsNamedByTheShape() =>
        Assert.AreEqual(ConfiguredShareAccessReader.Shape.Reading.Type, ConfiguredShareAccessReader.Reading.Type);

    private static ConfiguredShareAccess Accepted(string argument)
    {
        Assert.IsNull(ConfiguredShareAccessReader.Read(argument, out var access));
        return access;
    }
}
