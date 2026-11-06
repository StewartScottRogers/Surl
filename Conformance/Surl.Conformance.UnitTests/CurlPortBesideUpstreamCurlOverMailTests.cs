using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for the mail family (ADR-0089, BL-583): every conversation of
/// <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests"/>, <see cref="UpstreamCurlReadsMailFromSurlOverImapTests"/>
/// and <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests"/> runs through the pinned upstream build and through
/// the pinned Curl port, each against its own fresh <c>surl</c> over its own fresh <c>--directory</c>, and the
/// two runs are judged by <see cref="CurlPortComparison"/>. One test per mirrored test, each naming it.
/// </summary>
/// <remarks>
/// <para>
/// The mail conversations need more than <see cref="PinnedCurlPortBesideUpstreamCurl"/> gives one command line:
/// <c>smtp</c> beside <c>imap</c> or <c>pop3</c> in one <c>surl</c>, an inbox seeded over <c>smtp</c> first (by
/// upstream curl in both runs: the seed is the case's content, not what is measured), several client runs in a
/// row, a connection held open by the test, and the mail store read back after <c>surl</c> stops. This class
/// runs those itself and hands each client run to <see cref="CurlPortComparison.Compare"/>.
/// </para>
/// <para>
/// Normalisation this family adds to ADR-0089 decision 4's table, each for a value <c>surl</c> or the clock
/// chooses afresh per run and neither client decides: the date in the <c>Received:</c> trace field
/// (output and stored mail), the <c>UIDVALIDITY</c> value (output), and in the request bytes the
/// <c>CRAM-MD5</c> digest and the <c>APOP</c> digest, both computed from <c>surl</c>'s random challenge.
/// Request bytes are not recorded for a conversation that upgrades with <c>STARTTLS</c> or <c>STLS</c>
/// (<see cref="CurlPortMeasurementCase.RecordsClientBytes"/>); its plaintext twin compares curl's own bytes.
/// </para>
/// <para>
/// A case passes on agreement and on a port defect <c>Documentation/Conformance/CurlPort/Mail.md</c> lists, and
/// fails on a Surl defect, an unlisted port defect or a listed one that no longer appears (ADR-0089 decision 5).
/// Inconclusive where either pinned binary is absent.
/// </para>
/// </remarks>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverMailTests
{
    private const int WeirdServerReply = 8;
    private const int QuoteError = 21;
    private const int UploadFailed = 25;
    private const int SendError = 55;
    private const int UseSslFailed = 64;
    private const int LoginDenied = 67;
    private const int RemoteFileNotFound = 78;

    private const string Recipient = "tester@example.com";
    private const string MailPlaceholder = "{mail}";
    private const string AccountsPlaceholder = "{accounts}";
    private const string SurlBase = CurlPortMeasurementCase.SurlBaseUrlPlaceholder;
    private const string FindingsFile = "Documentation/Conformance/CurlPort/Mail.md";

    private const string SmtpClass = nameof(UpstreamCurlSendsMailToSurlOverSmtpTests);
    private const string ImapClass = nameof(UpstreamCurlReadsMailFromSurlOverImapTests);
    private const string Pop3Class = nameof(UpstreamCurlReadsMailFromSurlOverPop3Tests);

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    // ADR-0053's mail.txt, the SMTP class's message.
    private static readonly byte[] SmtpMail = "From: a@x\r\nTo: b@y\r\nSubject: hi\r\n\r\nhello\r\n.dot line\r\n"u8.ToArray();

    private static readonly byte[] SmtpMailWithBareLineFeeds = "From: a@x\nTo: b@y\nSubject: hi\n\nhello\n.dot line\n"u8.ToArray();

    // ADR-0055's mail.txt, the IMAP and POP3 classes' message.
    private static readonly byte[] ReadMail = "From: a@x\r\nSubject: hi\r\n\r\nhello\r\n"u8.ToArray();

    private static readonly string[] Accounts = ["--user-file", AccountsPlaceholder];

    private static readonly string[] ExtensionSwitches = ["CURL_EXTENSIONS", "SURL_EXTENSIONS"];

    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan RelayDrainTimeout = TimeSpan.FromSeconds(10);

    private static readonly Regex TraceDate = new(
        @"; [A-Z][a-z]{2}, \d{2} [A-Z][a-z]{2} \d{4} \d{2}:\d{2}:\d{2} \+0000\r\n", RegexOptions.CultureInvariant);

    private static readonly Regex UidValidityValue = new(@"(UIDVALIDITY )\d+", RegexOptions.CultureInvariant);

    private static readonly Regex ApopDigest = new(@"(APOP \S+ )[0-9a-f]{32}", RegexOptions.CultureInvariant);

    private static readonly Regex Base64Token = new("(?<![A-Za-z0-9+/=])[A-Za-z0-9+/]{8,}={0,2}(?![A-Za-z0-9+/=])", RegexOptions.CultureInvariant);

    private static readonly Regex DigestServerNonce = new("((?<![A-Za-z])nonce=\"?)[^\",]*", RegexOptions.CultureInvariant);

    private static readonly Regex Gs2Port = new("(\u0001port=)[0-9]+", RegexOptions.CultureInvariant);

    private static readonly Regex CramMd5Response = new(@"\A(\S+) [0-9a-f]{32}\z", RegexOptions.CultureInvariant);

    private readonly List<string> temporaryDirectories = [];

    private enum MailSeed
    {
        None,
        OverSmtpAsTester,
        OverSmtpAnonymously,
        BeforeStartAsTester,
    }

    private enum InboxOwner
    {
        None,
        Tester,
        Anonymous,
    }

    private enum MailHold
    {
        None,
        Greeting,
        Pop3Login,
    }

    public TestContext TestContext { get; set; } = null!;

    [TestCleanup]
    public void DeleteTemporaryDirectories()
    {
        foreach (var directory in temporaryDirectories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ----- SMTP -----

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_AllowAnonymous_StoresTheMessageInTheAnonymousInbox"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_AllowAnonymous_StoresTheMessageInTheAnonymousInbox() =>
        MeasureSmtpAsync(
            ["--allow-anonymous"], Send([]), InboxOwner.Anonymous,
            outcome =>
            {
                AssertExit(outcome, 0);
                Assert.IsEmpty(outcome.Last.StandardOutput);
                AssertStoredCopies(outcome, 1, "ESMTP", SmtpMail);
            });

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_AccountButNoLogin_ExitsSendErrorAndStoresNothing"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_AccountButNoLogin_ExitsSendErrorAndStoresNothing() =>
        MeasureSmtpAsync(
            Accounts, Send([]), InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, SendError);
                StringAssert.Contains(outcome.Last.StandardError, "MAIL failed: 530");
                Assert.IsEmpty(outcome.Inbox);
            },
            "MAIL failed: 530");

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_AccountLoggedIn_LogsInWithCramMd5AndStoresTheMessageInTheAccountsInbox"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_AccountLoggedIn_LogsInWithCramMd5AndStoresTheMessageInTheAccountsInbox() =>
        MeasureSmtpAsync(
            Accounts, Send(["-v", "-u", Credentials]), InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, 0);
                StringAssert.Contains(outcome.Last.StandardError, "AUTH CRAM-MD5");
                AssertStoredCopies(outcome, 1, "ESMTPA", SmtpMail);
            },
            "AUTH CRAM-MD5");

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_WrongPassword_ExitsLoginDeniedAndStoresNothing"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_WrongPassword_ExitsLoginDeniedAndStoresNothing() =>
        MeasureSmtpAsync(
            Accounts, Send(["-u", $"{AccountsFile.User}:wrong"]), InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, LoginDenied);
                Assert.IsEmpty(outcome.Inbox);
            });

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_TwoRecipientsAllowAnonymous_StoresOneCopyForEach"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_TwoRecipientsAllowAnonymous_StoresOneCopyForEach() =>
        MeasureSmtpAsync(
            ["--allow-anonymous"],
            ["-sS", "--mail-from", "a@x", "--mail-rcpt", "b@y", "--mail-rcpt", "c@z", "-T", MailPlaceholder, SurlBase + "c"],
            InboxOwner.Anonymous,
            outcome =>
            {
                AssertExit(outcome, 0);
                AssertStoredCopies(outcome, 2, "ESMTP", SmtpMail);
            });

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_AnAccountAndAnUnknownRecipient_StoresOneCopyAndDiscardsTheOther"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_AnAccountAndAnUnknownRecipient_StoresOneCopyAndDiscardsTheOther() =>
        MeasureSmtpAsync(
            Accounts,
            ["-sS", "-u", Credentials, "--mail-from", "a@x", "--mail-rcpt", "tester@x", "--mail-rcpt", "nobody@x", "-T", MailPlaceholder, SurlBase + "c"],
            InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, 0);
                AssertStoredCopies(outcome, 1, "ESMTPA", SmtpMail);
            });

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_AnInvalidRecipient_ExitsSendErrorAndStoresNothing"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_AnInvalidRecipient_ExitsSendErrorAndStoresNothing() =>
        MeasureSmtpAsync(
            ["--allow-anonymous"],
            ["-sS", "--mail-from", "a@x", "--mail-rcpt", "b@y", "--mail-rcpt", "Bob <c@z>", "-T", MailPlaceholder, SurlBase + "c"],
            InboxOwner.Anonymous,
            outcome =>
            {
                AssertExit(outcome, SendError);
                StringAssert.Contains(outcome.Last.StandardError, "RCPT failed: 501");
                Assert.IsEmpty(outcome.Inbox);
            },
            "RCPT failed: 501");

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_AnInvalidRecipientWithMailRcptAllowfails_StoresOneCopy"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_AnInvalidRecipientWithMailRcptAllowfails_StoresOneCopy() =>
        MeasureSmtpAsync(
            ["--allow-anonymous"],
            ["-sS", "--mail-rcpt-allowfails", "--mail-from", "a@x", "--mail-rcpt", "b@y", "--mail-rcpt", "Bob <c@z>", "-T", MailPlaceholder, SurlBase + "c"],
            InboxOwner.Anonymous,
            outcome =>
            {
                AssertExit(outcome, 0);
                AssertStoredCopies(outcome, 1, "ESMTP", SmtpMail);
            });

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_SslReqdWithSelfSigned_UpgradesWithStartTlsAndStoresTheMessage"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_SslReqdWithSelfSigned_UpgradesWithStartTlsAndStoresTheMessage() =>
        MeasureSmtpAsync(
            ["--self-signed", .. Accounts], Send(["-k", "--ssl-reqd", "-u", Credentials]), InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, 0);
                AssertStoredCopies(outcome, 1, "ESMTPSA", SmtpMail);
            });

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_SslReqdWithoutACertificate_ExitsUseSslFailed"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_SslReqdWithoutACertificate_ExitsUseSslFailed() =>
        MeasureSmtpAsync(
            Accounts, Send(["--ssl-reqd", "-u", Credentials]), InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, UseSslFailed);
                Assert.IsEmpty(outcome.Inbox);
            });

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_SmtpsWithSelfSigned_StoresTheMessage"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_SmtpsWithSelfSigned_StoresTheMessage() =>
        MeasureSmtpAsync(
            ["--self-signed", .. Accounts], Send(["-k", "-u", Credentials]), InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, 0);
                AssertStoredCopies(outcome, 1, "ESMTPSA", SmtpMail);
            },
            scheme: "smtps");

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_ForcedPasswordMechanism_LogsInAndStoresTheMessage"/>.</summary>
    [TestMethod]
    [DataRow("CRAM-MD5", false, "ESMTPA", DisplayName = "CRAM-MD5 over smtp")]
    [DataRow("CRAM-MD5", true, "ESMTPA", DisplayName = "CRAM-MD5 over smtp with --sasl-ir")]
    [DataRow("PLAIN", false, "ESMTPSA", DisplayName = "PLAIN after STARTTLS")]
    [DataRow("PLAIN", true, "ESMTPSA", DisplayName = "PLAIN after STARTTLS with --sasl-ir")]
    [DataRow("LOGIN", false, "ESMTPSA", DisplayName = "LOGIN after STARTTLS")]
    [DataRow("LOGIN", true, "ESMTPSA", DisplayName = "LOGIN after STARTTLS with --sasl-ir")]
    public Task Smtp_Send_ForcedPasswordMechanism_LogsInAndStoresTheMessage(string mechanism, bool initialResponse, string protocol)
    {
        var overTls = protocol == "ESMTPSA";
        return MeasureSmtpAsync(
            overTls ? ["--self-signed", .. Accounts] : Accounts,
            Send(
            [
                "-v", .. overTls ? (string[])["-k", "--ssl-reqd"] : [], .. initialResponse ? (string[])["--sasl-ir"] : [],
                "-u", Credentials, "--login-options", $"AUTH={mechanism}",
            ]),
            InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, 0);
                StringAssert.Contains(outcome.Last.StandardError, $"AUTH {mechanism}");
                AssertStoredCopies(outcome, 1, protocol, SmtpMail);
            },
            $"AUTH {mechanism}",
            row: Row(mechanism, initialResponse, protocol));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_ForcedBearerMechanismAfterStartTls_LogsInAsTheNamedAccountAndStoresTheMessage"/>.</summary>
    [TestMethod]
    [DataRow("XOAUTH2", false)]
    [DataRow("XOAUTH2", true)]
    [DataRow("OAUTHBEARER", false)]
    [DataRow("OAUTHBEARER", true)]
    public Task Smtp_Send_ForcedBearerMechanismAfterStartTls_LogsInAsTheNamedAccountAndStoresTheMessage(string mechanism, bool initialResponse) =>
        MeasureSmtpAsync(
            ["--self-signed", .. Accounts],
            Send(
            [
                "-v", "-k", "--ssl-reqd", .. initialResponse ? (string[])["--sasl-ir"] : [],
                "-u", $"{AccountsFile.User}:", "--oauth2-bearer", AccountsFile.BearerToken, "--login-options", $"AUTH={mechanism}",
            ]),
            InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, 0);
                StringAssert.Contains(outcome.Last.StandardError, $"AUTH {mechanism}");
                AssertStoredCopies(outcome, 1, "ESMTPSA", SmtpMail);
            },
            $"AUTH {mechanism}",
            row: Row(mechanism, initialResponse));

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_ForcedNamedChoiceMechanism_LogsInAndStoresTheMessage"/>.</summary>
    [TestMethod]
    [DataRow("DIGEST-MD5", "digest-md5", false)]
    [DataRow("DIGEST-MD5", "digest-md5", true)]
    [DataRow("NTLM", "ntlm", false)]
    [DataRow("NTLM", "ntlm", true)]
    public Task Smtp_Send_ForcedNamedChoiceMechanism_LogsInAndStoresTheMessage(string mechanism, string authWord, bool initialResponse) =>
        MeasureSmtpAsync(
            ["--auth", authWord, .. Accounts],
            Send(["-v", .. initialResponse ? (string[])["--sasl-ir"] : [], "-u", Credentials, "--login-options", $"AUTH={mechanism}"]),
            InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, 0);
                StringAssert.Contains(outcome.Last.StandardError, $"AUTH {mechanism}");
                AssertStoredCopies(outcome, 1, "ESMTPA", SmtpMail);
            },
            $"AUTH {mechanism}",
            row: Row(mechanism, authWord, initialResponse));

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_PlainOverSmtpWithoutTls_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_PlainOverSmtpWithoutTls_ExitsLoginDenied() =>
        MeasureSmtpAsync(
            Accounts, Send(["-u", Credentials, "--login-options", "AUTH=PLAIN"]), InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, LoginDenied);
                Assert.IsEmpty(outcome.Inbox);
            });

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_PlainOverSmtpWithAllowPlaintextAuth_StoresTheMessage"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_PlainOverSmtpWithAllowPlaintextAuth_StoresTheMessage() =>
        MeasureSmtpAsync(
            ["--allow-plaintext-auth", .. Accounts], Send(["-u", Credentials, "--login-options", "AUTH=PLAIN"]), InboxOwner.Tester,
            outcome =>
            {
                AssertExit(outcome, 0);
                AssertStoredCopies(outcome, 1, "ESMTPA", SmtpMail);
            });

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_MessageOverAMebibyteWithMaxFilesize1000_ExitsSendErrorAndStoresNothing"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_MessageOverAMebibyteWithMaxFilesize1000_ExitsSendErrorAndStoresNothing() =>
        MeasureSmtpAsync(
            ["--allow-anonymous", "--max-filesize", "1000"],
            ["-sS", "--mail-from", "a@x", "--mail-rcpt", "b@y", "-T", MailPlaceholder, SurlBase + "c"],
            InboxOwner.Anonymous,
            outcome =>
            {
                AssertExit(outcome, SendError);
                StringAssert.Contains(outcome.Last.StandardError, "MAIL failed: 552");
                Assert.IsEmpty(outcome.Inbox);
            },
            "MAIL failed: 552",
            mail: [.. SmtpMail, .. Enumerable.Repeat((byte)'x', 1_100_000), .. "\r\n"u8]);

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_MaxFilesize10_ExitsSendErrorAndStoresNothing"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_MaxFilesize10_ExitsSendErrorAndStoresNothing() =>
        MeasureSmtpAsync(
            ["--allow-anonymous", "--max-filesize", "10"], Send([]), InboxOwner.Anonymous,
            outcome =>
            {
                AssertExit(outcome, SendError);
                StringAssert.Contains(outcome.Last.StandardError, "MAIL failed: 552");
                Assert.IsEmpty(outcome.Inbox);
            },
            "MAIL failed: 552");

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Command_WithMailRcpt_WritesTheReplyToStandardOutput"/>.</summary>
    [TestMethod]
    [DataRow("VRFY", "b@y", "252 2.1.5 Cannot verify the user, but will accept the message\r\n")]
    [DataRow("EXPN", "list", "252 2.1.5 Cannot expand the list, but will accept the message\r\n")]
    public Task Smtp_Command_WithMailRcpt_WritesTheReplyToStandardOutput(string command, string argument, string reply) =>
        MeasureSmtpAsync(
            ["--allow-anonymous"], ["-sS", "--mail-rcpt", argument, "-X", command, SurlBase + "c"], InboxOwner.None,
            outcome => AssertOutput(outcome, 0, reply),
            row: Row(command, argument));

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.NothingToSend_SendsHelp_WritesTheHelpReplyToStandardOutput"/>.</summary>
    [TestMethod]
    public Task Smtp_NothingToSend_SendsHelp_WritesTheHelpReplyToStandardOutput() =>
        MeasureSmtpAsync(
            ["--allow-anonymous"], ["-sS", SurlBase + "c"], InboxOwner.None,
            outcome => AssertOutput(outcome, 0, "214 2.0.0 Commands: EHLO HELO STARTTLS AUTH MAIL RCPT DATA RSET NOOP VRFY EXPN HELP QUIT\r\n"));

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Noop_WritesTheReplyToStandardOutput"/>.</summary>
    [TestMethod]
    public Task Smtp_Noop_WritesTheReplyToStandardOutput() =>
        MeasureSmtpAsync(
            ["--allow-anonymous"], ["-sS", "-X", "NOOP", SurlBase + "c"], InboxOwner.None,
            outcome => AssertOutput(outcome, 0, "250 2.0.0 OK\r\n"));

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_BareLineFeedsWithCrlf_StoresTheMessageWithCrlfLineEnds"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_BareLineFeedsWithCrlf_StoresTheMessageWithCrlfLineEnds() =>
        MeasureSmtpAsync(
            ["--allow-anonymous"],
            ["-sS", "--crlf", "--mail-from", "a@x", "--mail-rcpt", "b@y", "-T", MailPlaceholder, SurlBase + "c"],
            InboxOwner.Anonymous,
            outcome =>
            {
                AssertExit(outcome, 0);
                AssertStoredCopies(outcome, 1, "ESMTP", SmtpMail);
            },
            mail: SmtpMailWithBareLineFeeds);

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_BareLineFeeds_StoresThemAsSentWithACrlfBeforeTheEnd"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_BareLineFeeds_StoresThemAsSentWithACrlfBeforeTheEnd() =>
        MeasureSmtpAsync(
            ["--allow-anonymous"],
            ["-sS", "--mail-from", "a@x", "--mail-rcpt", "b@y", "-T", MailPlaceholder, SurlBase + "c"],
            InboxOwner.Anonymous,
            outcome =>
            {
                AssertExit(outcome, 0);
                AssertStoredCopies(outcome, 1, "ESMTP", [.. SmtpMailWithBareLineFeeds, .. "\r\n"u8]);
            },
            mail: SmtpMailWithBareLineFeeds);

    /// <summary>Mirrors <see cref="UpstreamCurlSendsMailToSurlOverSmtpTests.Send_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply"/>.</summary>
    [TestMethod]
    public Task Smtp_Send_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply() =>
        MeasureSmtpAsync(
            ["--allow-anonymous", "--max-connections", "1"], Send([]), InboxOwner.None,
            outcome =>
            {
                AssertExit(outcome, WeirdServerReply);
                StringAssert.Contains(outcome.Last.StandardError, "421");
            },
            "421",
            hold: MailHold.Greeting);

    // ----- IMAP -----

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.List_Root_WritesTheInboxListLine"/>.</summary>
    [TestMethod]
    public Task Imap_List_Root_WritesTheInboxListLine() =>
        MeasureImapAsync([], ["-sS", "-u", Credentials, SurlBase], outcome => AssertOutput(outcome, 0, "* LIST (\\HasNoChildren) \"/\" INBOX\r\n"));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.FetchByUid_MailSentOverSmtp_WritesTheStoredMessageByteForByteAfterACramMd5Login"/>.</summary>
    [TestMethod]
    public Task Imap_FetchByUid_MailSentOverSmtp_WritesTheStoredMessageByteForByteAfterACramMd5Login() =>
        MeasureImapAsync(
            [], ["-sS", "-v", "-u", Credentials, SurlBase + "INBOX;UID=1"],
            outcome =>
            {
                AssertExit(outcome, 0);
                StringAssert.Contains(outcome.Last.StandardError, "AUTHENTICATE CRAM-MD5");
                AssertIsTheDeliveredMessage(outcome, "ESMTPA");
            },
            "AUTHENTICATE CRAM-MD5");

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.FetchByMailIndex_WritesTheStoredMessage"/>.</summary>
    [TestMethod]
    public Task Imap_FetchByMailIndex_WritesTheStoredMessage() =>
        MeasureImapAsync([], ["-sS", "-u", Credentials, SurlBase + "INBOX;MAILINDEX=1"], outcome => AssertFetchedDelivered(outcome, "ESMTPA"));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.FetchSectionText_WritesTheBody"/>.</summary>
    [TestMethod]
    public Task Imap_FetchSectionText_WritesTheBody() =>
        MeasureImapAsync([], ["-sS", "-u", Credentials, SurlBase + "INBOX;UID=1;SECTION=TEXT"], outcome => AssertOutput(outcome, 0, "hello\r\n"));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.FetchSectionHeaderFields_WritesTheSubjectField"/>.</summary>
    [TestMethod]
    public Task Imap_FetchSectionHeaderFields_WritesTheSubjectField() =>
        MeasureImapAsync(
            [], ["-sS", "-u", Credentials, SurlBase + "INBOX;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)"],
            outcome => AssertOutput(outcome, 0, "Subject: hi\r\n\r\n"));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.FetchPartial_WritesTheFirstTenBytesOfTheStoredMessage"/>.</summary>
    [TestMethod]
    public Task Imap_FetchPartial_WritesTheFirstTenBytesOfTheStoredMessage() =>
        MeasureImapAsync(
            [], ["-sS", "-u", Credentials, SurlBase + "INBOX;UID=1;PARTIAL=0.10"],
            outcome =>
            {
                AssertExit(outcome, 0);
                Assert.HasCount(1, outcome.Inbox);
                CollectionAssert.AreEqual(outcome.Inbox[0][..10], outcome.Last.StandardOutput);
            });

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.FetchWithAStaleUidValidity_ExitsRemoteFileNotFound"/>.</summary>
    [TestMethod]
    public Task Imap_FetchWithAStaleUidValidity_ExitsRemoteFileNotFound() =>
        MeasureImapAsync(
            [], ["-sS", "-u", Credentials, SurlBase + "INBOX;UIDVALIDITY=1/;UID=1"],
            outcome =>
            {
                AssertExit(outcome, RemoteFileNotFound);
                StringAssert.Contains(outcome.Last.StandardError, "Mailbox UIDVALIDITY has changed");
                Assert.IsEmpty(outcome.Last.StandardOutput);
            },
            "Mailbox UIDVALIDITY has changed");

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.FetchAUidNoMessageHas_ExitsRemoteFileNotFound"/>.</summary>
    [TestMethod]
    public Task Imap_FetchAUidNoMessageHas_ExitsRemoteFileNotFound() =>
        MeasureImapAsync([], ["-sS", "-u", Credentials, SurlBase + "INBOX;UID=9"], outcome => AssertExitWithNoOutput(outcome, RemoteFileNotFound));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.FetchFromAMailboxThatDoesNotExist_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    public Task Imap_FetchFromAMailboxThatDoesNotExist_ExitsLoginDenied() =>
        MeasureImapAsync(
            [], ["-sS", "-u", Credentials, SurlBase + "Nope;UID=1"],
            outcome =>
            {
                AssertExit(outcome, LoginDenied);
                StringAssert.Contains(outcome.Last.StandardError, "Select failed");
            },
            "Select failed");

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Search_WritesTheSearchLine"/>.</summary>
    [TestMethod]
    [DataRow("SUBJECT%20hi", "* SEARCH 1\r\n", DisplayName = "a match")]
    [DataRow("SUBJECT%20nothing", "* SEARCH\r\n", DisplayName = "no match")]
    public Task Imap_Search_WritesTheSearchLine(string query, string expected) =>
        MeasureImapAsync([], ["-sS", "-u", Credentials, $"{SurlBase}INBOX?{query}"], outcome => AssertOutput(outcome, 0, expected), row: Row(query));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Append_ThenFetchTheNewUid_WritesTheAppendedBytesWithNoTraceFields"/>.</summary>
    [TestMethod]
    public Task Imap_Append_ThenFetchTheNewUid_WritesTheAppendedBytesWithNoTraceFields() =>
        MeasureAsync(
            ImapCase(
                [],
                [["-sS", "-u", Credentials, "-T", MailPlaceholder, SurlBase + "INBOX"], ["-sS", "-u", Credentials, SurlBase + "INBOX;UID=2"]],
                outcome =>
                {
                    Assert.AreEqual(0, outcome.Steps[0].ExitCode, outcome.Steps[0].StandardError);
                    Assert.IsEmpty(outcome.Steps[0].StandardOutput);
                    Assert.AreEqual(0, outcome.Steps[1].ExitCode, outcome.Steps[1].StandardError);
                    CollectionAssert.AreEqual(ReadMail, outcome.Steps[1].StandardOutput);
                }));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Append_PastMaxFilesize_ExitsUploadFailedAndStoresNothing"/>.</summary>
    [TestMethod]
    public Task Imap_Append_PastMaxFilesize_ExitsUploadFailedAndStoresNothing() =>
        MeasureAsync(
            ImapCase(
                ["--max-filesize", "10"],
                [["-sS", "-u", Credentials, "-T", MailPlaceholder, SurlBase + "INBOX"]],
                outcome =>
                {
                    AssertExit(outcome, UploadFailed);
                    Assert.IsEmpty(outcome.Inbox);
                }) with
            { Seed = MailSeed.None });

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Append_ToAMailboxThatDoesNotExist_ExitsUploadFailed"/>.</summary>
    [TestMethod]
    public Task Imap_Append_ToAMailboxThatDoesNotExist_ExitsUploadFailed() =>
        MeasureImapAsync([], ["-sS", "-u", Credentials, "-T", MailPlaceholder, SurlBase + "Nope"], outcome => AssertExit(outcome, UploadFailed));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.CustomCreate_ThenList_ListsInboxAndTheNewMailbox"/>.</summary>
    [TestMethod]
    public Task Imap_CustomCreate_ThenList_ListsInboxAndTheNewMailbox() =>
        MeasureAsync(
            ImapCase(
                [],
                [["-sS", "-u", Credentials, "-X", "CREATE Archive", SurlBase], ["-sS", "-u", Credentials, SurlBase]],
                outcome =>
                {
                    Assert.AreEqual(0, outcome.Steps[0].ExitCode, outcome.Steps[0].StandardError);
                    Assert.IsEmpty(outcome.Steps[0].StandardOutput);
                    AssertOutput(outcome, 0, "* LIST (\\HasNoChildren) \"/\" INBOX\r\n* LIST (\\HasNoChildren) \"/\" Archive\r\n");
                }));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.CustomExamine_WritesTheExamineUntaggedLines"/>.</summary>
    [TestMethod]
    public Task Imap_CustomExamine_WritesTheExamineUntaggedLines() =>
        MeasureImapAsync(
            [], ["-sS", "-u", Credentials, "-X", "EXAMINE INBOX", SurlBase],
            outcome =>
            {
                AssertExit(outcome, 0);
                var text = Encoding.ASCII.GetString(outcome.Last.StandardOutput);
                Assert.IsTrue(
                    Regex.IsMatch(
                        text,
                        @"\A\* FLAGS \(\\Answered \\Flagged \\Deleted \\Seen \\Draft\)\r\n" +
                        @"\* OK \[PERMANENTFLAGS \(\)\] No permanent flags permitted\r\n" +
                        @"\* 1 EXISTS\r\n" +
                        @"\* 0 RECENT\r\n" +
                        @"\* OK \[UNSEEN 1\] First unseen\r\n" +
                        @"\* OK \[UIDVALIDITY \d+\] UIDs valid\r\n" +
                        @"\* OK \[UIDNEXT 2\] Predicted next UID\r\n\z",
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromSeconds(1)),
                    text);
            });

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.CustomStore_OnASelectedMailbox_WritesTheNewFlags"/>.</summary>
    [TestMethod]
    public Task Imap_CustomStore_OnASelectedMailbox_WritesTheNewFlags() =>
        MeasureImapAsync(
            [], ["-sS", "-u", Credentials, "-X", "STORE 1 +FLAGS \\Deleted", SurlBase + "INBOX"],
            outcome => AssertOutput(outcome, 0, "* 1 FETCH (FLAGS (\\Deleted))\r\n"));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.CustomStore_ThenExpunge_WritesTheExpungeLine"/>.</summary>
    [TestMethod]
    public Task Imap_CustomStore_ThenExpunge_WritesTheExpungeLine() =>
        MeasureAsync(
            ImapCase(
                [],
                [["-sS", "-u", Credentials, "-X", "STORE 1 +FLAGS \\Deleted", SurlBase + "INBOX"], ["-sS", "-u", Credentials, "-X", "EXPUNGE", SurlBase + "INBOX"]],
                outcome =>
                {
                    Assert.AreEqual(0, outcome.Steps[0].ExitCode, outcome.Steps[0].StandardError);
                    Assert.AreEqual("* 1 FETCH (FLAGS (\\Deleted))\r\n", Encoding.ASCII.GetString(outcome.Steps[0].StandardOutput));
                    AssertOutput(outcome, 0, "* 1 EXPUNGE\r\n");
                    Assert.IsEmpty(outcome.Inbox);
                }));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.CustomStatus_WritesTheStatusLine"/>.</summary>
    [TestMethod]
    public Task Imap_CustomStatus_WritesTheStatusLine() =>
        MeasureImapAsync(
            [], ["-sS", "-u", Credentials, "-X", "STATUS INBOX (MESSAGES)", SurlBase],
            outcome => AssertOutput(outcome, 0, "* STATUS INBOX (MESSAGES 1)\r\n"));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.CustomCommandRefused_ExitsQuoteError"/>.</summary>
    [TestMethod]
    [DataRow("STORE 1 +FLAGS (\\Seen)", DisplayName = "STORE with no mailbox selected")]
    [DataRow("XYZZY", DisplayName = "an unknown command")]
    public Task Imap_CustomCommandRefused_ExitsQuoteError(string command) =>
        MeasureImapAsync(
            [], ["-sS", "-u", Credentials, "-X", command, SurlBase],
            outcome =>
            {
                AssertExit(outcome, QuoteError);
                StringAssert.Contains(outcome.Last.StandardError, "Quote command returned error");
            },
            "Quote command returned error",
            row: Row(command));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_WrongPassword_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    public Task Imap_Fetch_WrongPassword_ExitsLoginDenied() =>
        MeasureImapAsync([], ["-sS", "-u", $"{AccountsFile.User}:wrong", SurlBase + "INBOX;UID=1"], outcome => AssertExitWithNoOutput(outcome, LoginDenied));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_NoLoginWithoutAllowAnonymous_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    public Task Imap_Fetch_NoLoginWithoutAllowAnonymous_ExitsLoginDenied() =>
        MeasureImapAsync([], ["-sS", SurlBase + "INBOX;UID=1"], outcome => AssertExitWithNoOutput(outcome, LoginDenied));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_NoLoginWithAllowAnonymous_WritesTheMessageSentAnonymouslyOverSmtp"/>.</summary>
    [TestMethod]
    public Task Imap_Fetch_NoLoginWithAllowAnonymous_WritesTheMessageSentAnonymouslyOverSmtp() =>
        MeasureAsync(
            ImapCase([], [["-sS", SurlBase + "INBOX;UID=1"]], outcome => AssertFetchedDelivered(outcome, "ESMTP")) with
            {
                SurlOptions = ["--allow-anonymous"],
                Seed = MailSeed.OverSmtpAnonymously,
                Inbox = InboxOwner.Anonymous,
            });

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ClearPasswordLoginOverPlaintext_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    [DataRow("AUTH=PLAIN", DisplayName = "AUTHENTICATE PLAIN")]
    [DataRow("AUTH=+LOGIN", DisplayName = "the LOGIN command")]
    public Task Imap_Fetch_ClearPasswordLoginOverPlaintext_ExitsLoginDenied(string loginOptions) =>
        MeasureImapAsync(
            [], ["-sS", "-u", Credentials, "--login-options", loginOptions, SurlBase + "INBOX;UID=1"],
            outcome => AssertExitWithNoOutput(outcome, LoginDenied),
            row: Row(loginOptions));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ClearPasswordLoginOverPlaintextWithAllowPlaintextAuth_WritesTheStoredMessage"/>.</summary>
    [TestMethod]
    [DataRow("AUTH=PLAIN", "AUTHENTICATE PLAIN", DisplayName = "AUTHENTICATE PLAIN")]
    [DataRow("AUTH=LOGIN", "AUTHENTICATE LOGIN", DisplayName = "AUTHENTICATE LOGIN")]
    [DataRow("AUTH=+LOGIN", "LOGIN tester", DisplayName = "the LOGIN command")]
    public Task Imap_Fetch_ClearPasswordLoginOverPlaintextWithAllowPlaintextAuth_WritesTheStoredMessage(string loginOptions, string sent) =>
        MeasureImapAsync(
            ["--allow-plaintext-auth"], ["-sS", "-v", "-u", Credentials, "--login-options", loginOptions, SurlBase + "INBOX;UID=1"],
            outcome => AssertSentAndFetchedDelivered(outcome, sent),
            sent,
            row: Row(loginOptions));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_SslReqdWithSelfSigned_UpgradesWithStartTlsAndWritesTheStoredMessage"/>.</summary>
    [TestMethod]
    public Task Imap_Fetch_SslReqdWithSelfSigned_UpgradesWithStartTlsAndWritesTheStoredMessage() =>
        MeasureImapAsync(
            ["--self-signed"], ["-sS", "-v", "-k", "--ssl-reqd", "-u", Credentials, SurlBase + "INBOX;UID=1"],
            outcome => AssertSentAndFetchedDelivered(outcome, "STARTTLS"),
            "STARTTLS");

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_SslReqdWithoutACertificate_ExitsUseSslFailed"/>.</summary>
    [TestMethod]
    public Task Imap_Fetch_SslReqdWithoutACertificate_ExitsUseSslFailed() =>
        MeasureImapAsync([], ["-sS", "--ssl-reqd", "-u", Credentials, SurlBase + "INBOX;UID=1"], outcome => AssertExitWithNoOutput(outcome, UseSslFailed));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ImapsWithSelfSigned_WritesTheStoredMessage"/>.</summary>
    [TestMethod]
    public Task Imap_Fetch_ImapsWithSelfSigned_WritesTheStoredMessage() =>
        MeasureImapAsync(
            ["--self-signed"], ["-sS", "-k", "-u", Credentials, SurlBase + "INBOX;UID=1"],
            outcome => AssertFetchedDelivered(outcome, "ESMTPA"),
            scheme: "imaps");

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ImapsForcedPasswordLogin_LogsInAndWritesTheStoredMessage"/>.</summary>
    [TestMethod]
    [DataRow("AUTH=PLAIN", false, "AUTHENTICATE PLAIN", DisplayName = "AUTHENTICATE PLAIN")]
    [DataRow("AUTH=PLAIN", true, "AUTHENTICATE PLAIN", DisplayName = "AUTHENTICATE PLAIN with --sasl-ir")]
    [DataRow("AUTH=LOGIN", false, "AUTHENTICATE LOGIN", DisplayName = "AUTHENTICATE LOGIN")]
    [DataRow("AUTH=CRAM-MD5", false, "AUTHENTICATE CRAM-MD5", DisplayName = "AUTHENTICATE CRAM-MD5")]
    [DataRow("AUTH=+LOGIN", false, "LOGIN tester", DisplayName = "the LOGIN command")]
    public Task Imap_Fetch_ImapsForcedPasswordLogin_LogsInAndWritesTheStoredMessage(string loginOptions, bool initialResponse, string sent) =>
        MeasureImapAsync(
            ["--self-signed"],
            ["-sS", "-v", "-k", .. initialResponse ? (string[])["--sasl-ir"] : [], "-u", Credentials, "--login-options", loginOptions, SurlBase + "INBOX;UID=1"],
            outcome => AssertSentAndFetchedDelivered(outcome, sent),
            sent,
            scheme: "imaps",
            row: Row(loginOptions, initialResponse));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ImapsForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage"/>.</summary>
    [TestMethod]
    [DataRow("XOAUTH2", false)]
    [DataRow("XOAUTH2", true)]
    [DataRow("OAUTHBEARER", false)]
    [DataRow("OAUTHBEARER", true)]
    public Task Imap_Fetch_ImapsForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage(string mechanism, bool initialResponse) =>
        MeasureImapAsync(
            ["--self-signed"],
            [
                "-sS", "-v", "-k", .. initialResponse ? (string[])["--sasl-ir"] : [],
                "-u", $"{AccountsFile.User}:", "--oauth2-bearer", AccountsFile.BearerToken, "--login-options", $"AUTH={mechanism}",
                SurlBase + "INBOX;UID=1",
            ],
            outcome => AssertSentAndFetchedDelivered(outcome, $"AUTHENTICATE {mechanism}"),
            $"AUTHENTICATE {mechanism}",
            scheme: "imaps",
            row: Row(mechanism, initialResponse));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_ImapsForcedNamedChoiceMechanism_LogsInAndWritesTheStoredMessage"/>.</summary>
    [TestMethod]
    [DataRow("DIGEST-MD5", "digest-md5")]
    [DataRow("NTLM", "ntlm")]
    public Task Imap_Fetch_ImapsForcedNamedChoiceMechanism_LogsInAndWritesTheStoredMessage(string mechanism, string authWord) =>
        MeasureImapAsync(
            ["--self-signed", "--auth", authWord],
            ["-sS", "-v", "-k", "-u", Credentials, "--login-options", $"AUTH={mechanism}", SurlBase + "INBOX;UID=1"],
            outcome => AssertSentAndFetchedDelivered(outcome, $"AUTHENTICATE {mechanism}"),
            $"AUTHENTICATE {mechanism}",
            scheme: "imaps",
            row: Row(mechanism, authWord));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply"/>.</summary>
    [TestMethod]
    public Task Imap_Fetch_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply() =>
        MeasureAsync(
            ImapCase(["--max-connections", "1"], [["-sS", "-u", Credentials, SurlBase + "INBOX;UID=1"]], outcome => AssertExit(outcome, WeirdServerReply)) with
            {
                Hold = MailHold.Greeting,
            });

    // ----- POP3 -----

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.List_Root_WritesOneScanListingLineAfterACramMd5Login"/>.</summary>
    [TestMethod]
    [DataRow(new string[0], DisplayName = "LIST")]
    [DataRow(new[] { "-l" }, DisplayName = "LIST with -l")]
    [DataRow(new[] { "-I" }, DisplayName = "LIST with -I")]
    public Task Pop3_List_Root_WritesOneScanListingLineAfterACramMd5Login(string[] listOptions) =>
        MeasurePop3Async(
            [], ["-sS", "-v", .. listOptions, "-u", Credentials, SurlBase],
            outcome =>
            {
                AssertExit(outcome, 0);
                StringAssert.Contains(outcome.Last.StandardError, "AUTH CRAM-MD5");
                Assert.HasCount(1, outcome.Inbox);
                Assert.AreEqual($"1 {outcome.Inbox[0].Length}\r\n", Encoding.ASCII.GetString(outcome.Last.StandardOutput));
            },
            "AUTH CRAM-MD5",
            row: Row(string.Join(' ', listOptions)));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_MailSentOverSmtp_WritesTheStoredMessageByteForByte"/>.</summary>
    [TestMethod]
    [DataRow(new string[0], DisplayName = "RETR")]
    [DataRow(new[] { "-I" }, DisplayName = "RETR with -I")]
    public Task Pop3_Retrieve_MailSentOverSmtp_WritesTheStoredMessageByteForByte(string[] retrieveOptions) =>
        MeasurePop3Async(
            [], ["-sS", .. retrieveOptions, "-u", Credentials, SurlBase + "1"],
            outcome => AssertFetchedDelivered(outcome, "ESMTPA"),
            row: Row(string.Join(' ', retrieveOptions)));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.ListOnly_OneMessage_WritesNothing"/>.</summary>
    [TestMethod]
    public Task Pop3_ListOnly_OneMessage_WritesNothing() =>
        MeasurePop3Async([], ["-sS", "-l", "-u", Credentials, SurlBase + "1"], outcome => AssertOutput(outcome, 0, string.Empty));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_AMessageNumberPastTheMaildrop_ExitsWeirdServerReply"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_AMessageNumberPastTheMaildrop_ExitsWeirdServerReply() =>
        MeasurePop3Async([], ["-sS", "-u", Credentials, SurlBase + "9"], outcome => AssertExitWithNoOutput(outcome, WeirdServerReply));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.CustomUidl_WritesTheUniqueIdFromTheStoresUidValidityAndUid"/>.</summary>
    [TestMethod]
    public Task Pop3_CustomUidl_WritesTheUniqueIdFromTheStoresUidValidityAndUid() =>
        MeasurePop3Async([], ["-sS", "-u", Credentials, "-X", "UIDL", SurlBase], outcome => AssertOutput(outcome, 0, $"1 {outcome.UidValidity}.1\r\n"));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.CustomTop_WritesTheStoredHeaderSectionAndTheEmptyLine"/>.</summary>
    [TestMethod]
    [DataRow("TOP 1 0", "/", DisplayName = "TOP 1 0")]
    [DataRow("TOP", "/1", DisplayName = "TOP with the message in the URL")]
    public Task Pop3_CustomTop_WritesTheStoredHeaderSectionAndTheEmptyLine(string command, string path) =>
        MeasurePop3Async(
            [], ["-sS", "-u", Credentials, "-X", command, SurlBase + path[1..]],
            outcome =>
            {
                AssertExit(outcome, 0);
                Assert.HasCount(1, outcome.Inbox);
                var text = Encoding.ASCII.GetString(outcome.Inbox[0]);
                var headerSectionLength = text.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
                Assert.AreEqual(text[..headerSectionLength], Encoding.ASCII.GetString(outcome.Last.StandardOutput));
                StringAssert.EndsWith(text[..headerSectionLength], "Subject: hi\r\n\r\n");
            },
            row: Row(command, path));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.CustomCapa_AfterTheLogin_WritesTheFiveTransactionCapabilities"/>.</summary>
    [TestMethod]
    public Task Pop3_CustomCapa_AfterTheLogin_WritesTheFiveTransactionCapabilities() =>
        MeasurePop3Async(
            [], ["-sS", "-u", Credentials, "-X", "CAPA", SurlBase],
            outcome => AssertOutput(outcome, 0, "TOP\r\nUIDL\r\nRESP-CODES\r\nAUTH-RESP-CODE\r\nPIPELINING\r\n"));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.CustomSingleLineCommand_ExitsZeroAndWritesNothing"/>.</summary>
    [TestMethod]
    [DataRow("STAT")]
    [DataRow("NOOP")]
    [DataRow("RSET")]
    public Task Pop3_CustomSingleLineCommand_ExitsZeroAndWritesNothing(string command) =>
        MeasurePop3Async([], ["-sS", "-u", Credentials, "-X", command, SurlBase], outcome => AssertOutput(outcome, 0, string.Empty), row: Row(command));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.CustomDele_ThenList_ListsAnEmptyMaildropAndTheStoreHoldsNothing"/>.</summary>
    [TestMethod]
    public Task Pop3_CustomDele_ThenList_ListsAnEmptyMaildropAndTheStoreHoldsNothing() =>
        MeasureAsync(
            Pop3Case(
                [],
                [["-sS", "-u", Credentials, "-X", "DELE 1", SurlBase], ["-sS", "-u", Credentials, SurlBase]],
                outcome =>
                {
                    Assert.AreEqual(0, outcome.Steps[0].ExitCode, outcome.Steps[0].StandardError);
                    Assert.IsEmpty(outcome.Steps[0].StandardOutput);
                    AssertOutput(outcome, 0, "\r\n");
                    Assert.IsEmpty(outcome.Inbox);
                }));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.CustomUnknownCommand_ExitsWeirdServerReply"/>.</summary>
    [TestMethod]
    public Task Pop3_CustomUnknownCommand_ExitsWeirdServerReply() =>
        MeasurePop3Async([], ["-sS", "-u", Credentials, "-X", "XYZZY", SurlBase], outcome => AssertExit(outcome, WeirdServerReply));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_WrongPassword_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_WrongPassword_ExitsLoginDenied() =>
        MeasurePop3Async([], ["-sS", "-u", $"{AccountsFile.User}:wrong", SurlBase + "1"], outcome => AssertExitWithNoOutput(outcome, LoginDenied));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_NoLoginWithoutAllowAnonymous_ExitsWeirdServerReply"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_NoLoginWithoutAllowAnonymous_ExitsWeirdServerReply() =>
        MeasurePop3Async([], ["-sS", SurlBase + "1"], outcome => AssertExitWithNoOutput(outcome, WeirdServerReply));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_NoLoginWithAllowAnonymous_WritesTheMessageSentAnonymouslyOverSmtp"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_NoLoginWithAllowAnonymous_WritesTheMessageSentAnonymouslyOverSmtp() =>
        MeasureAsync(
            Pop3Case([], [["-sS", SurlBase + "1"]], outcome => AssertFetchedDelivered(outcome, "ESMTP")) with
            {
                SurlOptions = ["--allow-anonymous"],
                Seed = MailSeed.OverSmtpAnonymously,
                Inbox = InboxOwner.Anonymous,
            });

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_PlainOverPlaintext_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_PlainOverPlaintext_ExitsLoginDenied() =>
        MeasurePop3Async(
            [], ["-sS", "-u", Credentials, "--login-options", "AUTH=PLAIN", SurlBase + "1"], outcome => AssertExitWithNoOutput(outcome, LoginDenied));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_ClearPasswordMechanismOverPlaintextWithAllowPlaintextAuth_WritesTheStoredMessage"/>.</summary>
    [TestMethod]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    public Task Pop3_Retrieve_ClearPasswordMechanismOverPlaintextWithAllowPlaintextAuth_WritesTheStoredMessage(string mechanism) =>
        MeasurePop3Async(
            ["--allow-plaintext-auth"], ["-sS", "-v", "-u", Credentials, "--login-options", $"AUTH={mechanism}", SurlBase + "1"],
            outcome => AssertSentAndFetchedDelivered(outcome, $"AUTH {mechanism}"),
            $"AUTH {mechanism}",
            row: Row(mechanism));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_UserPassWithAllowPlaintextAuth_WritesTheStoredMessage"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_UserPassWithAllowPlaintextAuth_WritesTheStoredMessage() =>
        MeasurePop3Async(
            ["--auth", "basic", "--allow-plaintext-auth"], ["-sS", "-v", "-u", Credentials, SurlBase + "1"],
            outcome => AssertSentAndFetchedDelivered(outcome, $"USER {AccountsFile.User}"),
            $"USER {AccountsFile.User}");

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_UserPassOverPlaintextWithoutAllowPlaintextAuth_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_UserPassOverPlaintextWithoutAllowPlaintextAuth_ExitsLoginDenied() =>
        MeasurePop3Async(["--auth", "basic"], ["-sS", "-u", Credentials, SurlBase + "1"], outcome => AssertExitWithNoOutput(outcome, LoginDenied));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_UserPassWrongPassword_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_UserPassWrongPassword_ExitsLoginDenied() =>
        MeasurePop3Async(
            ["--auth", "basic", "--allow-plaintext-auth"], ["-sS", "-u", $"{AccountsFile.User}:wrong", SurlBase + "1"],
            outcome => AssertExitWithNoOutput(outcome, LoginDenied));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_Apop_WritesTheStoredMessage"/>.</summary>
    [TestMethod]
    [DataRow(true, DisplayName = "APOP forced with --login-options AUTH=+APOP")]
    [DataRow(false, DisplayName = "APOP chosen from the greeting's timestamp")]
    public Task Pop3_Retrieve_Apop_WritesTheStoredMessage(bool forced) =>
        MeasurePop3Async(
            ["--auth", "apop"],
            ["-sS", "-v", "-u", Credentials, .. forced ? (string[])["--login-options", "AUTH=+APOP"] : [], SurlBase + "1"],
            outcome => AssertSentAndFetchedDelivered(outcome, $"APOP {AccountsFile.User} "),
            $"APOP {AccountsFile.User} ",
            row: Row(forced));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_SslReqdWithSelfSigned_UpgradesWithStlsAndWritesTheStoredMessage"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_SslReqdWithSelfSigned_UpgradesWithStlsAndWritesTheStoredMessage() =>
        MeasurePop3Async(
            ["--self-signed"], ["-sS", "-v", "-k", "--ssl-reqd", "-u", Credentials, SurlBase + "1"],
            outcome => AssertSentAndFetchedDelivered(outcome, "STLS"),
            "STLS");

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_SslReqdWithoutACertificate_ExitsUseSslFailed"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_SslReqdWithoutACertificate_ExitsUseSslFailed() =>
        MeasurePop3Async([], ["-sS", "--ssl-reqd", "-u", Credentials, SurlBase + "1"], outcome => AssertExitWithNoOutput(outcome, UseSslFailed));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_Pop3sWithSelfSigned_WritesTheStoredMessage"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_Pop3sWithSelfSigned_WritesTheStoredMessage() =>
        MeasurePop3Async(
            ["--self-signed"], ["-sS", "-k", "-u", Credentials, SurlBase + "1"], outcome => AssertFetchedDelivered(outcome, "ESMTPA"), scheme: "pop3s");

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_Pop3sForcedPasswordMechanism_LogsInAndWritesTheStoredMessage"/>.</summary>
    [TestMethod]
    [DataRow("PLAIN", false)]
    [DataRow("PLAIN", true, DisplayName = "PLAIN with --sasl-ir")]
    [DataRow("LOGIN", false)]
    [DataRow("CRAM-MD5", false)]
    public Task Pop3_Retrieve_Pop3sForcedPasswordMechanism_LogsInAndWritesTheStoredMessage(string mechanism, bool initialResponse) =>
        MeasurePop3Async(
            ["--self-signed"],
            ["-sS", "-v", "-k", .. initialResponse ? (string[])["--sasl-ir"] : [], "-u", Credentials, "--login-options", $"AUTH={mechanism}", SurlBase + "1"],
            outcome => AssertSentAndFetchedDelivered(outcome, $"AUTH {mechanism}"),
            $"AUTH {mechanism}",
            scheme: "pop3s",
            row: Row(mechanism, initialResponse));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_Pop3sForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage"/>.</summary>
    [TestMethod]
    [DataRow("XOAUTH2")]
    [DataRow("OAUTHBEARER")]
    public Task Pop3_Retrieve_Pop3sForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage(string mechanism) =>
        MeasurePop3Async(
            ["--self-signed"],
            [
                "-sS", "-v", "-k", "-u", $"{AccountsFile.User}:", "--oauth2-bearer", AccountsFile.BearerToken,
                "--login-options", $"AUTH={mechanism}", SurlBase + "1",
            ],
            outcome => AssertSentAndFetchedDelivered(outcome, $"AUTH {mechanism}"),
            $"AUTH {mechanism}",
            scheme: "pop3s",
            row: Row(mechanism));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_Pop3sForcedNamedChoiceMechanism_LogsInAndWritesTheStoredMessage"/>.</summary>
    [TestMethod]
    [DataRow("DIGEST-MD5", "digest-md5")]
    [DataRow("NTLM", "ntlm")]
    public Task Pop3_Retrieve_Pop3sForcedNamedChoiceMechanism_LogsInAndWritesTheStoredMessage(string mechanism, string authWord) =>
        MeasurePop3Async(
            ["--self-signed", "--auth", authWord],
            ["-sS", "-v", "-k", "-u", Credentials, "--login-options", $"AUTH={mechanism}", SurlBase + "1"],
            outcome => AssertSentAndFetchedDelivered(outcome, $"AUTH {mechanism}"),
            $"AUTH {mechanism}",
            scheme: "pop3s",
            row: Row(mechanism, authWord));

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_Pop3sUserPass_WritesTheStoredMessage"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_Pop3sUserPass_WritesTheStoredMessage() =>
        MeasurePop3Async(
            ["--self-signed", "--auth", "basic"], ["-sS", "-v", "-k", "-u", Credentials, SurlBase + "1"],
            outcome => AssertSentAndFetchedDelivered(outcome, $"USER {AccountsFile.User}"),
            $"USER {AccountsFile.User}",
            scheme: "pop3s");

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_MaildropHeldByAnotherSession_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_MaildropHeldByAnotherSession_ExitsLoginDenied() =>
        MeasureAsync(
            Pop3Case(
                ["--allow-plaintext-auth"],
                [["-sS", "-v", "-u", Credentials, SurlBase + "1"]],
                outcome =>
                {
                    AssertExitWithNoOutput(outcome, LoginDenied);
                    StringAssert.Contains(outcome.Last.StandardError, "[IN-USE]");
                }) with
            {
                Hold = MailHold.Pop3Login,
                AssertedStandardErrorFragments = ["[IN-USE]"],
            });

    /// <summary>Mirrors <see cref="UpstreamCurlReadsMailFromSurlOverPop3Tests.Retrieve_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply"/>.</summary>
    [TestMethod]
    public Task Pop3_Retrieve_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply() =>
        MeasureAsync(
            Pop3Case(["--max-connections", "1"], [["-sS", "-u", Credentials, SurlBase + "1"]], outcome => AssertExit(outcome, WeirdServerReply)) with
            {
                Hold = MailHold.Greeting,
            });

    // ----- The mirrored assertions, applied to upstream's run -----

    private static void AssertExit(MailOutcome outcome, int exitCode) =>
        Assert.AreEqual(exitCode, outcome.Last.ExitCode, outcome.Last.StandardError);

    private static void AssertExitWithNoOutput(MailOutcome outcome, int exitCode)
    {
        AssertExit(outcome, exitCode);
        Assert.IsEmpty(outcome.Last.StandardOutput);
    }

    private static void AssertOutput(MailOutcome outcome, int exitCode, string output)
    {
        AssertExit(outcome, exitCode);
        Assert.AreEqual(output, Encoding.ASCII.GetString(outcome.Last.StandardOutput));
    }

    private static void AssertSentAndFetchedDelivered(MailOutcome outcome, string sent)
    {
        StringAssert.Contains(outcome.Last.StandardError, sent);
        AssertFetchedDelivered(outcome, "ESMTPA");
    }

    private static void AssertFetchedDelivered(MailOutcome outcome, string protocol)
    {
        AssertExit(outcome, 0);
        AssertIsTheDeliveredMessage(outcome, protocol);
    }

    // The stored message is ADR-0053 decision 6's Return-Path and Received fields, then mail.txt.
    private static void AssertIsTheDeliveredMessage(MailOutcome outcome, string protocol)
    {
        AssertStoredCopies(outcome, 1, protocol, ReadMail);
        CollectionAssert.AreEqual(outcome.Inbox[0], outcome.Last.StandardOutput);
    }

    private static void AssertStoredCopies(MailOutcome outcome, int copies, string protocol, byte[] body)
    {
        Assert.HasCount(copies, outcome.Inbox);
        foreach (var stored in outcome.Inbox)
        {
            var text = Encoding.UTF8.GetString(stored);
            var traceFields = Regex.Match(
                text,
                $@"\AReturn-Path: <a@x>\r\nReceived: from c \(\[127\.0\.0\.1\]\) by surl with {protocol}; [A-Z][a-z]{{2}}, \d{{2}} [A-Z][a-z]{{2}} \d{{4}} \d{{2}}:\d{{2}}:\d{{2}} \+0000\r\n",
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
            Assert.IsTrue(traceFields.Success, text);
            CollectionAssert.AreEqual(body, stored[traceFields.Length..]);
        }
    }

    // ----- The cases -----

    private static string[] Send(string[] options) =>
        ["-sS", .. options, "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", MailPlaceholder, SurlBase + "c"];

    private static string Row(params object[] values) => string.Join(", ", values);

    private Task MeasureSmtpAsync(
        string[] surlOptions,
        string[] arguments,
        InboxOwner inbox,
        Action<MailOutcome> check,
        string? assertedStandardError = null,
        string scheme = "smtp",
        byte[]? mail = null,
        MailHold hold = MailHold.None,
        string? row = null,
        [CallerMemberName] string test = "") =>
        MeasureAsync(new MailCase(CaseName(test, row), Mirrors(SmtpClass, test), scheme, [scheme], surlOptions, [arguments], check)
        {
            Inbox = inbox,
            MailFile = mail ?? SmtpMail,
            Hold = hold,
            AssertedStandardErrorFragments = assertedStandardError is null ? [] : [assertedStandardError],
        });

    private Task MeasureImapAsync(
        string[] surlOptions,
        string[] arguments,
        Action<MailOutcome> check,
        string? assertedStandardError = null,
        string scheme = "imap",
        string? row = null,
        [CallerMemberName] string test = "") =>
        MeasureAsync(ImapCase(surlOptions, [arguments], check, scheme, row, test) with
        {
            AssertedStandardErrorFragments = assertedStandardError is null ? [] : [assertedStandardError],
        });

    // The IMAP class starts surl on smtp beside imap or imaps with the accounts file and delivers
    // mail.txt to tester over smtp in the same surl first.
    private static MailCase ImapCase(
        string[] surlOptions,
        string[][] steps,
        Action<MailOutcome> check,
        string scheme = "imap",
        string? row = null,
        [CallerMemberName] string test = "") =>
        new(CaseName(test, row), Mirrors(ImapClass, test), scheme, ["smtp", scheme], [.. surlOptions, .. Accounts], steps, check)
        {
            Seed = MailSeed.OverSmtpAsTester,
            Inbox = InboxOwner.Tester,
            MailFile = ReadMail,
        };

    private Task MeasurePop3Async(
        string[] surlOptions,
        string[] arguments,
        Action<MailOutcome> check,
        string? assertedStandardError = null,
        string scheme = "pop3",
        string? row = null,
        [CallerMemberName] string test = "") =>
        MeasureAsync(Pop3Case(surlOptions, [arguments], check, scheme, row, test) with
        {
            AssertedStandardErrorFragments = assertedStandardError is null ? [] : [assertedStandardError],
        });

    // The POP3 class does the same, except that with --auth among surl's options the delivery is
    // made by an earlier surl over the same directory with the default login options.
    private static MailCase Pop3Case(
        string[] surlOptions,
        string[][] steps,
        Action<MailOutcome> check,
        string scheme = "pop3",
        string? row = null,
        [CallerMemberName] string test = "") =>
        new(CaseName(test, row), Mirrors(Pop3Class, test), scheme, ["smtp", scheme], [.. surlOptions, .. Accounts], steps, check)
        {
            Seed = surlOptions.Contains("--auth") ? MailSeed.BeforeStartAsTester : MailSeed.OverSmtpAsTester,
            Inbox = InboxOwner.Tester,
            MailFile = ReadMail,
        };

    private static string CaseName(string test, string? row) => row is null ? test : $"{test}({row})";

    // A Phase 7 test is named after the one it mirrors with its protocol before it: Smtp_, Imap_ or Pop3_.
    private static string Mirrors(string mirroredClass, string test) => $"{mirroredClass}.{test[(test.IndexOf('_', StringComparison.Ordinal) + 1)..]}";

    // ----- Running and judging -----

    private async Task MeasureAsync(MailCase mailCase)
    {
        RequireNoExtensionSwitch();
        var cancellationToken = TestContext.CancellationToken;
        var root = PinnedUpstreamCurl.RepositoryRoot();
        var upstreamPins = UpstreamCurlBuildPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, UpstreamCurlBuildPins.FileName), cancellationToken));
        var portPins = CurlPortClientPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, CurlPortClientPins.FileName), cancellationToken));
        var files = new FileSystemUpstreamCurlFileAccess();
        var upstreamLocation = new UpstreamCurlLocator(files)
            .LocateForProtocol(upstreamPins, UpstreamCurlLocator.CurrentPlatform, mailCase.Scheme);
        if (!upstreamLocation.IsAvailable)
        {
            Assert.Inconclusive(upstreamLocation.Message);
        }

        var portRunner = PinnedCurlPortClientOnThisMachine.RunnerFor(
            new CurlPortClientLocator(files).Locate(portPins, upstreamPins, UpstreamCurlLocator.CurrentPlatform));
        var upstreamRunner = new UpstreamCurlRunner(upstreamLocation, ClientTimeout, TimeProvider.System);
        var listedFindings = await ReadListedFindingsAsync(root, cancellationToken);

        using var accounts = await AccountsFile.WriteAsync(cancellationToken);
        var context = new MailRunContext(accounts.Path, await WriteMailFileAsync(mailCase.MailFile), await WriteMailFileAsync(ReadMail));

        var upstream = await RunClientAsync(
            mailCase, context, (arguments, environment) => upstreamRunner.RunAsync(arguments, ReadOnlyMemory<byte>.Empty, environment, cancellationToken));
        CheckUpstreamIsWhatTheMirroredTestAsserts(mailCase, upstream);
        var port = await RunClientAsync(
            mailCase, context, (arguments, environment) => portRunner.RunAsync(arguments, ReadOnlyMemory<byte>.Empty, environment, cancellationToken));

        var surlCommit = await SurlCommitAsync(root, cancellationToken);
        var findings = new List<CurlPortVerdict>();
        for (var step = 0; step < mailCase.Steps.Count; step++)
        {
            var verdict = CompareStep(mailCase, step, upstream, port, surlCommit);
            TestContext.WriteLine($"{mailCase.Name} step {step + 1}: {verdict}");
            if (verdict.Kind != CurlPortVerdictKind.Agreement)
            {
                findings.Add(verdict);
            }
        }

        var listed = listedFindings.Contains(mailCase.Name);
        if (findings.Count > 0)
        {
            Assert.IsTrue(
                listed,
                $"Port defect not listed in {FindingsFile} (ADR-0089 decision 5):\n{string.Join('\n', findings)}");
        }
        else
        {
            Assert.IsFalse(listed, $"{FindingsFile} lists {mailCase.Name}, but the port now agrees with upstream curl: remove the finding.");
        }
    }

    private static void RequireNoExtensionSwitch()
    {
        foreach (var name in ExtensionSwitches)
        {
            if (Environment.GetEnvironmentVariable(name) is not null)
            {
                Assert.Inconclusive($"{name} is set in the test process; ADR-0089 decision 3 runs both clients and surl as shipped, without it.");
            }
        }
    }

    private static void CheckUpstreamIsWhatTheMirroredTestAsserts(MailCase mailCase, ClientRun<UpstreamCurlRunResult> upstream)
    {
        var outcome = new MailOutcome(
            [.. upstream.Steps.Select(step => CurlPortMeasuredRun.Of(step.Result, step.Surl))], upstream.Inbox, upstream.UidValidity);
        try
        {
            mailCase.CheckMirroredAssertions(outcome);
        }
        catch (AssertFailedException failure)
        {
            Assert.Fail($"Surl defect (ADR-0089 decision 5): upstream curl's own run is not what {mailCase.Mirrors} asserts: {failure.Message}");
        }
    }

    private static CurlPortVerdict CompareStep(
        MailCase mailCase, int step, ClientRun<UpstreamCurlRunResult> upstream, ClientRun<CurlPortClientRunResult> port, string surlCommit)
    {
        var isLast = step == mailCase.Steps.Count - 1;
        var upstreamStored = NormaliseInbox(upstream);
        var portStored = NormaliseInbox(port);
        var stepCase = new CurlPortMeasurementCase(
            mailCase.Name,
            mailCase.Mirrors,
            mailCase.Scheme,
            [.. mailCase.Steps[step].Select(argument => argument.Replace(MailPlaceholder, "<mail.txt>", StringComparison.Ordinal))],
            mailCase.Scheme,
            [.. mailCase.SurlOptions.Select(option => option.Replace(AccountsPlaceholder, "<accounts>", StringComparison.Ordinal))],
            new Dictionary<string, byte[]>(),
            _ => null)
        {
            AssertedStandardErrorFragments = mailCase.AssertedStandardErrorFragments,
            ComparedStoredFiles = isLast ? [.. upstreamStored.Keys.Union(portStored.Keys)] : [],
            RecordsClientBytes = mailCase.RecordsClientBytes,
        };

        var upstreamStep = upstream.Steps[step];
        var portStep = port.Steps[step];
        var upstreamResult = upstreamStep.Result.ExitCode is { } upstreamExit
            ? UpstreamCurlRunResult.Exited(upstreamStep.Result.Build, upstreamExit, NormaliseOutput(upstreamStep.Result.StandardOutput, upstream.UidValidity), NormaliseServerChosenValues(upstreamStep.Result.StandardError))
            : UpstreamCurlRunResult.StoppedAtTimeout(upstreamStep.Result.Build, NormaliseOutput(upstreamStep.Result.StandardOutput, upstream.UidValidity), NormaliseServerChosenValues(upstreamStep.Result.StandardError));
        var portResult = portStep.Result.ExitCode is { } portExit
            ? CurlPortClientRunResult.Exited(portStep.Result.Client, portExit, NormaliseOutput(portStep.Result.StandardOutput, port.UidValidity), NormaliseServerChosenValues(portStep.Result.StandardError))
            : CurlPortClientRunResult.StoppedAtTimeout(portStep.Result.Client, NormaliseOutput(portStep.Result.StandardOutput, port.UidValidity), NormaliseServerChosenValues(portStep.Result.StandardError));

        return CurlPortComparison.Compare(
            stepCase,
            upstreamResult,
            NormaliseSide(upstreamStep.Surl, isLast ? upstreamStored : null, isLast ? upstream.Notes : string.Empty),
            portResult,
            NormaliseSide(portStep.Surl, isLast ? portStored : null, isLast ? port.Notes : string.Empty),
            surlCommit);
    }

    private static Dictionary<string, byte[]> NormaliseInbox<TResult>(ClientRun<TResult> run)
    {
        var stored = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        for (var index = 0; index < run.Inbox.Count; index++)
        {
            stored[$"INBOX message {index + 1}"] = NormaliseOutput(run.Inbox[index], null);
        }

        return stored;
    }

    private static CurlPortSurlSide NormaliseSide(CurlPortSurlSide side, IReadOnlyDictionary<string, byte[]>? stored, string notes) =>
        new(side.Port, notes, stored ?? new Dictionary<string, byte[]>())
        {
            RelayPort = side.RelayPort,
            ClientRequestBytes = side.ClientRequestBytes is null ? null : [.. side.ClientRequestBytes.Select(NormaliseServerChosenDigests)],
        };

    // The trace field's date and the mailbox's UIDVALIDITY are chosen per run by the clock and surl.
    private static byte[] NormaliseOutput(byte[] output, uint? uidValidity)
    {
        var text = Encoding.Latin1.GetString(output);
        text = TraceDate.Replace(text, "; <date>\r\n");
        text = UidValidityValue.Replace(text, "${1}<uidvalidity>");
        if (uidValidity is { } value)
        {
            text = text.Replace($" {value}.", " <uidvalidity>.", StringComparison.Ordinal);
        }

        return Encoding.Latin1.GetBytes(text);
    }

    private static byte[] NormaliseServerChosenDigests(byte[] requestBytes) =>
        Encoding.Latin1.GetBytes(NormaliseServerChosenValues(Encoding.Latin1.GetString(requestBytes)));

    // Values surl or the port it listens on decide afresh every run, in what a client sends or echoes to
    // standard error: CRAM-MD5's and APOP's digests (keyed by surl's random challenge), DIGEST-MD5's
    // server nonce, and the port OAUTHBEARER's GS2 header names, each inside a base64 SASL token.
    private static string NormaliseServerChosenValues(string text)
    {
        text = ApopDigest.Replace(text, "${1}<apop-digest>");
        return Base64Token.Replace(text, token =>
        {
            var decoded = new byte[token.Length];
            if (!Convert.TryFromBase64String(token.Value, decoded, out var length))
            {
                return token.Value;
            }

            var plain = Encoding.Latin1.GetString(decoded, 0, length);
            var response = CramMd5Response.Match(plain);
            var normalised = response.Success
                ? $"{response.Groups[1].Value} <cram-md5-digest>"
                : Gs2Port.Replace(DigestServerNonce.Replace(plain, "${1}<nonce>"), "${1}<port>");
            return normalised == plain ? token.Value : Convert.ToBase64String(Encoding.Latin1.GetBytes(normalised));
        });
    }

    private async Task<ClientRun<TResult>> RunClientAsync<TResult>(
        MailCase mailCase,
        MailRunContext context,
        Func<IReadOnlyList<string>, IReadOnlyDictionary<string, string>, Task<TResult>> runClientAsync)
    {
        var cancellationToken = TestContext.CancellationToken;
        var dataDirectory = NewTemporaryDirectory();
        var home = NewTemporaryDirectory();
        var environment = new Dictionary<string, string> { ["HOME"] = home, ["CURL_HOME"] = home, ["USERPROFILE"] = home };
        var options = mailCase.SurlOptions.Select(option => option.Replace(AccountsPlaceholder, context.AccountsPath, StringComparison.Ordinal)).ToArray();
        if (mailCase.Seed == MailSeed.BeforeStartAsTester)
        {
            await using var seeder = await SurlOnLoopback.StartOverDirectoryAsync(
                ["smtp", "pop3"], dataDirectory, ["--user-file", context.AccountsPath], cancellationToken);
            await SeedOverSmtpAsync(seeder, context, Credentials);
        }

        var steps = new List<ClientStep<TResult>>();
        string notes;
        await using (var surl = await SurlOnLoopback.StartOverDirectoryAsync(mailCase.SurlSchemes, dataDirectory, options, cancellationToken))
        {
            if (mailCase.Seed is MailSeed.OverSmtpAsTester or MailSeed.OverSmtpAnonymously)
            {
                await SeedOverSmtpAsync(surl, context, mailCase.Seed == MailSeed.OverSmtpAsTester ? Credentials : null);
            }

            var listener = surl.BaseUrlOf(mailCase.Scheme);
            using var holder = await HoldAsync(mailCase.Hold, listener);
            foreach (var arguments in mailCase.Steps)
            {
                steps.Add(await RunStepAsync(mailCase, listener, arguments, context, environment, runClientAsync));
            }

            notes = surl.Log;
        }

        var owner = mailCase.Inbox == InboxOwner.Tester ? AccountsFile.User : null;
        return mailCase.Inbox == InboxOwner.None
            ? new ClientRun<TResult>(steps, [], null, notes)
            : new ClientRun<TResult>(
                steps,
                await StoredMail.ReadInboxAsync(dataDirectory, owner, cancellationToken),
                await StoredMail.ReadInboxUidValidityAsync(dataDirectory, owner, cancellationToken),
                notes);
    }

    private async Task<ClientStep<TResult>> RunStepAsync<TResult>(
        MailCase mailCase,
        Uri listener,
        IReadOnlyList<string> arguments,
        MailRunContext context,
        IReadOnlyDictionary<string, string> environment,
        Func<IReadOnlyList<string>, IReadOnlyDictionary<string, string>, Task<TResult>> runClientAsync)
    {
        var cancellationToken = TestContext.CancellationToken;
        await using var relay = mailCase.RecordsClientBytes ? ClientBytesRelay.Start(listener, cancellationToken) : null;
        var baseUrl = relay?.BaseUrlFor(listener) ?? listener;
        var filled = arguments
            .Select(argument => argument
                .Replace(SurlBase, baseUrl.AbsoluteUri, StringComparison.Ordinal)
                .Replace(MailPlaceholder, context.MailPath, StringComparison.Ordinal))
            .ToArray();
        var result = await runClientAsync(filled, environment);
        TestContext.WriteLine($"{string.Join(' ', arguments)}: {result}");
        var side = new CurlPortSurlSide(listener.Port, string.Empty, new Dictionary<string, byte[]>());
        if (relay is not null)
        {
            await relay.WaitForConnectionsAsync(RelayDrainTimeout, cancellationToken);
            side = side with { RelayPort = relay.Port, ClientRequestBytes = relay.Transcript.ToConnections() };
        }

        return new ClientStep<TResult>(result, side);
    }

    // The seed is the case's content, delivered by upstream curl for both runs as the mirrored class
    // delivers it: logged in as tester when credentials are given, else anonymously, to a URL ending
    // /c so the trace field names EHLO c.
    private async Task SeedOverSmtpAsync(SurlOnLoopback surl, MailRunContext context, string? credentials)
    {
        var smtp = surl.BaseUrlOf("smtp");
        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext,
            [
                "-sS", .. credentials is null ? (string[])[] : ["-u", credentials],
                "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", context.SeedMailPath, $"smtp://{smtp.Host}:{smtp.Port}/c",
            ]);
        Assert.AreEqual(0, result.ExitCode, $"The SMTP delivery that seeds the case failed: {result.StandardError}");
    }

    private async Task<IDisposable?> HoldAsync(MailHold hold, Uri listener)
    {
        if (hold == MailHold.None)
        {
            return null;
        }

        var cancellationToken = TestContext.CancellationToken;
        var holder = new TcpClient();
        await holder.ConnectAsync(listener.Host, listener.Port, cancellationToken);
        var reader = new StreamReader(holder.GetStream(), Encoding.ASCII);
        StringAssert.StartsWith(await reader.ReadLineAsync(cancellationToken), hold == MailHold.Pop3Login ? "+OK" : string.Empty);
        if (hold == MailHold.Pop3Login)
        {
            await holder.GetStream().WriteAsync(Encoding.ASCII.GetBytes($"USER {AccountsFile.User}\r\n"), cancellationToken);
            StringAssert.StartsWith(await reader.ReadLineAsync(cancellationToken), "+OK");
            await holder.GetStream().WriteAsync(Encoding.ASCII.GetBytes($"PASS {AccountsFile.Password}\r\n"), cancellationToken);
            Assert.AreEqual("+OK Logged in", await reader.ReadLineAsync(cancellationToken));
        }

        return holder;
    }

    // ADR-0089 decision 5: the findings file's one table, whose first column is the Phase 7 test's name.
    private static async Task<IReadOnlySet<string>> ReadListedFindingsAsync(string root, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(Path.Combine(root, FindingsFile), cancellationToken);
        return lines
            .Where(line => line.StartsWith("| `", StringComparison.Ordinal))
            .Select(line => line.Split('|')[1].Trim().Trim('`'))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<string> SurlCommitAsync(string root, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "-C", root, "rev-parse", "HEAD" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var git = Process.Start(startInfo)!;
            var commit = (await git.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            await git.WaitForExitAsync(cancellationToken);
            return git.ExitCode == 0 && commit.Length > 0 ? commit : "unknown (git rev-parse HEAD failed)";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "unknown (git is not on PATH)";
        }
    }

    private string NewTemporaryDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-phase7-mail-").FullName;
        temporaryDirectories.Add(directory);
        return directory;
    }

    private async Task<string> WriteMailFileAsync(byte[] contents)
    {
        var path = Path.Combine(NewTemporaryDirectory(), "mail.txt");
        await File.WriteAllBytesAsync(path, contents, TestContext.CancellationToken);
        return path;
    }

    /// <summary>One mirrored conversation: the surl it runs against, its seed, and one or more client runs.</summary>
    private sealed record MailCase(
        string Name,
        string Mirrors,
        string Scheme,
        IReadOnlyList<string> SurlSchemes,
        IReadOnlyList<string> SurlOptions,
        IReadOnlyList<IReadOnlyList<string>> Steps,
        Action<MailOutcome> CheckMirroredAssertions)
    {
        public MailSeed Seed { get; init; } = MailSeed.None;

        public InboxOwner Inbox { get; init; } = InboxOwner.None;

        public MailHold Hold { get; init; } = MailHold.None;

        public byte[] MailFile { get; init; } = [];

        public IReadOnlyList<string> AssertedStandardErrorFragments { get; init; } = [];

        // Plaintext, unless the conversation upgrades with STARTTLS or STLS: after it the bytes are the TLS library's.
        public bool RecordsClientBytes =>
            CurlPortMeasurementCase.PlaintextTcpSchemes.Contains(Scheme)
            && !(SurlOptions.Contains("--self-signed") && Steps.Any(step => step.Contains("--ssl-reqd")));
    }

    /// <summary>The files one case's two runs share, read-only.</summary>
    private sealed record MailRunContext(string AccountsPath, string MailPath, string SeedMailPath);

    /// <summary>One client run and what its surl and relay saw of it.</summary>
    private sealed record ClientStep<TResult>(TResult Result, CurlPortSurlSide Surl);

    /// <summary>One client's whole case: each run, then the inbox and UIDVALIDITY the stopped surl left.</summary>
    private sealed record ClientRun<TResult>(IReadOnlyList<ClientStep<TResult>> Steps, IReadOnlyList<byte[]> Inbox, uint? UidValidity, string Notes);

    /// <summary>Upstream's run as the mirrored test's assertions see it.</summary>
    private sealed record MailOutcome(IReadOnlyList<CurlPortMeasuredRun> Steps, IReadOnlyList<byte[]> Inbox, uint? UidValidity)
    {
        public CurlPortMeasuredRun Last => Steps[^1];
    }
}
