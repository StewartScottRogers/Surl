using Surl.Cli;
using Surl.Output;

namespace Surl.Composition;

[TestClass]
public sealed class AccountGroupExpansionTests
{
    private static readonly string[] Accounts = ["alice", "bob", "carol"];

    [TestMethod]
    public void Compose_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => AccountGroupExpansion.Compose(null!, Accounts));
        Assert.ThrowsExactly<ArgumentNullException>(() => AccountGroupExpansion.Compose([], null!));
    }

    [TestMethod]
    public void Compose_NoGroups_HasNoGroups()
    {
        var (expansion, failure) = AccountGroupExpansion.Compose([], Accounts);

        Assert.IsNull(failure);
        Assert.IsEmpty(expansion!.Groups);
    }

    [TestMethod]
    public void Compose_AccountMembers_ExpandToThoseAccounts() =>
        AssertExpandsTo(["admins=alice,bob"], ("admins", ["alice", "bob"]));

    [TestMethod]
    public void Compose_NestedGroups_ExpandThroughEveryLevelInCommandLineOrder() =>
        AssertExpandsTo(
            ["all=@admins,carol", "admins=alice,@ops", "ops=bob"],
            ("all", ["alice", "bob", "carol"]),
            ("admins", ["alice", "bob"]),
            ("ops", ["bob"]));

    [TestMethod]
    public void Compose_AnAccountReachedTwice_CountsOnce() =>
        AssertExpandsTo(["ops=alice,bob", "admins=alice,@ops"], ("ops", ["alice", "bob"]), ("admins", ["alice", "bob"]));

    [TestMethod]
    public void Compose_AGroupTwoGroupsInclude_IsExpandedOnce() =>
        AssertExpandsTo(
            ["a=@c", "b=@c", "c=carol"],
            ("a", ["carol"]),
            ("b", ["carol"]),
            ("c", ["carol"]));

    [TestMethod]
    [DataRow(new[] { "ops=dave" }, "(2) option --group: member dave names no account or group", DisplayName = "An unknown account")]
    [DataRow(new[] { "ops=@admins" }, "(2) option --group: member @admins names no account or group", DisplayName = "An unknown group")]
    [DataRow(new[] { "ops=Alice" }, "(2) option --group: member Alice names no account or group", DisplayName = "Account names match exactly")]
    [DataRow(new[] { "ops=alice", "admins=@ops,ops" }, "(2) option --group: member ops names no account or group", DisplayName = "A group name without @")]
    [DataRow(new[] { "ops=@ops" }, "(2) option --group: group ops includes itself", DisplayName = "Directly")]
    [DataRow(new[] { "a=@b", "b=@c", "c=alice,@a" }, "(2) option --group: group a includes itself", DisplayName = "Through other groups")]
    [DataRow(new[] { "x=alice", "a=@b", "b=@a" }, "(2) option --group: group a includes itself", DisplayName = "The first cycle in command-line order")]
    [DataRow(new[] { "everyone=alice" }, "(2) option --group: group everyone is reserved", DisplayName = "A group named everyone")]
    [DataRow(new[] { "ops=alice,@everyone" }, "(2) option --group: member @everyone is not allowed here", DisplayName = "A member @everyone")]
    [DataRow(new[] { "ops=@ops,dave" }, "(2) option --group: member dave names no account or group", DisplayName = "An unknown member before a cycle")]
    [DataRow(new[] { "ops=dave", "everyone=alice" }, "(2) option --group: group everyone is reserved", DisplayName = "Everyone before an unknown member")]
    public void Compose_ARefusedGroup_IsRefusedWithTheAdrsText(string[] groups, string message)
    {
        var (expansion, failure) = AccountGroupExpansion.Compose(Groups(groups), Accounts);

        Assert.IsNull(expansion);
        Assert.AreEqual(message, failure);
    }

    [TestMethod]
    public void Compose_AnAccountNamedEveryone_IsAMemberLikeAnyOther() =>
        Assert.IsNull(AccountGroupExpansion.Compose(Groups(["ops=everyone"]), ["everyone"]).FailureMessage);

    [TestMethod]
    public void Expand_AccountsAndGroups_ReachEachAccountOnce()
    {
        var expansion = Composed(["ops=alice,bob"]);

        var failure = expansion.Expand("share-access", Members("bob", "@ops", "carol"), out var accounts);

        Assert.IsNull(failure);
        CollectionAssert.AreEquivalent(new[] { "alice", "bob", "carol" }, accounts.ToArray());
    }

    [TestMethod]
    [DataRow("dave", "(2) option --share-access: member dave names no account or group", DisplayName = "An unknown account")]
    [DataRow("@admins", "(2) option --share-access: member @admins names no account or group", DisplayName = "An unknown group")]
    public void Expand_AMemberNamingNothing_IsRefusedNamingTheOption(string member, string message)
    {
        var expansion = Composed(["ops=alice"]);

        var failure = expansion.Expand("share-access", Members("@ops", member), out var accounts);

        Assert.AreEqual(message, failure);
        Assert.IsEmpty(accounts);
    }

    [TestMethod]
    public void Expand_NullArguments_Throw()
    {
        var expansion = Composed([]);

        Assert.ThrowsExactly<ArgumentNullException>(() => expansion.Expand(null!, [], out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => expansion.Expand("x", null!, out _));
    }

    [TestMethod]
    public void WriteStartLines_Verbose_NotesEachGroupInCommandLineOrder()
    {
        using var log = new StringWriter();

        Composed(["ops=alice", "admins=@ops,bob"]).WriteStartLines(LogLevel.Verbose, log);

        Assert.AreEqual(
            $"* Group ops: 1 accounts{Environment.NewLine}* Group admins: 2 accounts{Environment.NewLine}",
            log.ToString());
    }

    [TestMethod]
    public void WriteStartLines_BelowVerbose_WritesNothing()
    {
        using var log = new StringWriter();

        Composed(["ops=alice"]).WriteStartLines(LogLevel.Info, log);

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public void WriteStartLines_NullLog_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Composed([]).WriteStartLines(LogLevel.Verbose, null!));

    private static void AssertExpandsTo(string[] groups, params (string Name, string[] Accounts)[] expected)
    {
        var expansion = Composed(groups);

        CollectionAssert.AreEqual(expected.Select(group => group.Name).ToArray(), expansion.Groups.Select(group => group.Key).ToArray());
        foreach (var (name, accounts) in expected)
        {
            CollectionAssert.AreEquivalent(accounts, expansion.Groups.Single(group => group.Key == name).Value.ToArray(), name);
        }
    }

    private static AccountGroupExpansion Composed(string[] groups)
    {
        var (expansion, failure) = AccountGroupExpansion.Compose(Groups(groups), Accounts);

        Assert.IsNull(failure);
        return expansion!;
    }

    // Each group read as surl reads --group, so the tests use the option's own grammar.
    private static ConfiguredGroup[] Groups(string[] arguments)
    {
        var result = CommandLineParser.Parse([.. arguments.SelectMany(argument => new[] { "--group", argument }), "http://127.0.0.1:0"]);

        Assert.IsNotNull(result.CommandLine, result.Failure?.Message);
        return [.. result.CommandLine.Groups];
    }

    private static ConfiguredMember[] Members(params string[] written) =>
        [.. written.Select(member => member.StartsWith('@') ? new ConfiguredMember(member[1..], IsGroup: true) : new ConfiguredMember(member, IsGroup: false))];
}
