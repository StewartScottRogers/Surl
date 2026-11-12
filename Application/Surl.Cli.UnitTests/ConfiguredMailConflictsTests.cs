namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredMailConflicts"/>: the duplicates and conflicts among <c>--mail-alias</c>,
/// <c>--mail-catch-all</c> and <c>--shared-folder</c> (ADR-0101 decision 5 rows 14 and 15).
/// </summary>
[TestClass]
public sealed class ConfiguredMailConflictsTests
{
    private static readonly string[] Domains = ["Example.COM."];

    private static readonly ConfiguredMailbox[] Mailboxes = [new("ann@example.com", "secret", null)];

    [TestMethod]
    public void Find_NothingGiven_FindsNone() =>
        Assert.IsNull(ConfiguredMailConflicts.Find([], [], [], [], []));

    [TestMethod]
    public void Find_DistinctItemsInTheDomains_FindsNone() =>
        Assert.IsNull(ConfiguredMailConflicts.Find(
            Domains,
            Mailboxes,
            [Alias("--mail-alias", "sales@example.com"), Alias("--mail-alias", "Sales@example.com")],
            [CatchAll("--mail-catch-all", "example.com")],
            [Folder("--shared-folder", "team"), Folder("--shared-folder", "Team")]));

    [TestMethod]
    public void Find_SharedFoldersWithNoMailDomain_FindsNone() =>
        Assert.IsNull(ConfiguredMailConflicts.Find([], [], [], [], [Folder("--shared-folder", "team")]));

    [TestMethod]
    public void Find_AliasTwice_ComparesTheDomainNormalisedAndNamesTheLaterAsWritten() =>
        Assert.AreEqual(
            new ConfiguredMailConflicts.Refusal("mail-alias", "alias sales@EXAMPLE.com is given twice"),
            ConfiguredMailConflicts.Find(Domains, [], [Alias("--mail-alias", "sales@example.com"), Alias("mail-alias", "sales@EXAMPLE.com")], [], []));

    [TestMethod]
    public void Find_CatchAllDomainTwice_IsRefused() =>
        Assert.AreEqual(
            new ConfiguredMailConflicts.Refusal("--mail-catch-all", "catch-all domain example.com. is given twice"),
            ConfiguredMailConflicts.Find(Domains, [], [], [CatchAll("--mail-catch-all", "Example.com"), CatchAll("--mail-catch-all", "example.com.")], []));

    [TestMethod]
    public void Find_SharedFolderTwice_IsRefused() =>
        Assert.AreEqual(
            new ConfiguredMailConflicts.Refusal("--shared-folder", "shared folder team is given twice"),
            ConfiguredMailConflicts.Find([], [], [], [], [Folder("--shared-folder", "team"), Folder("--shared-folder", "team")]));

    [TestMethod]
    public void Find_AliasWithNoMailDomain_NeedsOne() =>
        Assert.AreEqual(
            new ConfiguredMailConflicts.Refusal("--mail-alias", "alias sales@example.com needs --mail-domain"),
            ConfiguredMailConflicts.Find([], [], [Alias("--mail-alias", "sales@example.com")], [], []));

    [TestMethod]
    public void Find_AliasInAnotherDomain_IsNotInAnyMailDomain() =>
        Assert.AreEqual(
            new ConfiguredMailConflicts.Refusal("--mail-alias", "alias sales@example.org is not in any --mail-domain"),
            ConfiguredMailConflicts.Find(Domains, [], [Alias("--mail-alias", "sales@example.org")], [], []));

    [TestMethod]
    public void Find_AliasOfAMailboxInAnotherCase_IsAlsoAMailbox() =>
        Assert.AreEqual(
            new ConfiguredMailConflicts.Refusal("--mail-alias", "alias ANN@Example.com is also a mailbox"),
            ConfiguredMailConflicts.Find(Domains, Mailboxes, [Alias("--mail-alias", "ANN@Example.com")], [], []));

    [TestMethod]
    public void Find_CatchAllWithNoMailDomain_NeedsOne() =>
        Assert.AreEqual(
            new ConfiguredMailConflicts.Refusal("--mail-catch-all", "catch-all domain example.com needs --mail-domain"),
            ConfiguredMailConflicts.Find([], [], [], [CatchAll("--mail-catch-all", "example.com")], []));

    [TestMethod]
    public void Find_CatchAllForAnotherDomain_IsNotInAnyMailDomain() =>
        Assert.AreEqual(
            new ConfiguredMailConflicts.Refusal("--mail-catch-all", "catch-all domain example.org is not in any --mail-domain"),
            ConfiguredMailConflicts.Find(Domains, [], [], [CatchAll("--mail-catch-all", "example.org")], []));

    [TestMethod]
    public void Find_RepeatAndConflict_RefusesTheRepeatFirst() =>
        Assert.AreEqual(
            "shared folder t is given twice",
            ConfiguredMailConflicts.Find([], [], [Alias("--mail-alias", "a@example.com")], [], [Folder("--shared-folder", "t"), Folder("--shared-folder", "t")])?.Reason);

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void Find_NullList_Throws(int which) =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ConfiguredMailConflicts.Find(
            which == 0 ? null! : [],
            which == 1 ? null! : [],
            which == 2 ? null! : [],
            which == 3 ? null! : [],
            which == 4 ? null! : []));

    [TestMethod]
    public void NewMembers_AreEmptyByDefault()
    {
        var commandLine = new SurlCommandLine();

        Assert.IsEmpty(commandLine.MailAliases);
        Assert.IsEmpty(commandLine.MailCatchAlls);
        Assert.IsEmpty(commandLine.SharedFolders);
    }

    private static (string, ConfiguredMailAlias) Alias(string optionName, string address) =>
        (optionName, new ConfiguredMailAlias(address, ["ann@example.com"]));

    private static (string, ConfiguredMailCatchAll) CatchAll(string optionName, string domain) =>
        (optionName, new ConfiguredMailCatchAll(domain, "ann@example.com"));

    private static (string, ConfiguredSharedFolder) Folder(string optionName, string name) =>
        (optionName, new ConfiguredSharedFolder(name, [new("ann", false)]));
}
