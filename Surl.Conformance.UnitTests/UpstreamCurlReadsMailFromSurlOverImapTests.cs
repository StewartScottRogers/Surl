using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build reads, searches, appends and manages mail on a live,
/// in-process <c>surl</c> over <c>imap</c> and <c>imaps</c>, and is answered as ADR-0055
/// decision 15 lists. Every case starts one surl listening on <c>smtp</c> beside <c>imap</c> or
/// <c>imaps</c> over a fresh <c>--directory</c>, and first has curl deliver ADR-0055's
/// <c>mail.txt</c> over <c>smtp</c> (BL-210's case), so <c>INBOX</c> holds one message, UID 1;
/// every fetch of it must then be, byte for byte, the message the stopped surl's persisted mail
/// store holds (ADR-0050, decision 7): ADR-0053 decision 6's trace fields followed by
/// <c>mail.txt</c>. Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlReadsMailFromSurlOverImapTests
{
    // CURLE_WEIRD_SERVER_REPLY: a BYE in place of the greeting (ADR-0055 decision 12, row 49).
    private const int WeirdServerReply = 8;

    // CURLE_QUOTE_ERROR: a -X command answered NO or BAD (rows 30 and 37).
    private const int QuoteError = 21;

    // CURLE_UPLOAD_FAILED: APPEND refused before the + continuation (row 28).
    private const int UploadFailed = 25;

    // CURLE_USE_SSL_FAILED: --ssl-reqd and no STARTTLS (rows 45 and 47).
    private const int UseSslFailed = 64;

    // CURLE_LOGIN_DENIED: a refused login, a login not offered, or a failed SELECT (rows 19, 42, 43).
    private const int LoginDenied = 67;

    // CURLE_REMOTE_FILE_NOT_FOUND: no such UID, or UIDVALIDITY changed (rows 12 and 17).
    private const int RemoteFileNotFound = 78;

    private const string Recipient = "tester@example.com";

    // ADR-0055's mail.txt: 33 bytes, every line ending CRLF.
    private static readonly byte[] Mail = "From: a@x\r\nSubject: hi\r\n\r\nhello\r\n"u8.ToArray();

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
    public async Task List_Root_WritesTheInboxListLine()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "{imap}/");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("* LIST (\\HasNoChildren) \"/\" INBOX\r\n", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task FetchByUid_MailSentOverSmtp_WritesTheStoredMessageByteForByteAfterACramMd5Login()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync(dataDirectory, "imap", "--user-file", accounts.Path))
        {
            await SendMailOverSmtpAsync(surl, Credentials);
            result = await RunUpstreamCurlAsync("-sS", "-v", "-u", Credentials, ImapUrl(surl, "/INBOX;UID=1"));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "AUTHENTICATE CRAM-MD5");
        await AssertIsTheDeliveredMessageAsync(result.StandardOutput, dataDirectory, AccountsFile.User, "ESMTPA");
    }

    [TestMethod]
    public async Task FetchByMailIndex_WritesTheStoredMessage()
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync([], "-sS", "-u", Credentials, "{imap}/INBOX;MAILINDEX=1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    public async Task FetchSectionText_WritesTheBody()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "{imap}/INBOX;UID=1;SECTION=TEXT");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("hello\r\n", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task FetchSectionHeaderFields_WritesTheSubjectField()
    {
        var result = await RunAgainstSeededSurlAsync(
            [], "-sS", "-u", Credentials, "{imap}/INBOX;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("Subject: hi\r\n\r\n", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task FetchPartial_WritesTheFirstTenBytesOfTheStoredMessage()
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync([], "-sS", "-u", Credentials, "{imap}/INBOX;UID=1;PARTIAL=0.10");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.HasCount(1, stored);
        CollectionAssert.AreEqual(stored[0][..10], result.StandardOutput);
    }

    [TestMethod]
    public async Task FetchWithAStaleUidValidity_ExitsRemoteFileNotFound()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "{imap}/INBOX;UIDVALIDITY=1/;UID=1");

        Assert.AreEqual(RemoteFileNotFound, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Mailbox UIDVALIDITY has changed");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task FetchAUidNoMessageHas_ExitsRemoteFileNotFound()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "{imap}/INBOX;UID=9");

        Assert.AreEqual(RemoteFileNotFound, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task FetchFromAMailboxThatDoesNotExist_ExitsLoginDenied()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "{imap}/Nope;UID=1");

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Select failed");
    }

    [TestMethod]
    [DataRow("SUBJECT%20hi", "* SEARCH 1\r\n", DisplayName = "a match")]
    [DataRow("SUBJECT%20nothing", "* SEARCH\r\n", DisplayName = "no match")]
    public async Task Search_WritesTheSearchLine(string query, string expected)
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, $"{{imap}}/INBOX?{query}");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(expected, Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Append_ThenFetchTheNewUid_WritesTheAppendedBytesWithNoTraceFields()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var mail = await WriteMailFileAsync();
        await using var surl = await StartSurlAsync(NewTemporaryDirectory(), "imap", "--user-file", accounts.Path);
        await SendMailOverSmtpAsync(surl, Credentials);

        var append = await RunUpstreamCurlAsync("-sS", "-u", Credentials, "-T", mail, ImapUrl(surl, "/INBOX"));
        var fetch = await RunUpstreamCurlAsync("-sS", "-u", Credentials, ImapUrl(surl, "/INBOX;UID=2"));

        Assert.AreEqual(0, append.ExitCode, append.StandardError);
        Assert.IsEmpty(append.StandardOutput);
        Assert.AreEqual(0, fetch.ExitCode, fetch.StandardError);
        CollectionAssert.AreEqual(Mail, fetch.StandardOutput);
    }

    [TestMethod]
    public async Task Append_PastMaxFilesize_ExitsUploadFailedAndStoresNothing()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        var mail = await WriteMailFileAsync();
        UpstreamCurlRunResult result;

        // No SMTP delivery first: the same --max-filesize would refuse it too.
        await using (var surl = await StartSurlAsync(dataDirectory, "imap", "--user-file", accounts.Path, "--max-filesize", "10"))
        {
            result = await RunUpstreamCurlAsync("-sS", "-u", Credentials, "-T", mail, ImapUrl(surl, "/INBOX"));
        }

        Assert.AreEqual(UploadFailed, result.ExitCode, result.StandardError);
        Assert.IsEmpty(await ReadInboxAsync(dataDirectory, AccountsFile.User));
    }

    [TestMethod]
    public async Task Append_ToAMailboxThatDoesNotExist_ExitsUploadFailed()
    {
        var mail = await WriteMailFileAsync();

        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "-T", mail, "{imap}/Nope");

        Assert.AreEqual(UploadFailed, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task CustomCreate_ThenList_ListsInboxAndTheNewMailbox()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync(NewTemporaryDirectory(), "imap", "--user-file", accounts.Path);
        await SendMailOverSmtpAsync(surl, Credentials);

        var create = await RunUpstreamCurlAsync("-sS", "-u", Credentials, "-X", "CREATE Archive", ImapUrl(surl, "/"));
        var list = await RunUpstreamCurlAsync("-sS", "-u", Credentials, ImapUrl(surl, "/"));

        Assert.AreEqual(0, create.ExitCode, create.StandardError);
        Assert.IsEmpty(create.StandardOutput);
        Assert.AreEqual(0, list.ExitCode, list.StandardError);
        Assert.AreEqual(
            "* LIST (\\HasNoChildren) \"/\" INBOX\r\n* LIST (\\HasNoChildren) \"/\" Archive\r\n",
            Encoding.ASCII.GetString(list.StandardOutput));
    }

    [TestMethod]
    public async Task CustomExamine_WritesTheExamineUntaggedLines()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "-X", "EXAMINE INBOX", "{imap}/");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var text = Encoding.ASCII.GetString(result.StandardOutput);
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
    }

    [TestMethod]
    public async Task CustomStore_OnASelectedMailbox_WritesTheNewFlags()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "-X", "STORE 1 +FLAGS \\Deleted", "{imap}/INBOX");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("* 1 FETCH (FLAGS (\\Deleted))\r\n", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task CustomStore_ThenExpunge_WritesTheExpungeLine()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult store;
        UpstreamCurlRunResult expunge;
        await using (var surl = await StartSurlAsync(dataDirectory, "imap", "--user-file", accounts.Path))
        {
            await SendMailOverSmtpAsync(surl, Credentials);
            store = await RunUpstreamCurlAsync("-sS", "-u", Credentials, "-X", "STORE 1 +FLAGS \\Deleted", ImapUrl(surl, "/INBOX"));
            expunge = await RunUpstreamCurlAsync("-sS", "-u", Credentials, "-X", "EXPUNGE", ImapUrl(surl, "/INBOX"));
        }

        Assert.AreEqual(0, store.ExitCode, store.StandardError);
        Assert.AreEqual("* 1 FETCH (FLAGS (\\Deleted))\r\n", Encoding.ASCII.GetString(store.StandardOutput));
        Assert.AreEqual(0, expunge.ExitCode, expunge.StandardError);
        Assert.AreEqual("* 1 EXPUNGE\r\n", Encoding.ASCII.GetString(expunge.StandardOutput));
        Assert.IsEmpty(await ReadInboxAsync(dataDirectory, AccountsFile.User));
    }

    [TestMethod]
    public async Task CustomStatus_WritesTheStatusLine()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "-X", "STATUS INBOX (MESSAGES)", "{imap}/");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("* STATUS INBOX (MESSAGES 1)\r\n", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    [DataRow("STORE 1 +FLAGS (\\Seen)", DisplayName = "STORE with no mailbox selected")]
    [DataRow("XYZZY", DisplayName = "an unknown command")]
    public async Task CustomCommandRefused_ExitsQuoteError(string command)
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "-X", command, "{imap}/");

        Assert.AreEqual(QuoteError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Quote command returned error");
    }

    [TestMethod]
    public async Task Fetch_WrongPassword_ExitsLoginDenied()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", $"{AccountsFile.User}:wrong", "{imap}/INBOX;UID=1");

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Fetch_NoLoginWithoutAllowAnonymous_ExitsLoginDenied()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "{imap}/INBOX;UID=1");

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Fetch_NoLoginWithAllowAnonymous_WritesTheMessageSentAnonymouslyOverSmtp()
    {
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync(dataDirectory, "imap", "--allow-anonymous"))
        {
            await SendMailOverSmtpAsync(surl, null);
            result = await RunUpstreamCurlAsync("-sS", ImapUrl(surl, "/INBOX;UID=1"));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        await AssertIsTheDeliveredMessageAsync(result.StandardOutput, dataDirectory, null, "ESMTP");
    }

    [TestMethod]
    [DataRow("AUTH=PLAIN", DisplayName = "AUTHENTICATE PLAIN")]
    [DataRow("AUTH=+LOGIN", DisplayName = "the LOGIN command")]
    public async Task Fetch_ClearPasswordLoginOverPlaintext_ExitsLoginDenied(string loginOptions)
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "--login-options", loginOptions, "{imap}/INBOX;UID=1");

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    [DataRow("AUTH=PLAIN", "AUTHENTICATE PLAIN", DisplayName = "AUTHENTICATE PLAIN")]
    [DataRow("AUTH=LOGIN", "AUTHENTICATE LOGIN", DisplayName = "AUTHENTICATE LOGIN")]
    [DataRow("AUTH=+LOGIN", "LOGIN tester", DisplayName = "the LOGIN command")]
    public async Task Fetch_ClearPasswordLoginOverPlaintextWithAllowPlaintextAuth_WritesTheStoredMessage(string loginOptions, string sent)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--allow-plaintext-auth"], "-sS", "-v", "-u", Credentials, "--login-options", loginOptions, "{imap}/INBOX;UID=1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, sent);
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    public async Task Fetch_SslReqdWithSelfSigned_UpgradesWithStartTlsAndWritesTheStoredMessage()
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--self-signed"], "-sS", "-v", "-k", "--ssl-reqd", "-u", Credentials, "{imap}/INBOX;UID=1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "STARTTLS");
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    public async Task Fetch_SslReqdWithoutACertificate_ExitsUseSslFailed()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "--ssl-reqd", "-u", Credentials, "{imap}/INBOX;UID=1");

        Assert.AreEqual(UseSslFailed, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Fetch_ImapsWithSelfSigned_WritesTheStoredMessage()
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--self-signed"], "-sS", "-k", "-u", Credentials, "{imaps}/INBOX;UID=1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    [DataRow("AUTH=PLAIN", false, "AUTHENTICATE PLAIN", DisplayName = "AUTHENTICATE PLAIN")]
    [DataRow("AUTH=PLAIN", true, "AUTHENTICATE PLAIN", DisplayName = "AUTHENTICATE PLAIN with --sasl-ir")]
    [DataRow("AUTH=LOGIN", false, "AUTHENTICATE LOGIN", DisplayName = "AUTHENTICATE LOGIN")]
    [DataRow("AUTH=CRAM-MD5", false, "AUTHENTICATE CRAM-MD5", DisplayName = "AUTHENTICATE CRAM-MD5")]
    [DataRow("AUTH=+LOGIN", false, "LOGIN tester", DisplayName = "the LOGIN command")]
    public async Task Fetch_ImapsForcedPasswordLogin_LogsInAndWritesTheStoredMessage(string loginOptions, bool initialResponse, string sent)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--self-signed"],
            [
                "-sS", "-v", "-k", .. initialResponse ? (string[])["--sasl-ir"] : [],
                "-u", Credentials, "--login-options", loginOptions, "{imaps}/INBOX;UID=1",
            ]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, sent);
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    [DataRow("XOAUTH2", false)]
    [DataRow("XOAUTH2", true)]
    [DataRow("OAUTHBEARER", false)]
    [DataRow("OAUTHBEARER", true)]
    public async Task Fetch_ImapsForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage(string mechanism, bool initialResponse)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--self-signed"],
            [
                "-sS", "-v", "-k", .. initialResponse ? (string[])["--sasl-ir"] : [],
                "-u", $"{AccountsFile.User}:", "--oauth2-bearer", AccountsFile.BearerToken, "--login-options", $"AUTH={mechanism}",
                "{imaps}/INBOX;UID=1",
            ]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"AUTHENTICATE {mechanism}");
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    [DataRow("DIGEST-MD5", "digest-md5")]
    [DataRow("NTLM", "ntlm")]
    public async Task Fetch_ImapsForcedNamedChoiceMechanism_LogsInAndWritesTheStoredMessage(string mechanism, string authWord)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--self-signed", "--auth", authWord],
            "-sS", "-v", "-k", "-u", Credentials, "--login-options", $"AUTH={mechanism}", "{imaps}/INBOX;UID=1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"AUTHENTICATE {mechanism}");
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    public async Task Fetch_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync(NewTemporaryDirectory(), "imap", "--user-file", accounts.Path, "--max-connections", "1");
        await SendMailOverSmtpAsync(surl, Credentials);
        var imap = surl.BaseUrlOf("imap");
        using var holder = new TcpClient();
        await holder.ConnectAsync(imap.Host, imap.Port, TestContext.CancellationToken);
        var greeting = new byte[4];
        await holder.GetStream().ReadExactlyAsync(greeting, TestContext.CancellationToken);
        Assert.AreEqual("* OK", Encoding.ASCII.GetString(greeting));

        var result = await RunUpstreamCurlAsync("-sS", "-u", Credentials, ImapUrl(surl, "/INBOX;UID=1"));

        Assert.AreEqual(WeirdServerReply, result.ExitCode, result.StandardError);
    }

    // The stored message is ADR-0053 decision 6's Return-Path and Received fields, then mail.txt.
    private static void AssertIsTheDeliveredMessage(byte[] fetched, IReadOnlyList<byte[]> stored, string protocol)
    {
        Assert.HasCount(1, stored);
        var text = Encoding.UTF8.GetString(stored[0]);
        var traceFields = Regex.Match(
            text,
            $@"\AReturn-Path: <a@x>\r\nReceived: from c \(\[127\.0\.0\.1\]\) by surl with {protocol}; [A-Z][a-z]{{2}}, \d{{2}} [A-Z][a-z]{{2}} \d{{4}} \d{{2}}:\d{{2}}:\d{{2}} \+0000\r\n",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        Assert.IsTrue(traceFields.Success, text);
        CollectionAssert.AreEqual(Mail, stored[0][traceFields.Length..]);
        CollectionAssert.AreEqual(stored[0], fetched);
    }

    private async Task AssertIsTheDeliveredMessageAsync(byte[] fetched, string dataDirectory, string? accountName, string protocol) =>
        AssertIsTheDeliveredMessage(fetched, await ReadInboxAsync(dataDirectory, accountName), protocol);

    // Starts surl with the accounts file and serverOptions on smtp and on the scheme curlArguments'
    // URL names ({imap} or {imaps}), delivers mail.txt to tester over smtp, runs curl once with the
    // URL placeholder filled in, stops surl and reads tester's inbox back from the store.
    private async Task<(UpstreamCurlRunResult Result, IReadOnlyList<byte[]> Stored)> RunAgainstSeededSurlAndReadInboxAsync(
        string[] serverOptions, params string[] curlArguments)
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var scheme = curlArguments.Any(argument => argument.StartsWith("{imaps}", StringComparison.Ordinal)) ? "imaps" : "imap";
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync(dataDirectory, scheme, [.. serverOptions, "--user-file", accounts.Path]))
        {
            await SendMailOverSmtpAsync(surl, Credentials);
            var imapBase = ImapUrl(surl, string.Empty);
            result = await RunUpstreamCurlAsync(
                [.. curlArguments.Select(argument => argument.Replace($"{{{scheme}}}", imapBase, StringComparison.Ordinal))]);
        }

        return (result, await ReadInboxAsync(dataDirectory, AccountsFile.User));
    }

    private async Task<UpstreamCurlRunResult> RunAgainstSeededSurlAsync(string[] serverOptions, params string[] curlArguments) =>
        (await RunAgainstSeededSurlAndReadInboxAsync(serverOptions, curlArguments)).Result;

    // curl delivers mail.txt over smtp as BL-210 does: logged in as tester when credentials are
    // given, else anonymously; the URL ends /c so the trace field names EHLO c.
    private async Task SendMailOverSmtpAsync(SurlOnLoopback surl, string? credentials)
    {
        var smtp = surl.BaseUrlOf("smtp");
        var mail = await WriteMailFileAsync();
        var result = await RunUpstreamCurlAsync(
            [
                "-sS", .. credentials is null ? (string[])[] : ["-u", credentials],
                "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail, $"smtp://{smtp.Host}:{smtp.Port}/c",
            ]);
        Assert.AreEqual(0, result.ExitCode, $"The SMTP delivery failed: {result.StandardError}");
    }

    private static string ImapUrl(SurlOnLoopback surl, string path)
    {
        var imap = surl.BaseUrls.Single(url => url.Scheme is "imap" or "imaps");
        return $"{imap.Scheme}://{imap.Host}:{imap.Port}{path}";
    }

    private string NewTemporaryDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-conformance-imap-").FullName;
        temporaryDirectories.Add(directory);
        return directory;
    }

    private async Task<string> WriteMailFileAsync()
    {
        var path = Path.Combine(NewTemporaryDirectory(), "mail.txt");
        await File.WriteAllBytesAsync(path, Mail, TestContext.CancellationToken);
        return path;
    }

    private Task<SurlOnLoopback> StartSurlAsync(string dataDirectory, string mailScheme, params string[] options) =>
        SurlOnLoopback.StartOverDirectoryAsync(["smtp", mailScheme], dataDirectory, options, TestContext.CancellationToken);

    private Task<IReadOnlyList<byte[]>> ReadInboxAsync(string dataDirectory, string? accountName) =>
        StoredMail.ReadInboxAsync(dataDirectory, accountName, TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunUpstreamCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunAsync(TestContext, arguments);
}
