using System.Text;
using Surl.Authentication;
using Surl.Cli;
using Surl.Content;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// <see cref="MailboxComposition"/>, and the store and policies composed from it
/// (ADR-0102 decisions 1.1, 1.2, 1.4, 4.3 and 5; ADR-0101 decision 5; BL-882). The options
/// themselves are BL-883's, so each command line here is parsed and then given its mail domains,
/// mailboxes and mailbox files as the parser will.
/// </summary>
[TestClass]
public sealed class MailboxCompositionTests
{
    private const string Url = "imap://127.0.0.1:0/";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Compose_NoMailDomain_HasNoConfigurationAndNoAccounts()
    {
        var (composition, exitCode, failure) = Compose(StartTestSeams.Parse("-u", "ann:secret", Url));

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsNull(failure);
        Assert.IsNull(composition!.MailDomains);
        Assert.IsEmpty(composition.Accounts);
    }

    [TestMethod]
    public void Compose_MailboxesAndTwoFiles_AreAllAccountsNamedByTheirAddressInOrder()
    {
        var commandLine = WithMail(
            StartTestSeams.Parse(Url),
            ["Example.COM"],
            [new ConfiguredMailbox("ann@example.com", "secret", null)],
            ["one.csv", "two.csv"]);

        var (composition, _, _) = Compose(
            commandLine,
            ("one.csv", "address,password,protocols\nbob@example.com,pw,\"imap,POP3\"\n"),
            ("two.csv", "address,password\ncat@example.com,pw\n"));

        string[] addresses = ["ann@example.com", "bob@example.com", "cat@example.com"];
        CollectionAssert.AreEqual(addresses, composition!.Accounts.Select(account => account.UserName).ToArray());
        Assert.AreEqual("secret", composition.Accounts[0].Password);
        Assert.AreSame(Account.MailSchemes, composition.Accounts[0].Schemes);
        Assert.IsTrue(composition.Accounts[1].MayLogInOn("pop3s"));
        Assert.IsFalse(composition.Accounts[1].MayLogInOn("smtp"));
        CollectionAssert.AreEqual(addresses, composition.MailDomains!.MailboxAddresses.ToArray());
        Assert.Contains("example.com", composition.MailDomains.Domains);
    }

    [TestMethod]
    public void Compose_MailboxFileThatCannotBeRead_IsCouldNotReadFile()
    {
        var (composition, exitCode, failure) = Compose(WithMail(StartTestSeams.Parse(Url), ["example.com"], [], ["missing.csv"]));

        Assert.IsNull(composition);
        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual("(37) Could not read mailbox file missing.csv", failure);
    }

    [TestMethod]
    public void Compose_MalformedMailboxFile_IsFailedInitNamingTheFileAndLine()
    {
        var (composition, exitCode, failure) = Compose(
            WithMail(StartTestSeams.Parse(Url), ["example.com"], [], ["m.csv"]), ("m.csv", "address,password\nann@example.com,secret,x\n"));

        Assert.IsNull(composition);
        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        StringAssert.StartsWith(failure, "(2) Mailbox file m.csv, line 2: ");
        Assert.DoesNotContain("secret", failure!);
    }

    [TestMethod]
    public void Compose_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => MailboxComposition.Compose(null!, _ => [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => MailboxComposition.Compose(new SurlCommandLine(), null!, []));
    }

    [TestMethod]
    public async Task LoadServiceStateAsync_AConfiguredMailbox_IsAnOwnerWithItsInboxAndOtherDomainsAreRelaying()
    {
        var commandLine = WithMail(StartTestSeams.Parse(Url), ["example.com"], [new ConfiguredMailbox("ann@example.com", "secret", null)]);
        var (composition, _, _) = Compose(commandLine);
        var (policy, accountNames, _, _, _) = AuthenticationComposition.Compose(
            commandLine, _ => [], TimeProvider.System, mailboxAccounts: composition!.Accounts);

        var state = await LoadServiceStateAsync(commandLine, policy!, accountNames, composition.MailDomains);

        CollectionAssert.AreEqual(new[] { "INBOX" }, state.MailStore.ListMailboxes(state.MailStore.ViewFor("ann@example.com")).ToArray());
        Assert.AreEqual(MailRecipientLookup.RelayingDenied, state.MailStore.LookUpRecipient("<bob@example.org>", out _));
        Assert.AreEqual(MailRecipientLookup.Deliverable, state.MailStore.LookUpRecipient("<ann@example.com>", out _));
    }

    [TestMethod]
    public async Task LoadServiceStateAsync_NoMailDomain_ComposesTheStoreAndAccountsAsBefore()
    {
        var commandLine = StartTestSeams.Parse("-u", "ann:secret", Url);
        var (composition, _, _) = Compose(commandLine);
        var (policy, accountNames, _, _, _) = AuthenticationComposition.Compose(
            commandLine, _ => [], TimeProvider.System, mailboxAccounts: composition!.Accounts);

        var state = await LoadServiceStateAsync(commandLine, policy!, accountNames, composition.MailDomains);

        CollectionAssert.AreEqual(new[] { "ann" }, accountNames.ToArray());
        Assert.AreEqual(MailRecipientLookup.Deliverable, state.MailStore.LookUpRecipient("<ann@anywhere.example>", out _));
    }

    [TestMethod]
    public async Task For_AMailboxRestrictedToImapAndPop3_LogsInOverImapAndIsRefusedOverSmtp()
    {
        var commandLine = WithMail(
            StartTestSeams.Parse("--allow-plaintext-auth", Url), ["example.com"], [new ConfiguredMailbox("ann@example.com", "secret", ["imap", "pop3"])]);
        var policies = ComposeSchemePolicies(commandLine);

        var overImap = await policies.For("imap").CheckPasswordLoginAsync(Login("imap"), TestContext.CancellationToken);
        var overSmtp = await policies.For("smtp").CheckPasswordLoginAsync(Login("smtp"), TestContext.CancellationToken);

        Assert.AreEqual(PasswordLoginVerdict.Accepted, overImap);
        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, overSmtp);
    }

    [TestMethod]
    public async Task For_AMailboxWithNoProtocols_LogsInOnTheMailSchemesOnly()
    {
        var commandLine = WithMail(
            StartTestSeams.Parse("--allow-plaintext-auth", Url), ["example.com"], [new ConfiguredMailbox("ann@example.com", "secret", null)]);
        var policies = ComposeSchemePolicies(commandLine);

        var overSmtp = await policies.For("smtp").CheckPasswordLoginAsync(Login("smtp"), TestContext.CancellationToken);
        var overFtp = await policies.For("ftp").CheckPasswordLoginAsync(Login("ftp"), TestContext.CancellationToken);

        Assert.AreEqual(PasswordLoginVerdict.Accepted, overSmtp);
        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, overFtp);
    }

    [TestMethod]
    public void AuthenticationCompose_AMailboxNamedLikeAUserFileAccount_IsRefusedWithFailedInit()
    {
        var commandLine = WithMail(
            StartTestSeams.Parse("--user-file", "users.txt", Url), ["example.com"], [new ConfiguredMailbox("ann@example.com", "secret", null)]);
        var (composition, _, _) = Compose(commandLine);

        var (policy, _, _, exitCode, failure) = AuthenticationComposition.Compose(
            commandLine, _ => "ann@example.com:other\n"u8.ToArray(), TimeProvider.System, mailboxAccounts: composition!.Accounts);

        Assert.IsNull(policy);
        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual("(2) option --mailbox: mailbox ann@example.com is also a --user-file account", failure);
    }

    [TestMethod]
    public void AuthenticationCompose_AUserFileThatCannotBeRead_IsStillRefusedFirst()
    {
        var (policy, _, _, exitCode, _) = AuthenticationComposition.Compose(
            StartTestSeams.Parse("--user-file", "users.txt", Url), _ => throw new FileNotFoundException(), TimeProvider.System, mailboxAccounts: []);

        Assert.IsNull(policy);
        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
    }

    [TestMethod]
    public void WriteStartLines_Verbose_NotesTheDomainsAndEachMailboxsSchemes()
    {
        var commandLine = WithMail(
            StartTestSeams.Parse("-v", Url),
            ["Example.com", "example.org."],
            [new ConfiguredMailbox("ann@example.com", "a", null), new ConfiguredMailbox("bob@example.org", "b", ["imap", "ftp"])]);
        using var log = new StringWriter();

        Compose(commandLine).Composition!.WriteStartLines(commandLine, log);

        Assert.AreEqual(
            Lines("* Mail domains: example.com, example.org", "* Mailbox ann@example.com: logins on smtp, imap, pop3", "* Mailbox bob@example.org: logins on imap, ftp"),
            log.ToString());
    }

    [TestMethod]
    public void WriteStartLines_AllowAnonymous_NotesTheDomainsAreNotApplied()
    {
        var commandLine = WithMail(StartTestSeams.Parse("-v", "--allow-anonymous", Url), ["example.com"], []);
        using var log = new StringWriter();

        Compose(commandLine).Composition!.WriteStartLines(commandLine, log);

        Assert.AreEqual(
            Lines("* Mail domains: example.com", "* Mail domains not applied: --allow-anonymous delivers every recipient to one mailbox set"),
            log.ToString());
    }

    [TestMethod]
    public void WriteStartLines_BelowVerbose_WritesNothing()
    {
        var commandLine = WithMail(StartTestSeams.Parse(Url), ["example.com"], [new ConfiguredMailbox("ann@example.com", "a", null)]);
        using var log = new StringWriter();

        Compose(commandLine).Composition!.WriteStartLines(commandLine, log);

        Assert.IsEmpty(log.ToString());
    }

    [TestMethod]
    public void WriteStartLines_NoMailDomain_WritesNothing()
    {
        var commandLine = StartTestSeams.Parse("-v", Url);
        using var log = new StringWriter();

        Compose(commandLine).Composition!.WriteStartLines(commandLine, log);

        Assert.IsEmpty(log.ToString());
    }

    [TestMethod]
    public void WriteStartLines_NullArgument_Throws()
    {
        var composition = Compose(StartTestSeams.Parse(Url)).Composition!;

        Assert.ThrowsExactly<ArgumentNullException>(() => composition.WriteStartLines(null!, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => composition.WriteStartLines(new SurlCommandLine(), null!));
    }

    [TestMethod]
    public async Task LoadServiceStateAsync_AliasCatchAllQuotaAndFolders_ReachTheComposedStore()
    {
        var commandLine = WithMail(
            StartTestSeams.Parse(Url),
            ["example.com"],
            [new ConfiguredMailbox("ann@example.com", "secret", null, 5000, ["Archive", "INBOX"]), new ConfiguredMailbox("bob@example.com", "pw", null)]) with
        {
            MailAliases = [new ConfiguredMailAlias("team@example.com", ["ann@example.com", "bob@example.com"])],
            MailCatchAlls = [new ConfiguredMailCatchAll("example.com", "bob@example.com")],
        };
        var (composition, _, _) = Compose(commandLine);
        var (policy, accountNames, _, _, _) = AuthenticationComposition.Compose(
            commandLine, _ => [], TimeProvider.System, mailboxAccounts: composition!.Accounts);

        var state = await LoadServiceStateAsync(commandLine, policy!, accountNames, composition.MailDomains);

        Assert.AreEqual(MailRecipientLookup.Deliverable, state.MailStore.LookUpRecipient("<team@example.com>", out var team));
        Assert.HasCount(2, team!.OwnerNames);
        Assert.AreEqual(MailRecipientLookup.Deliverable, state.MailStore.LookUpRecipient("<nobody@example.com>", out var caught));
        CollectionAssert.AreEqual(new[] { "bob@example.com" }, caught!.OwnerNames.ToArray());
        CollectionAssert.AreEqual(
            new[] { "Archive", "INBOX" }, state.MailStore.ListMailboxes(state.MailStore.ViewFor("ann@example.com")).ToArray());
        Assert.AreEqual(5000, composition.MailDomains!.MailboxSettings["ann@example.com"].QuotaBytes);
    }

    [TestMethod]
    public void Compose_AnAliasOutsideEveryMailDomain_IsFailedInitWithoutTheParameterName()
    {
        var commandLine = WithMail(StartTestSeams.Parse(Url), ["example.com"], []) with
        {
            MailAliases = [new ConfiguredMailAlias("team@example.org", ["ann@example.com"])],
        };

        var (composition, exitCode, failure) = Compose(commandLine);

        Assert.IsNull(composition);
        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual("(2) mail configuration: The mail address team@example.org is not in any mail domain.", failure);
    }

    [TestMethod]
    public void ComposeSharedFolders_AGroupMember_IsExpandedToItsAccountsAndReachesTheStore()
    {
        var commandLine = SharedFolderCommandLine(new ConfiguredSharedFolder("team", [new ConfiguredMember("staff", true), new ConfiguredMember("cat", false)]));
        var (composition, _, _) = Compose(commandLine);
        var groups = Groups(["ann@example.com", "bob@example.com", "cat"], new ConfiguredGroup("staff", [new ConfiguredMember("ann@example.com", false), new ConfiguredMember("bob@example.com", false)]));

        var failure = composition!.ComposeSharedFolders(commandLine, groups);

        Assert.IsNull(failure);
        var folder = composition.MailDomains!.SharedFolders.Single();
        Assert.AreEqual("team", folder.Name);
        CollectionAssert.AreEquivalent(new[] { "ann@example.com", "bob@example.com", "cat" }, folder.Members.ToArray());
        var store = new MailboxStore(["cat"], false, TimeProvider.System, 1000, mailDomains: composition.MailDomains);
        CollectionAssert.AreEqual(new[] { "Shared/team" }, store.ViewFor("cat")!.SharedFolders.ToArray());
    }

    [TestMethod]
    public void ComposeSharedFolders_NoSharedFolder_LeavesTheConfigurationAsIs()
    {
        var commandLine = WithMail(StartTestSeams.Parse(Url), ["example.com"], []);
        var composition = Compose(commandLine).Composition!;
        var before = composition.MailDomains;

        var failure = composition.ComposeSharedFolders(commandLine, Groups([]));

        Assert.IsNull(failure);
        Assert.AreSame(before, composition.MailDomains);
    }

    [TestMethod]
    public void ComposeSharedFolders_OnlySharedFolders_ComposeAConfigurationWithoutDomains()
    {
        var commandLine = StartTestSeams.Parse(Url) with { SharedFolders = [new ConfiguredSharedFolder("team", [new ConfiguredMember("ann", false)])] };
        var composition = Compose(commandLine).Composition!;

        var failure = composition.ComposeSharedFolders(commandLine, Groups(["ann"]));

        Assert.IsNull(failure);
        Assert.IsEmpty(composition.MailDomains!.Domains);
        Assert.HasCount(1, composition.MailDomains.SharedFolders);
    }

    [TestMethod]
    public void ComposeSharedFolders_EveryoneAsAMember_IsRefused()
    {
        var commandLine = SharedFolderCommandLine(new ConfiguredSharedFolder("team", [new ConfiguredMember("everyone", true)]));
        var composition = Compose(commandLine).Composition!;

        var failure = composition.ComposeSharedFolders(commandLine, Groups(["ann@example.com"]));

        Assert.AreEqual("(2) option --shared-folder: member @everyone is not allowed here", failure);
    }

    [TestMethod]
    public void ComposeSharedFolders_AMemberNamingNothing_IsRefused()
    {
        var commandLine = SharedFolderCommandLine(new ConfiguredSharedFolder("team", [new ConfiguredMember("ghost", false)]));
        var composition = Compose(commandLine).Composition!;

        var failure = composition.ComposeSharedFolders(commandLine, Groups(["ann@example.com"]));

        Assert.AreEqual("(2) option --shared-folder: member ghost names no account or group", failure);
    }

    [TestMethod]
    public void ComposeSharedFolders_ANameTheStoreRefuses_IsRefusedWithoutTheParameterName()
    {
        var commandLine = SharedFolderCommandLine(new ConfiguredSharedFolder("inbox", [new ConfiguredMember("ann@example.com", false)]));
        var composition = Compose(commandLine).Composition!;

        var failure = composition.ComposeSharedFolders(commandLine, Groups(["ann@example.com"]));

        Assert.AreEqual("(2) option --shared-folder: INBOX cannot be a shared folder.", failure);
    }

    [TestMethod]
    public void ComposeSharedFolders_NullArgument_Throws()
    {
        var composition = Compose(StartTestSeams.Parse(Url)).Composition!;

        Assert.ThrowsExactly<ArgumentNullException>(() => composition.ComposeSharedFolders(null!, Groups([])));
        Assert.ThrowsExactly<ArgumentNullException>(() => composition.ComposeSharedFolders(new SurlCommandLine(), null!));
    }

    [TestMethod]
    [DataRow(MailRouteKind.Alias, MailRouteProblem.TargetOutsideMailDomains, "(2) option --mail-alias: target x@y.example is not in any --mail-domain")]
    [DataRow(MailRouteKind.Alias, MailRouteProblem.TargetNamesNoMailbox, "(2) option --mail-alias: target x@y.example names no mailbox")]
    [DataRow(MailRouteKind.Alias, MailRouteProblem.IncludesItself, "(2) option --mail-alias: alias x@y.example includes itself")]
    [DataRow(MailRouteKind.CatchAll, MailRouteProblem.TargetOutsideMailDomains, "(2) option --mail-catch-all: target x@y.example is not in any --mail-domain")]
    [DataRow(MailRouteKind.CatchAll, MailRouteProblem.TargetNamesNoMailbox, "(2) option --mail-catch-all: target x@y.example names no mailbox")]
    [DataRow(MailRouteKind.CatchAll, MailRouteProblem.IncludesItself, "(2) option --mail-catch-all: catch-all x@y.example includes itself")]
    public void DescribeRouteFailure_EachProblem_HasTheAdrsText(MailRouteKind kind, MailRouteProblem problem, string expected)
    {
        Assert.AreEqual(expected, MailboxComposition.DescribeRouteFailure(new MailRouteException(kind, problem, "x@y.example")));
    }

    [TestMethod]
    public void DescribeRouteFailure_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => MailboxComposition.DescribeRouteFailure(null!));
    }

    [TestMethod]
    public async Task LoadMailStoreAsync_AnAliasWhoseTargetNamesNoMailbox_IsAMessageForExit2()
    {
        var commandLine = WithMail(StartTestSeams.Parse(Url), ["example.com"], [new ConfiguredMailbox("ann@example.com", "a", null)]) with
        {
            MailAliases = [new ConfiguredMailAlias("team@example.com", ["ann@example.com", "x@example.com"])],
        };
        var composition = Compose(commandLine).Composition!;

        var (store, failure) = await SurlServerComposition.LoadMailStoreAsync(
            null, commandLine, ["ann@example.com"], TimeProvider.System, DiscardingSurlEventPublisher.Instance, EmptyFaultPlan.Instance, TestContext.CancellationToken, composition.MailDomains);

        Assert.IsNull(store);
        Assert.AreEqual("(2) option --mail-alias: target x@example.com names no mailbox", failure);
    }

    [TestMethod]
    public async Task LoadMailStoreAsync_ARouteTheStoreRefusesOnLoad_IsAMessageForExit2()
    {
        var commandLine = WithMail(StartTestSeams.Parse(Url), ["example.com"], [new ConfiguredMailbox("ann@example.com", "a", null)]) with
        {
            MailCatchAlls = [new ConfiguredMailCatchAll("example.com", "x@elsewhere.example")],
        };
        var composition = Compose(commandLine).Composition!;
        var files = new MailStoreFiles(new InMemoryContentFileSystem(TimeProvider.System), "mail");

        var (store, failure) = await SurlServerComposition.LoadMailStoreAsync(
            files, commandLine, ["ann@example.com"], TimeProvider.System, DiscardingSurlEventPublisher.Instance, EmptyFaultPlan.Instance, TestContext.CancellationToken, composition.MailDomains);

        Assert.IsNull(store);
        Assert.AreEqual("(2) option --mail-catch-all: target x@elsewhere.example is not in any --mail-domain", failure);
    }

    [TestMethod]
    public async Task ComposeAsync_AnAliasWhoseTargetNamesNoMailbox_ExitsWith2AndTheAdrsText()
    {
        var commandLine = WithMail(StartTestSeams.Parse(Url), ["example.com"], [new ConfiguredMailbox("ann@example.com", "a", null)]) with
        {
            MailAliases = [new ConfiguredMailAlias("team@example.com", ["x@example.com"])],
        };

        var start = await SurlServerComposition.ComposeAsync(commandLine, new StartTestSeams().Seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(2) option --mail-alias: target x@example.com names no mailbox" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task ComposeAsync_ASharedFolderNamingEveryone_ExitsWith2()
    {
        var commandLine = WithMail(StartTestSeams.Parse(Url), ["example.com"], []) with
        {
            SharedFolders = [new ConfiguredSharedFolder("team", [new ConfiguredMember("everyone", true)])],
        };

        var start = await SurlServerComposition.ComposeAsync(commandLine, new StartTestSeams().Seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(2) option --shared-folder: member @everyone is not allowed here" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ACatchAll_WarnsAtStart()
    {
        var commandLine = WithMail(StartTestSeams.Parse("-v", Url), ["example.com"], [new ConfiguredMailbox("ann@example.com", "a", null)]) with
        {
            MailCatchAlls = [new ConfiguredMailCatchAll("Example.com", "ann@example.com")],
        };
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = await SurlServerComposition.ComposeAsync(commandLine, new StartTestSeams().Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, _ => bound.SetResult()), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        StringAssert.Contains(log.ToString(), "surl: warning: --mail-catch-all: example.com keeps mail to every unknown address");
    }

    [TestMethod]
    public void WriteWarnings_ACatchAllBelowInfo_WritesNothing()
    {
        var commandLine = StartTestSeams.Parse("-s", Url) with { MailCatchAlls = [new ConfiguredMailCatchAll("example.com", "ann@example.com")] };
        using var log = new StringWriter();

        MailboxComposition.WriteWarnings(commandLine, log);

        Assert.IsEmpty(log.ToString());
    }

    [TestMethod]
    public void WriteWarnings_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => MailboxComposition.WriteWarnings(null!, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => MailboxComposition.WriteWarnings(new SurlCommandLine(), null!));
    }

    [TestMethod]
    public void WriteStartLines_QuotaFoldersAndSharedFolders_AreNoted()
    {
        var commandLine = WithMail(
            StartTestSeams.Parse("-v", Url), ["example.com"], [new ConfiguredMailbox("ann@example.com", "a", null, 2048, ["One", "Two"])]) with
        {
            SharedFolders = [new ConfiguredSharedFolder("team", [new ConfiguredMember("ann@example.com", false)])],
        };
        var composition = Compose(commandLine).Composition!;
        composition.ComposeSharedFolders(commandLine, Groups(["ann@example.com"]));
        using var log = new StringWriter();

        composition.WriteStartLines(commandLine, log);

        Assert.AreEqual(
            Lines(
                "* Mail domains: example.com",
                "* Mailbox ann@example.com: logins on smtp, imap, pop3, quota 2048 bytes, 2 folders",
                "* Shared folder Shared/team: 1 members"),
            log.ToString());
    }

    [TestMethod]
    public void WriteStartLines_OnlySharedFolders_NotesNoDomains()
    {
        var commandLine = StartTestSeams.Parse("-v", Url) with { SharedFolders = [new ConfiguredSharedFolder("team", [new ConfiguredMember("ann", false)])] };
        var composition = Compose(commandLine).Composition!;
        composition.ComposeSharedFolders(commandLine, Groups(["ann"]));
        using var log = new StringWriter();

        composition.WriteStartLines(commandLine, log);

        Assert.AreEqual(Lines("* Shared folder Shared/team: 1 members"), log.ToString());
    }

    private static SurlCommandLine SharedFolderCommandLine(ConfiguredSharedFolder folder) =>
        WithMail(
            StartTestSeams.Parse(Url),
            ["example.com"],
            [new ConfiguredMailbox("ann@example.com", "a", null), new ConfiguredMailbox("bob@example.com", "b", null)]) with
        {
            SharedFolders = [folder],
        };

    private static AccountGroupExpansion Groups(string[] accountNames, params ConfiguredGroup[] groups) =>
        AccountGroupExpansion.Compose(groups, accountNames).Expansion!;

    /// <summary>
    /// <paramref name="commandLine"/> with the mail domains, mailboxes and mailbox files BL-883's
    /// options will set.
    /// </summary>
    internal static SurlCommandLine WithMail(
        SurlCommandLine commandLine, string[] mailDomains, ConfiguredMailbox[] mailboxes, string[]? mailboxFiles = null) =>
        commandLine with { MailDomains = mailDomains, Mailboxes = mailboxes, MailboxFiles = mailboxFiles ?? [] };

    private static PasswordLogin Login(string scheme) =>
        new(scheme, "ann@example.com", Encoding.UTF8.GetBytes("secret"), null);

    private static string Lines(params string[] lines) => string.Concat(lines.Select(line => line + Environment.NewLine));

    private static SchemeAuthenticationPolicies ComposeSchemePolicies(SurlCommandLine commandLine)
    {
        var (composition, _, _) = Compose(commandLine);
        var (policy, _, _, _, _) = AuthenticationComposition.Compose(
            commandLine, _ => [], TimeProvider.System, mailboxAccounts: composition!.Accounts);
        return new SchemeAuthenticationPolicies(policy!, TimeProvider.System);
    }

    private static (MailboxComposition? Composition, SurlExitCode ExitCode, string? FailureMessage) Compose(
        SurlCommandLine commandLine, params (string Path, string Text)[] files) =>
        MailboxComposition.Compose(
            commandLine,
            path => files.FirstOrDefault(file => file.Path == path).Text is { } text ? Encoding.UTF8.GetBytes(text) : throw new FileNotFoundException(path),
            []);

    private async Task<ServiceState> LoadServiceStateAsync(
        SurlCommandLine commandLine, AuthenticationPolicy policy, IReadOnlyList<string> accountNames, MailDomainConfiguration? mailDomains)
    {
        var (state, failure) = await SurlServerComposition.LoadServiceStateAsync(
            commandLine,
            new InMemoryContentFileSystem(TimeProvider.System),
            policy,
            accountNames,
            isTlsUpgradeAvailable: false,
            TimeProvider.System,
            DiscardingSurlEventPublisher.Instance,
            EmptyFaultPlan.Instance,
            TestContext.CancellationToken,
            mailDomains);
        Assert.IsNull(failure);
        return state!;
    }
}
