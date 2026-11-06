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
