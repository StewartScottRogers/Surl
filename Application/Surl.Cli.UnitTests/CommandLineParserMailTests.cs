using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// <c>--mail-domain</c>, <c>--mailbox</c> and <c>--mailbox-file</c> read into <see cref="SurlCommandLine"/>
/// (ADR-0101 decisions 3 to 6, ADR-0102 decisions 1.1 and 1.2, BL-653).
/// </summary>
[TestClass]
public sealed class CommandLineParserMailTests
{
    private const string Url = "imap://127.0.0.1:0/";
    private const string Domain = "example.com";

    [TestMethod]
    public void Parse_NoMailOption_HasNoDomainsMailboxesOrFiles()
    {
        var commandLine = Served(Url);

        Assert.IsEmpty(commandLine.MailDomains);
        Assert.IsEmpty(commandLine.Mailboxes);
        Assert.IsEmpty(commandLine.MailboxFiles);
    }

    [TestMethod]
    public void Parse_SeveralMailDomains_AreKeptAsWrittenInOrder() =>
        CollectionAssert.AreEqual(
            new[] { "Example.COM", "bücher.example." },
            Served("--mail-domain", "Example.COM", "--mail-domain=bücher.example.", Url).MailDomains.ToArray());

    [TestMethod]
    public void Parse_OneMailbox_ReadsItsAddressPasswordAndNoProtocols()
    {
        var mailbox = Served("--mail-domain", Domain, "--mailbox", "ann@example.com:pa:ss", Url).Mailboxes.Single();

        Assert.AreEqual("ann@example.com", mailbox.Address);
        Assert.AreEqual("pa:ss", mailbox.Password);
        Assert.IsNull(mailbox.Protocols);
    }

    [TestMethod]
    public void Parse_MailboxProtocols_AreLowerCasedInOrder() =>
        CollectionAssert.AreEqual(
            new[] { "imap", "pop3s" },
            Served("--mail-domain", Domain, "--mailbox", "ann@example.com:secret;PROTOCOLS=IMAP,Pop3s", Url).Mailboxes.Single().Protocols!.ToArray());

    [TestMethod]
    public void Parse_MailboxProtocols_AcceptAnExtendedServersScheme()
    {
        var result = CommandLineParser.Parse(["--mail-domain", Domain, "--mailbox", "ann@example.com:s;protocols=coap", Url], ["coap"]);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
    }

    [TestMethod]
    public void Parse_ManyMailboxes_EachAddAMailboxByItsNormalisedAddressInCommandLineOrder() =>
        CollectionAssert.AreEqual(
            new[] { "bob@example.com", "ann@example.com", "carol@example.org" },
            Served(
                "--mail-domain", Domain, "--mail-domain", "example.org",
                "--mailbox", "bob@example.com:a", "--mailbox", "ann@EXAMPLE.com:b", "--mailbox=carol@example.org:c", Url)
                .Mailboxes.Select(mailbox => mailbox.Address).ToArray());

    [TestMethod]
    public void Parse_MailboxInAUnicodeDomain_MatchesItsAsciiForm() =>
        Assert.HasCount(1, Served("--mail-domain", "xn--bcher-kva.example", "--mailbox", "ann@BÜCHER.example.:s", Url).Mailboxes);

    [TestMethod]
    public void Parse_MailboxFiles_AreKeptAsGivenInOrder() =>
        CollectionAssert.AreEqual(
            new[] { "a.csv", "b.csv" },
            Served("--mailbox-file", "a.csv", "--mailbox-file", "b.csv", Url).MailboxFiles.ToArray());

    [TestMethod]
    [DataRow(new[] { "--mail-domain", "a..b" }, "option --mail-domain: not a domain name", DisplayName = "Not a domain")]
    [DataRow(new[] { "--mail-domain", "" }, "option --mail-domain: blank argument where content is expected", DisplayName = "Blank domain")]
    [DataRow(new[] { "--mail-domain", Domain, "--mail-domain", "EXAMPLE.com." }, "option --mail-domain: mail domain EXAMPLE.com. is given twice", DisplayName = "A domain twice")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "ann@example.com:a", "--mailbox", "ANN@example.com:b" }, "option --mailbox: mailbox ANN@example.com is given twice", DisplayName = "A mailbox twice")]
    [DataRow(new[] { "--mailbox", "ann@example.com:a" }, "option --mailbox: mailbox ann@example.com needs --mail-domain", DisplayName = "No domain")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "ann@example.org:a" }, "option --mailbox: mailbox ann@example.org is not in any --mail-domain", DisplayName = "Another domain")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "sub@mail.example.com:a" }, "option --mailbox: mailbox sub@mail.example.com is not in any --mail-domain", DisplayName = "A subdomain")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "ann:a" }, "option --mailbox: not a mail address", DisplayName = "No @")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "@example.com:a" }, "option --mailbox: not a mail address", DisplayName = "Empty local part")]
    [DataRow(new[] { "--mail-domain", Domain, "-u", "ann@example.com:x", "--mailbox", "ann@EXAMPLE.com:a" }, "option --mailbox: mailbox ann@EXAMPLE.com is also a --user account", DisplayName = "A --user name")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "ann@example.com" }, "option --mailbox: expected <address>:<password>[;protocols=<scheme>,...][;quota=<bytes>][;folders=<name>,...]", DisplayName = "No password")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "ann@example.com:" }, "option --mailbox: an empty field", DisplayName = "Empty password")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "a%3Ab@example.com:s" }, "option --mailbox: the address holds a colon", DisplayName = "Colon in the address")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "ann@example.com:s;protocols=imap,gopherz" }, "option --mailbox: unknown scheme gopherz", DisplayName = "Unknown scheme")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "ann@example.com:s;protocols=coap" }, "option --mailbox: unknown scheme coap", DisplayName = "An extended scheme no registered server claims")]
    [DataRow(new[] { "--mail-domain", Domain, "--mailbox", "ann@example.com:s;protocols=" }, "option --mailbox: key protocols needs a value", DisplayName = "Empty protocols")]
    [DataRow(new[] { "--mailbox-file", "" }, "option --mailbox-file: blank argument where content is expected", DisplayName = "Blank file")]
    public void Parse_MailOptionRefused_IsRefusedWithItsReason(string[] arguments, string message) =>
        AssertOptionRefused([.. arguments, Url], message);

    [TestMethod]
    [DataRow("--mailbox", "ann@example.com:hunter2;bad")]
    [DataRow("--mailbox=ann@example.com:hunter2;bad", null)]
    public void Parse_MailboxRefused_NeverEchoesThePassword(string option, string? value)
    {
        string[] arguments = value is null ? ["--mail-domain", Domain, option, Url] : ["--mail-domain", Domain, option, value, Url];

        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual("option --mailbox: unknown flag bad", result.Failure!.Message);
        Assert.DoesNotContain("hunter2", result.Failure.Message);
    }

    [TestMethod]
    public void ToString_Mailbox_HoldsNoPassword() =>
        Assert.AreEqual("ConfiguredMailbox { Address = ann@example.com }", new ConfiguredMailbox("ann@example.com", "hunter2", null).ToString());

    [TestMethod]
    public void Parse_MailboxGivenTenThousandTimes_IsServed() =>
        Assert.HasCount(10000, Served(["--mail-domain", Domain, .. MailboxArguments(10000), Url]).Mailboxes);

    [TestMethod]
    public void Parse_MailboxGivenMoreThanTenThousandTimes_IsRefused() =>
        AssertOptionRefused(["--mail-domain", Domain, .. MailboxArguments(10001), Url], "option --mailbox: given more than 10000 times");

    [TestMethod]
    public void Parse_MailDomainGivenMoreThanTenThousandTimes_IsRefused() =>
        AssertOptionRefused(
            [.. Enumerable.Range(0, 10001).SelectMany(index => new[] { "--mail-domain", $"d{index}.example" }), Url],
            "option --mail-domain: given more than 10000 times");

    [TestMethod]
    public void Parse_MailboxConflict_IsCheckedAfterTheWholeLine() =>
        AssertOptionRefused(["--mailbox", "ann@example.com:a", "--nosuch", Url], "option --nosuch: is unknown");

    [TestMethod]
    public void Parse_MailboxInAConfigFile_IsRefusedAtItsLine()
    {
        var files = new FakeConfigFileSource(("m.cfg", "mail-domain = example.com\nmailbox = \"ann@example.com:s;protocols=nosuch\"\n"));

        var result = CommandLineParser.Parse(["-K", "m.cfg", Url], [], files);

        Assert.AreEqual("m.cfg:2: option --mailbox: unknown scheme nosuch", result.Failure!.Message);
    }

    private static IEnumerable<string> MailboxArguments(int count) =>
        Enumerable.Range(0, count).SelectMany(index => new[] { "--mailbox", $"m{index}@example.com:s" });

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
