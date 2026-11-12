using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// <c>--mail-alias</c>, <c>--mail-catch-all</c>, <c>--shared-folder</c> and the <c>quota=</c> and <c>folders=</c>
/// keys of <c>--mailbox</c> read into <see cref="SurlCommandLine"/> (ADR-0101 decisions 4, 5 and 7, ADR-0102
/// decisions 1.3, 1.4 and 1.6, BL-995).
/// </summary>
[TestClass]
public sealed class CommandLineParserMailRoutingTests
{
    private const string Url = "imap://127.0.0.1:0/";
    private const string Domain = "example.com";

    [TestMethod]
    public void Parse_NoRoutingOption_HasNoAliasesCatchAllsOrSharedFolders()
    {
        var commandLine = Served(Url);

        Assert.IsEmpty(commandLine.MailAliases);
        Assert.IsEmpty(commandLine.MailCatchAlls);
        Assert.IsEmpty(commandLine.SharedFolders);
    }

    [TestMethod]
    public void Parse_MailAlias_ReadsItsAddressAndTargets()
    {
        var alias = Served("--mail-domain", Domain, "--mail-alias", "sales@example.com=ann@example.com,bob@example.com", Url).MailAliases.Single();

        Assert.AreEqual("sales@example.com", alias.Address);
        CollectionAssert.AreEqual(new[] { "ann@example.com", "bob@example.com" }, alias.Targets.ToArray());
    }

    [TestMethod]
    public void Parse_MailCatchAll_ReadsItsDomainAndAddress()
    {
        var catchAll = Served("--mail-domain", Domain, "--mail-catch-all", "example.com=postmaster@example.com", Url).MailCatchAlls.Single();

        Assert.AreEqual("example.com", catchAll.Domain);
        Assert.AreEqual("postmaster@example.com", catchAll.Address);
    }

    [TestMethod]
    public void Parse_SharedFolder_ReadsItsNameAndMembers()
    {
        var folder = Served("--shared-folder", "team=ann@example.com,@sales", Url).SharedFolders.Single();

        Assert.AreEqual("team", folder.Name);
        Assert.HasCount(2, folder.Members);
    }

    [TestMethod]
    public void Parse_MailboxQuotaAndFolders_AreReadIntoTheMailbox()
    {
        var mailbox = Served("--mail-domain", Domain, "--mailbox", "ann@example.com:s;quota=2k;folders=Sent,Work", Url).Mailboxes.Single();

        Assert.AreEqual(2048L, mailbox.QuotaBytes);
        CollectionAssert.AreEqual(new[] { "Sent", "Work" }, mailbox.Folders.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "--mail-alias", "a@example.com=b@example.com" }, "option --mail-alias: alias a@example.com needs --mail-domain", DisplayName = "Alias, no domain")]
    [DataRow(new[] { "--mail-domain", Domain, "--mail-alias", "a@example.org=b@example.com" }, "option --mail-alias: alias a@example.org is not in any --mail-domain", DisplayName = "Alias, another domain")]
    [DataRow(new[] { "--mail-domain", Domain, "--mail-alias", "a@example.com=b@example.com", "--mail-alias", "a@EXAMPLE.com.=c@example.com" }, "option --mail-alias: alias a@EXAMPLE.com. is given twice", DisplayName = "Alias twice")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "ann@example.com:s", "--mail-alias", "Ann@example.com=b@example.com" }, "option --mail-alias: alias Ann@example.com is also a mailbox", DisplayName = "Alias is a mailbox")]
    [DataRow(new[] { "--mail-catch-all", "example.com=a@example.com" }, "option --mail-catch-all: catch-all domain example.com needs --mail-domain", DisplayName = "Catch-all, no domain")]
    [DataRow(new[] { "--mail-domain", Domain, "--mail-catch-all", "example.org=a@example.com" }, "option --mail-catch-all: catch-all domain example.org is not in any --mail-domain", DisplayName = "Catch-all, another domain")]
    [DataRow(new[] { "--mail-domain", Domain, "--mail-catch-all", "example.com=a@example.com", "--mail-catch-all", "EXAMPLE.com.=b@example.com" }, "option --mail-catch-all: catch-all domain EXAMPLE.com. is given twice", DisplayName = "Catch-all twice")]
    [DataRow(new[] { "--shared-folder", "team=ann", "--shared-folder", "team=bob" }, "option --shared-folder: shared folder team is given twice", DisplayName = "Shared folder twice")]
    [DataRow(new[] { "--shared-folder", "INBOX=ann" }, "option --shared-folder: shared folder INBOX cannot be INBOX", DisplayName = "Shared folder named INBOX")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "ann@example.com:s;quota=0" }, "option --mailbox: key quota needs a value above 0", DisplayName = "Quota zero")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "ann@example.com:s;folders=Sent,Sent" }, "option --mailbox: member Sent is given twice", DisplayName = "Folder twice")]
    public void Parse_MailRoutingRefused_IsRefusedWithItsReason(string[] arguments, string message) =>
        AssertOptionRefused([.. arguments, Url], message);

    [TestMethod]
    public void Parse_MailAliasGivenMoreThanTenThousandTimes_IsRefused() =>
        AssertOptionRefused(
            [.. Enumerable.Range(0, 10001).SelectMany(index => new[] { "--mail-alias", $"a{index}@example.com=b@example.com" }), Url],
            "option --mail-alias: given more than 10000 times");

    [TestMethod]
    public void Parse_MailCatchAllGivenMoreThanTenThousandTimes_IsRefused() =>
        AssertOptionRefused(
            [.. Enumerable.Range(0, 10001).SelectMany(index => new[] { "--mail-catch-all", $"d{index}.example=a@example.com" }), Url],
            "option --mail-catch-all: given more than 10000 times");

    [TestMethod]
    public void Parse_SharedFolderGivenMoreThanTenThousandTimes_IsRefused() =>
        AssertOptionRefused(
            [.. Enumerable.Range(0, 10001).SelectMany(index => new[] { "--shared-folder", $"f{index}=ann" }), Url],
            "option --shared-folder: given more than 10000 times");

    [TestMethod]
    public void Parse_MailAliasInAConfigFile_IsRefusedNamingTheOptionAsWritten()
    {
        var files = new FakeConfigFileSource(("m.cfg", "mail-alias = \"a@example.com=b@example.com\"\n"));

        var result = CommandLineParser.Parse(["-K", "m.cfg", Url], [], files);

        Assert.AreEqual("option --mail-alias: alias a@example.com needs --mail-domain", result.Failure!.Message);
    }

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
