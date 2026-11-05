using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary><c>--group</c> read into <see cref="SurlCommandLine.Groups"/> (ADR-0101 decisions 3 to 6, BL-646).</summary>
[TestClass]
public sealed class CommandLineParserGroupTests
{
    private const string Url = "http://127.0.0.1:8080/";

    [TestMethod]
    public void Parse_NoGroup_HasNoGroups() =>
        Assert.IsEmpty(Served(Url).Groups);

    [TestMethod]
    public void Parse_OneGroup_ReadsItsNameAndMembersInOrder()
    {
        var group = Served("--group", "admins=alice,@ops,bob", Url).Groups.Single();

        Assert.AreEqual("admins", group.Name);
        CollectionAssert.AreEqual(
            new[] { new ConfiguredMember("alice", IsGroup: false), new ConfiguredMember("ops", IsGroup: true), new ConfiguredMember("bob", IsGroup: false) },
            group.Members.ToArray());
    }

    [TestMethod]
    public void Parse_SeveralGroups_EachAddAGroupInCommandLineOrder() =>
        CollectionAssert.AreEqual(
            new[] { "ops", "admins" },
            Served("--group", "ops=carol", Url, "--group=admins=alice,@ops").Groups.Select(group => group.Name).ToArray());

    [TestMethod]
    public void Parse_GroupFields_ArePercentDecoded()
    {
        var group = Served("--group", "a%3Db=%40alice,c%2Cd", Url).Groups.Single();

        Assert.AreEqual("a=b", group.Name);
        CollectionAssert.AreEqual(
            new[] { new ConfiguredMember("@alice", IsGroup: false), new ConfiguredMember("c,d", IsGroup: false) },
            group.Members.ToArray());
    }

    [TestMethod]
    public void Parse_GroupNamesDifferingOnlyInCase_AreTwoGroups() =>
        Assert.HasCount(2, Served("--group", "ops=a", "--group", "OPS=a", Url).Groups);

    [TestMethod]
    [DataRow(new[] { "--group", "ops=a", "--group", "ops=b" }, "option --group: group ops is given twice", DisplayName = "A name twice")]
    [DataRow(new[] { "--group=x=a", "--group=ops=a", "--group=ops=b" }, "option --group=ops=b: group ops is given twice", DisplayName = "Named as written")]
    [DataRow(new[] { "--group", "ops=alice,bob,alice" }, "option --group: member alice is given twice", DisplayName = "An account member twice")]
    [DataRow(new[] { "--group", "ops=@x,@x" }, "option --group: member @x is given twice", DisplayName = "A group member twice")]
    [DataRow(new[] { "--group", "ops" }, "option --group: expected <name>=<member>,...", DisplayName = "No separator")]
    [DataRow(new[] { "--group", "=alice" }, "option --group: the name is empty", DisplayName = "Empty name")]
    [DataRow(new[] { "--group", "ops=" }, "option --group: an empty field", DisplayName = "No member")]
    [DataRow(new[] { "--group", "ops=alice," }, "option --group: an empty field", DisplayName = "Trailing comma")]
    [DataRow(new[] { "--group", "ops=@" }, "option --group: an empty field", DisplayName = "Bare @")]
    [DataRow(new[] { "--group", "ops=alice;admin" }, "option --group: unknown flag admin", DisplayName = "A flag")]
    [DataRow(new[] { "--group", "ops=alice;k=v" }, "option --group: unknown key k", DisplayName = "A key")]
    [DataRow(new[] { "--group", "ops=al%zzice" }, "option --group: a malformed percent escape", DisplayName = "Bad escape")]
    [DataRow(new[] { "--group", "ops=al\tice" }, "option --group: a control character", DisplayName = "Control character")]
    public void Parse_GroupRefused_IsRefusedWithItsReason(string[] arguments, string message) =>
        AssertOptionRefused([.. arguments, Url], message);

    [TestMethod]
    public void Parse_GroupMissingItsArgument_RequiresParameter() =>
        AssertOptionRefused([Url, "--group"], "option --group: requires parameter");

    [TestMethod]
    public void Parse_GroupItemPastItsLengthBound_IsRefused() =>
        AssertOptionRefused(["--group", "ops=" + new string('a', 4093), Url], "option --group: the item is longer than 4096 characters");

    [TestMethod]
    public void Parse_GroupListPastItsMemberBound_IsRefused() =>
        AssertOptionRefused(
            ["--group", "ops=" + string.Join(',', Enumerable.Repeat("a", 1001)), Url],
            "option --group: more than 1000 members");

    [TestMethod]
    public void Parse_GroupGivenTenThousandTimes_IsServed() =>
        Assert.HasCount(10000, Served([.. GroupArguments(10000), Url]).Groups);

    [TestMethod]
    public void Parse_GroupGivenMoreThanTenThousandTimes_IsRefused() =>
        AssertOptionRefused([.. GroupArguments(10001), Url], "option --group: given more than 10000 times");

    [TestMethod]
    public void Parse_GroupNameGivenTwice_IsCheckedAfterTheWholeLine() =>
        AssertOptionRefused(["--group", "a=x", "--group", "a=y", "--nosuch", Url], "option --nosuch: is unknown");

    [TestMethod]
    public void Parse_NegatedGroup_CannotBeReversed() =>
        AssertOptionRefused(["--no-group", Url], "option --no-group: the given option cannot be reversed with a --no- prefix");

    [TestMethod]
    [DataRow("alice", false, "alice")]
    [DataRow("ops", true, "@ops")]
    public void AsWritten_Member_IsAsARefusalNamesIt(string name, bool isGroup, string written) =>
        Assert.AreEqual(written, new ConfiguredMember(name, isGroup).AsWritten);

    private static IEnumerable<string> GroupArguments(int count) =>
        Enumerable.Range(0, count).SelectMany(index => new[] { "--group", $"g{index}=a" });

    private static SurlCommandLine Served(params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        Assert.IsNotNull(result.CommandLine);
        return result.CommandLine;
    }

    private static void AssertOptionRefused(string[] arguments, string message)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(SurlExitCode.FailedInit, result.Failure.ExitCode);
        Assert.AreEqual(message, result.Failure.Message);
        Assert.IsTrue(result.Failure.FollowedByTryHelpLine);
    }
}
