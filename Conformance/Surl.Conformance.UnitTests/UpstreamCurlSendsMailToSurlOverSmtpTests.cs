using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build sends mail to a live, in-process <c>surl</c> over <c>smtp</c>
/// and <c>smtps</c>, and is answered as ADR-0053 decision 10 lists: one and two recipients, a
/// refused recipient with and without <c>--mail-rcpt-allowfails</c>, <c>STARTTLS</c> and implicit
/// TLS with <c>--self-signed</c>, a login with each SASL mechanism ADR-0049 offers, a plain-text
/// login refused without TLS and accepted with <c>--allow-plaintext-auth</c>, a message past
/// <c>--max-filesize</c>, <c>VRFY</c>, <c>EXPN</c>, <c>HELP</c>, <c>NOOP</c>, bare-LF bodies
/// and <c>--max-connections</c>. Every delivered message is read back from the stopped surl's
/// <c>--directory</c> through the persisted mail store (ADR-0050, decision 7) and must be
/// ADR-0053 decision 6's trace fields followed by the body curl sent, dot-unstuffed.
/// Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlSendsMailToSurlOverSmtpTests
{
    // CURLE_WEIRD_SERVER_REPLY: a 421 in place of the greeting (ADR-0053 row 20).
    private const int WeirdServerReply = 8;

    // CURLE_SEND_ERROR: MAIL, RCPT or DATA refused (ADR-0053 rows 6, 10, 11, 16).
    private const int SendError = 55;

    // CURLE_USE_SSL_FAILED: --ssl-reqd and no STARTTLS (ADR-0053 row 43).
    private const int UseSslFailed = 64;

    // CURLE_LOGIN_DENIED: any SASL refusal (ADR-0049 section 7).
    private const int LoginDenied = 67;

    private const string Recipient = "tester@example.com";

    // ADR-0053's mail.txt: 53 bytes, every line ending CRLF, with a line starting with a dot.
    private static readonly byte[] Mail = "From: a@x\r\nTo: b@y\r\nSubject: hi\r\n\r\nhello\r\n.dot line\r\n"u8.ToArray();

    // The same message with bare LF line ends.
    private static readonly byte[] MailWithBareLineFeeds = "From: a@x\nTo: b@y\nSubject: hi\n\nhello\n.dot line\n"u8.ToArray();

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    private readonly List<string> temporaryDirectories = [];

    public TestContext TestContext { get; set; } = null!;

    [TestCleanup]
    public void DeleteTemporaryDirectories()
    {
        foreach (var directory in temporaryDirectories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Send_AllowAnonymous_StoresTheMessageInTheAnonymousInbox()
    {
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--allow-anonymous"))
        {
            result = await RunUpstreamCurlAsync("-sS", "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
        var stored = await ReadInboxAsync(dataDirectory, null);
        Assert.HasCount(1, stored);
        AssertStored(stored[0], "a@x", "ESMTP", Mail);
    }

    [TestMethod]
    public async Task Send_AccountButNoLogin_ExitsSendErrorAndStoresNothing()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--user-file", accounts.Path))
        {
            result = await RunUpstreamCurlAsync("-sS", "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(SendError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "MAIL failed: 530");
        Assert.IsEmpty(await ReadInboxAsync(dataDirectory, AccountsFile.User));
    }

    [TestMethod]
    public async Task Send_AccountLoggedIn_LogsInWithCramMd5AndStoresTheMessageInTheAccountsInbox()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--user-file", accounts.Path))
        {
            result = await RunUpstreamCurlAsync(
                "-sS", "-v", "-u", Credentials, "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "AUTH CRAM-MD5");
        var stored = await ReadInboxAsync(dataDirectory, AccountsFile.User);
        Assert.HasCount(1, stored);
        AssertStored(stored[0], "a@x", "ESMTPA", Mail);
    }

    [TestMethod]
    public async Task Send_WrongPassword_ExitsLoginDeniedAndStoresNothing()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--user-file", accounts.Path))
        {
            result = await RunUpstreamCurlAsync(
                "-sS", "-u", $"{AccountsFile.User}:wrong", "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.IsEmpty(await ReadInboxAsync(dataDirectory, AccountsFile.User));
    }

    [TestMethod]
    public async Task Send_TwoRecipientsAllowAnonymous_StoresOneCopyForEach()
    {
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--allow-anonymous"))
        {
            result = await RunUpstreamCurlAsync(
                "-sS", "--mail-from", "a@x", "--mail-rcpt", "b@y", "--mail-rcpt", "c@z", "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var stored = await ReadInboxAsync(dataDirectory, null);
        Assert.HasCount(2, stored);
        AssertStored(stored[0], "a@x", "ESMTP", Mail);
        AssertStored(stored[1], "a@x", "ESMTP", Mail);
    }

    [TestMethod]
    public async Task Send_AnAccountAndAnUnknownRecipient_StoresOneCopyAndDiscardsTheOther()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--user-file", accounts.Path))
        {
            result = await RunUpstreamCurlAsync(
                "-sS", "-u", Credentials, "--mail-from", "a@x", "--mail-rcpt", "tester@x", "--mail-rcpt", "nobody@x", "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var stored = await ReadInboxAsync(dataDirectory, AccountsFile.User);
        Assert.HasCount(1, stored);
        AssertStored(stored[0], "a@x", "ESMTPA", Mail);
    }

    [TestMethod]
    public async Task Send_AnInvalidRecipient_ExitsSendErrorAndStoresNothing()
    {
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--allow-anonymous"))
        {
            result = await RunUpstreamCurlAsync(
                "-sS", "--mail-from", "a@x", "--mail-rcpt", "b@y", "--mail-rcpt", "Bob <c@z>", "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(SendError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "RCPT failed: 501");
        Assert.IsEmpty(await ReadInboxAsync(dataDirectory, null));
    }

    [TestMethod]
    public async Task Send_AnInvalidRecipientWithMailRcptAllowfails_StoresOneCopy()
    {
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--allow-anonymous"))
        {
            result = await RunUpstreamCurlAsync(
                "-sS", "--mail-rcpt-allowfails", "--mail-from", "a@x", "--mail-rcpt", "b@y", "--mail-rcpt", "Bob <c@z>", "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var stored = await ReadInboxAsync(dataDirectory, null);
        Assert.HasCount(1, stored);
        AssertStored(stored[0], "a@x", "ESMTP", Mail);
    }

    [TestMethod]
    public async Task Send_SslReqdWithSelfSigned_UpgradesWithStartTlsAndStoresTheMessage()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--self-signed", "--user-file", accounts.Path))
        {
            result = await RunUpstreamCurlAsync(
                "-sS", "-k", "--ssl-reqd", "-u", Credentials, "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var stored = await ReadInboxAsync(dataDirectory, AccountsFile.User);
        Assert.HasCount(1, stored);
        AssertStored(stored[0], "a@x", "ESMTPSA", Mail);
    }

    [TestMethod]
    public async Task Send_SslReqdWithoutACertificate_ExitsUseSslFailed()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--user-file", accounts.Path))
        {
            result = await RunUpstreamCurlAsync(
                "-sS", "--ssl-reqd", "-u", Credentials, "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(UseSslFailed, result.ExitCode, result.StandardError);
        Assert.IsEmpty(await ReadInboxAsync(dataDirectory, AccountsFile.User));
    }

    [TestMethod]
    public async Task Send_SmtpsWithSelfSigned_StoresTheMessage()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtps", dataDirectory, "--self-signed", "--user-file", accounts.Path))
        {
            result = await RunUpstreamCurlAsync(
                "-sS", "-k", "-u", Credentials, "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var stored = await ReadInboxAsync(dataDirectory, AccountsFile.User);
        Assert.HasCount(1, stored);
        AssertStored(stored[0], "a@x", "ESMTPSA", Mail);
    }

    [TestMethod]
    [DataRow("CRAM-MD5", false, "ESMTPA", DisplayName = "CRAM-MD5 over smtp")]
    [DataRow("CRAM-MD5", true, "ESMTPA", DisplayName = "CRAM-MD5 over smtp with --sasl-ir")]
    [DataRow("PLAIN", false, "ESMTPSA", DisplayName = "PLAIN after STARTTLS")]
    [DataRow("PLAIN", true, "ESMTPSA", DisplayName = "PLAIN after STARTTLS with --sasl-ir")]
    [DataRow("LOGIN", false, "ESMTPSA", DisplayName = "LOGIN after STARTTLS")]
    [DataRow("LOGIN", true, "ESMTPSA", DisplayName = "LOGIN after STARTTLS with --sasl-ir")]
    public async Task Send_ForcedPasswordMechanism_LogsInAndStoresTheMessage(string mechanism, bool initialResponse, string protocol)
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var overTls = protocol == "ESMTPSA";
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, overTls ? ["--self-signed", "--user-file", accounts.Path] : ["--user-file", accounts.Path]))
        {
            result = await RunUpstreamCurlAsync(
                [
                    "-sS", "-v", .. overTls ? (string[])["-k", "--ssl-reqd"] : [], .. initialResponse ? (string[])["--sasl-ir"] : [],
                    "-u", Credentials, "--login-options", $"AUTH={mechanism}",
                    "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl),
                ]);
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"AUTH {mechanism}");
        var stored = await ReadInboxAsync(dataDirectory, AccountsFile.User);
        Assert.HasCount(1, stored);
        AssertStored(stored[0], "a@x", protocol, Mail);
    }

    [TestMethod]
    [DataRow("XOAUTH2", false)]
    [DataRow("XOAUTH2", true)]
    [DataRow("OAUTHBEARER", false)]
    [DataRow("OAUTHBEARER", true)]
    public async Task Send_ForcedBearerMechanismAfterStartTls_LogsInAsTheNamedAccountAndStoresTheMessage(string mechanism, bool initialResponse)
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--self-signed", "--user-file", accounts.Path))
        {
            result = await RunUpstreamCurlAsync(
                [
                    "-sS", "-v", "-k", "--ssl-reqd", .. initialResponse ? (string[])["--sasl-ir"] : [],
                    "-u", $"{AccountsFile.User}:", "--oauth2-bearer", AccountsFile.BearerToken, "--login-options", $"AUTH={mechanism}",
                    "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl),
                ]);
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"AUTH {mechanism}");
        var stored = await ReadInboxAsync(dataDirectory, AccountsFile.User);
        Assert.HasCount(1, stored);
        AssertStored(stored[0], "a@x", "ESMTPSA", Mail);
    }

    [TestMethod]
    [DataRow("DIGEST-MD5", "digest-md5", false)]
    [DataRow("DIGEST-MD5", "digest-md5", true)]
    [DataRow("NTLM", "ntlm", false)]
    [DataRow("NTLM", "ntlm", true)]
    public async Task Send_ForcedNamedChoiceMechanism_LogsInAndStoresTheMessage(string mechanism, string authWord, bool initialResponse)
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--auth", authWord, "--user-file", accounts.Path))
        {
            result = await RunUpstreamCurlAsync(
                [
                    "-sS", "-v", .. initialResponse ? (string[])["--sasl-ir"] : [],
                    "-u", Credentials, "--login-options", $"AUTH={mechanism}",
                    "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl),
                ]);
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"AUTH {mechanism}");
        var stored = await ReadInboxAsync(dataDirectory, AccountsFile.User);
        Assert.HasCount(1, stored);
        AssertStored(stored[0], "a@x", "ESMTPA", Mail);
    }

    [TestMethod]
    public async Task Send_PlainOverSmtpWithoutTls_ExitsLoginDenied()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--user-file", accounts.Path))
        {
            result = await RunUpstreamCurlAsync(
                "-sS", "-u", Credentials, "--login-options", "AUTH=PLAIN", "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.IsEmpty(await ReadInboxAsync(dataDirectory, AccountsFile.User));
    }

    [TestMethod]
    public async Task Send_PlainOverSmtpWithAllowPlaintextAuth_StoresTheMessage()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--allow-plaintext-auth", "--user-file", accounts.Path))
        {
            result = await RunUpstreamCurlAsync(
                "-sS", "-u", Credentials, "--login-options", "AUTH=PLAIN", "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var stored = await ReadInboxAsync(dataDirectory, AccountsFile.User);
        Assert.HasCount(1, stored);
        AssertStored(stored[0], "a@x", "ESMTPA", Mail);
    }

    [TestMethod]
    public async Task Send_MessageOverAMebibyteWithMaxFilesize1000_ExitsSendErrorAndStoresNothing()
    {
        var dataDirectory = NewTemporaryDirectory();
        var big = await WriteMailFileAsync([.. Mail, .. Enumerable.Repeat((byte)'x', 1_100_000), .. "\r\n"u8]);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--allow-anonymous", "--max-filesize", "1000"))
        {
            result = await RunUpstreamCurlAsync("-sS", "--mail-from", "a@x", "--mail-rcpt", "b@y", "-T", big, MailUrl(surl));
        }

        Assert.AreEqual(SendError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "MAIL failed: 552");
        Assert.IsEmpty(await ReadInboxAsync(dataDirectory, null));
    }

    [TestMethod]
    public async Task Send_MaxFilesize10_ExitsSendErrorAndStoresNothing()
    {
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(Mail);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--allow-anonymous", "--max-filesize", "10"))
        {
            result = await RunUpstreamCurlAsync("-sS", "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(SendError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "MAIL failed: 552");
        Assert.IsEmpty(await ReadInboxAsync(dataDirectory, null));
    }

    [TestMethod]
    [DataRow("VRFY", "b@y", "252 2.1.5 Cannot verify the user, but will accept the message\r\n")]
    [DataRow("EXPN", "list", "252 2.1.5 Cannot expand the list, but will accept the message\r\n")]
    public async Task Command_WithMailRcpt_WritesTheReplyToStandardOutput(string command, string argument, string reply)
    {
        await using var surl = await StartSurlAsync("smtp", NewTemporaryDirectory(), "--allow-anonymous");

        var result = await RunUpstreamCurlAsync("-sS", "--mail-rcpt", argument, "-X", command, MailUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(reply, Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task NothingToSend_SendsHelp_WritesTheHelpReplyToStandardOutput()
    {
        await using var surl = await StartSurlAsync("smtp", NewTemporaryDirectory(), "--allow-anonymous");

        var result = await RunUpstreamCurlAsync("-sS", MailUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(
            "214 2.0.0 Commands: EHLO HELO STARTTLS AUTH MAIL RCPT DATA RSET NOOP VRFY EXPN HELP QUIT\r\n",
            Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Noop_WritesTheReplyToStandardOutput()
    {
        await using var surl = await StartSurlAsync("smtp", NewTemporaryDirectory(), "--allow-anonymous");

        var result = await RunUpstreamCurlAsync("-sS", "-X", "NOOP", MailUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("250 2.0.0 OK\r\n", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Send_BareLineFeedsWithCrlf_StoresTheMessageWithCrlfLineEnds()
    {
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(MailWithBareLineFeeds);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--allow-anonymous"))
        {
            result = await RunUpstreamCurlAsync("-sS", "--crlf", "--mail-from", "a@x", "--mail-rcpt", "b@y", "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var stored = await ReadInboxAsync(dataDirectory, null);
        Assert.HasCount(1, stored);
        AssertStored(stored[0], "a@x", "ESMTP", Mail);
    }

    [TestMethod]
    public async Task Send_BareLineFeeds_StoresThemAsSentWithACrlfBeforeTheEnd()
    {
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync(MailWithBareLineFeeds);
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync("smtp", dataDirectory, "--allow-anonymous"))
        {
            result = await RunUpstreamCurlAsync("-sS", "--mail-from", "a@x", "--mail-rcpt", "b@y", "-T", mail, MailUrl(surl));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var stored = await ReadInboxAsync(dataDirectory, null);
        Assert.HasCount(1, stored);

        // ADR-0053 row 38: curl stuffs no dot after a bare LF and ends the body with CRLF . CRLF.
        AssertStored(stored[0], "a@x", "ESMTP", [.. MailWithBareLineFeeds, .. "\r\n"u8]);
    }

    [TestMethod]
    public async Task Send_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply()
    {
        var mail = await WriteMailFileAsync(Mail);
        await using var surl = await StartSurlAsync("smtp", NewTemporaryDirectory(), "--allow-anonymous", "--max-connections", "1");
        using var holder = new TcpClient();
        await holder.ConnectAsync(surl.BaseUrl.Host, surl.BaseUrl.Port, TestContext.CancellationToken);
        var greeting = new byte[5];
        await holder.GetStream().ReadExactlyAsync(greeting, TestContext.CancellationToken);
        Assert.AreEqual("220 s", Encoding.ASCII.GetString(greeting));

        var result = await RunUpstreamCurlAsync("-sS", "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, MailUrl(surl));

        Assert.AreEqual(WeirdServerReply, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "421");
    }

    // Decision 6: Return-Path, then Received naming the EHLO domain "c", the loopback address,
    // RFC 3848's protocol word and an RFC 5322 date, then the body as sent, dot-unstuffed.
    private static void AssertStored(byte[] stored, string reversePath, string protocol, byte[] body)
    {
        var text = Encoding.UTF8.GetString(stored);
        var traceFields = Regex.Match(
            text,
            $@"\AReturn-Path: <{Regex.Escape(reversePath)}>\r\nReceived: from c \(\[127\.0\.0\.1\]\) by surl with {protocol}; [A-Z][a-z]{{2}}, \d{{2}} [A-Z][a-z]{{2}} \d{{4}} \d{{2}}:\d{{2}}:\d{{2}} \+0000\r\n",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        Assert.IsTrue(traceFields.Success, text);
        CollectionAssert.AreEqual(body, stored[traceFields.Length..]);
    }

    // Every URL ends /c so curl sends EHLO c (ADR-0053 decision 10).
    private static string MailUrl(SurlOnLoopback surl) =>
        $"{surl.BaseUrl.Scheme}://{surl.BaseUrl.Host}:{surl.BaseUrl.Port}/c";

    private string NewTemporaryDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-conformance-mail-").FullName;
        temporaryDirectories.Add(directory);
        return directory;
    }

    private async Task<string> WriteMailFileAsync(byte[] contents)
    {
        var path = Path.Combine(NewTemporaryDirectory(), "mail.txt");
        await File.WriteAllBytesAsync(path, contents, TestContext.CancellationToken);
        return path;
    }

    private Task<SurlOnLoopback> StartSurlAsync(string scheme, string dataDirectory, params string[] options) =>
        SurlOnLoopback.StartOverDirectoryAsync(scheme, dataDirectory, options, TestContext.CancellationToken);

    private Task<IReadOnlyList<byte[]>> ReadInboxAsync(string dataDirectory, string? accountName) =>
        StoredMail.ReadInboxAsync(dataDirectory, accountName, TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunUpstreamCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunAsync(TestContext, arguments);
}
