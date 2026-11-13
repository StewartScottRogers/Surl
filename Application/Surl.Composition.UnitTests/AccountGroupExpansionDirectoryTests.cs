using Surl.Cli;
using Surl.Output;

namespace Surl.Composition;

/// <summary>
/// <see cref="AccountGroupExpansion"/> when <c>--user-ldap</c> is given: a member naming no configured account is
/// taken as a directory account (ADR-0149 decision 8, amending ADR-0102 decision 4.1; BL-673).
/// </summary>
[TestClass]
public sealed class AccountGroupExpansionDirectoryTests
{
    private static readonly string[] Accounts = ["alice"];

    [TestMethod]
    public void Compose_UnknownMemberWithADirectory_IsAcceptedAsADirectoryAccount()
    {
        var (expansion, failure) = AccountGroupExpansion.Compose(Groups("ops=alice,dave,erin"), Accounts, takesUnknownNamesAsDirectoryAccounts: true);

        Assert.IsNull(failure);
        CollectionAssert.AreEquivalent(new[] { "alice", "dave", "erin" }, expansion!.Groups.Single().Value.ToArray());
    }

    [TestMethod]
    public void Compose_UnknownMemberWithoutADirectory_IsStillRefused()
    {
        var (expansion, failure) = AccountGroupExpansion.Compose(Groups("ops=dave"), Accounts);

        Assert.IsNull(expansion);
        Assert.AreEqual("(2) option --group: member dave names no account or group", failure);
    }

    [TestMethod]
    public void Compose_UnknownGroupWithADirectory_IsStillRefused()
    {
        var (_, failure) = AccountGroupExpansion.Compose(Groups("ops=@nothing"), Accounts, takesUnknownNamesAsDirectoryAccounts: true);

        Assert.AreEqual("(2) option --group: member @nothing names no account or group", failure);
    }

    [TestMethod]
    public void Expand_UnknownNameWithADirectory_ReachesItself()
    {
        var expansion = AccountGroupExpansion.Compose([], Accounts, takesUnknownNamesAsDirectoryAccounts: true).Expansion!;

        var failure = expansion.Expand("share-access", [new ConfiguredMember("dave", IsGroup: false), new ConfiguredMember("alice", IsGroup: false)], out var reached);

        Assert.IsNull(failure);
        CollectionAssert.AreEquivalent(new[] { "dave", "alice" }, reached.ToArray());
    }

    [TestMethod]
    public void WriteStartLines_Verbose_NotesEachDirectoryNameOnce()
    {
        var expansion = AccountGroupExpansion.Compose(Groups("ops=dave,alice", "dev=dave"), Accounts, takesUnknownNamesAsDirectoryAccounts: true).Expansion!;
        expansion.Expand("share-access", [new ConfiguredMember("dave", IsGroup: false), new ConfiguredMember("erin", IsGroup: false)], out _);
        using var log = new StringWriter();

        expansion.WriteStartLines(LogLevel.Verbose, log);

        Assert.AreEqual(
            $"* Group ops: 2 accounts{Environment.NewLine}* Group dev: 1 accounts{Environment.NewLine}"
            + $"Member dave is not a configured account; taken as a directory account{Environment.NewLine}"
            + $"Member erin is not a configured account; taken as a directory account{Environment.NewLine}",
            log.ToString());
    }

    private static ConfiguredGroup[] Groups(params string[] arguments)
    {
        var result = CommandLineParser.Parse([.. arguments.SelectMany(argument => new[] { "--group", argument }), "http://127.0.0.1:0"]);

        Assert.IsNotNull(result.CommandLine, result.Failure?.Message);
        return [.. result.CommandLine.Groups];
    }
}
