using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build lists, retrieves and deletes mail on a live, in-process
/// <c>surl</c> over <c>pop3</c> and <c>pop3s</c>, and is answered as ADR-0056 decision 12 lists.
/// Every case starts one surl listening on <c>smtp</c> beside <c>pop3</c> or <c>pop3s</c> over a
/// fresh <c>--directory</c>, and first has curl deliver ADR-0056's <c>mail.txt</c> over
/// <c>smtp</c> (BL-210's case), so the maildrop holds one message; every retrieval of it must
/// then be, byte for byte, the message the stopped surl's persisted mail store holds (ADR-0050,
/// decision 7): ADR-0053 decision 6's trace fields followed by <c>mail.txt</c>. A case whose surl
/// options include <c>--auth</c> leaves SMTP no login mechanism curl would use, so its message is
/// delivered by an earlier surl over the same directory, and the store carries it across.
/// Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlReadsMailFromSurlOverPop3Tests
{
    // CURLE_WEIRD_SERVER_REPLY: any -ERR outside the login, or one in place of the greeting
    // (ADR-0056, rows 11, 13, 15 and 20).
    private const int WeirdServerReply = 8;

    // CURLE_USE_SSL_FAILED: --ssl-reqd and no STLS (rows 28 and 30).
    private const int UseSslFailed = 64;

    // CURLE_LOGIN_DENIED: a refused login, a login not offered, or a locked maildrop (rows 16 and 18).
    private const int LoginDenied = 67;

    private const string Recipient = "tester@example.com";

    // ADR-0056's mail.txt: 33 bytes, every line ending CRLF.
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
    [DataRow(new string[0], DisplayName = "LIST")]
    [DataRow(new[] { "-l" }, DisplayName = "LIST with -l")]
    [DataRow(new[] { "-I" }, DisplayName = "LIST with -I")]
    public async Task List_Root_WritesOneScanListingLineAfterACramMd5Login(string[] listOptions)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync([], ["-sS", "-v", .. listOptions, "-u", Credentials, "{pop3}/"]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "AUTH CRAM-MD5");
        Assert.HasCount(1, stored);
        Assert.AreEqual($"1 {stored[0].Length}\r\n", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    [DataRow(new string[0], DisplayName = "RETR")]
    [DataRow(new[] { "-I" }, DisplayName = "RETR with -I")]
    public async Task Retrieve_MailSentOverSmtp_WritesTheStoredMessageByteForByte(string[] retrieveOptions)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync([], ["-sS", .. retrieveOptions, "-u", Credentials, "{pop3}/1"]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    public async Task ListOnly_OneMessage_WritesNothing()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-l", "-u", Credentials, "{pop3}/1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Retrieve_AMessageNumberPastTheMaildrop_ExitsWeirdServerReply()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "{pop3}/9");

        Assert.AreEqual(WeirdServerReply, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task CustomUidl_WritesTheUniqueIdFromTheStoresUidValidityAndUid()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync(dataDirectory, "pop3", "--user-file", accounts.Path))
        {
            await SendMailOverSmtpAsync(surl, Credentials);
            result = await RunUpstreamCurlAsync("-sS", "-u", Credentials, "-X", "UIDL", Pop3Url(surl, "/"));
        }

        var uidValidity = await StoredMail.ReadInboxUidValidityAsync(dataDirectory, AccountsFile.User, TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual($"1 {uidValidity}.1\r\n", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    [DataRow("TOP 1 0", "/", DisplayName = "TOP 1 0")]
    [DataRow("TOP", "/1", DisplayName = "TOP with the message in the URL")]
    public async Task CustomTop_WritesTheStoredHeaderSectionAndTheEmptyLine(string command, string path)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync([], "-sS", "-u", Credentials, "-X", command, "{pop3}" + path);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.HasCount(1, stored);
        var text = Encoding.ASCII.GetString(stored[0]);
        var headerSectionLength = text.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
        Assert.AreEqual(text[..headerSectionLength], Encoding.ASCII.GetString(result.StandardOutput));
        StringAssert.EndsWith(text[..headerSectionLength], "Subject: hi\r\n\r\n");
    }

    [TestMethod]
    public async Task CustomCapa_AfterTheLogin_WritesTheFiveTransactionCapabilities()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "-X", "CAPA", "{pop3}/");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("TOP\r\nUIDL\r\nRESP-CODES\r\nAUTH-RESP-CODE\r\nPIPELINING\r\n", Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    [DataRow("STAT")]
    [DataRow("NOOP")]
    [DataRow("RSET")]
    public async Task CustomSingleLineCommand_ExitsZeroAndWritesNothing(string command)
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "-X", command, "{pop3}/");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task CustomDele_ThenList_ListsAnEmptyMaildropAndTheStoreHoldsNothing()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult delete;
        UpstreamCurlRunResult list;
        await using (var surl = await StartSurlAsync(dataDirectory, "pop3", "--user-file", accounts.Path))
        {
            await SendMailOverSmtpAsync(surl, Credentials);
            delete = await RunUpstreamCurlAsync("-sS", "-u", Credentials, "-X", "DELE 1", Pop3Url(surl, "/"));
            list = await RunUpstreamCurlAsync("-sS", "-u", Credentials, Pop3Url(surl, "/"));
        }

        Assert.AreEqual(0, delete.ExitCode, delete.StandardError);
        Assert.IsEmpty(delete.StandardOutput);
        Assert.AreEqual(0, list.ExitCode, list.StandardError);
        Assert.AreEqual("\r\n", Encoding.ASCII.GetString(list.StandardOutput));
        Assert.IsEmpty(await ReadInboxAsync(dataDirectory, AccountsFile.User));
    }

    [TestMethod]
    public async Task CustomUnknownCommand_ExitsWeirdServerReply()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "-X", "XYZZY", "{pop3}/");

        Assert.AreEqual(WeirdServerReply, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Retrieve_WrongPassword_ExitsLoginDenied()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", $"{AccountsFile.User}:wrong", "{pop3}/1");

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Retrieve_NoLoginWithoutAllowAnonymous_ExitsWeirdServerReply()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "{pop3}/1");

        Assert.AreEqual(WeirdServerReply, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Retrieve_NoLoginWithAllowAnonymous_WritesTheMessageSentAnonymouslyOverSmtp()
    {
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync(dataDirectory, "pop3", "--allow-anonymous"))
        {
            await SendMailOverSmtpAsync(surl, null);
            result = await RunUpstreamCurlAsync("-sS", Pop3Url(surl, "/1"));
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        AssertIsTheDeliveredMessage(result.StandardOutput, await ReadInboxAsync(dataDirectory, null), "ESMTP");
    }

    [TestMethod]
    public async Task Retrieve_PlainOverPlaintext_ExitsLoginDenied()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "-u", Credentials, "--login-options", "AUTH=PLAIN", "{pop3}/1");

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    public async Task Retrieve_ClearPasswordMechanismOverPlaintextWithAllowPlaintextAuth_WritesTheStoredMessage(string mechanism)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--allow-plaintext-auth"], "-sS", "-v", "-u", Credentials, "--login-options", $"AUTH={mechanism}", "{pop3}/1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"AUTH {mechanism}");
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    public async Task Retrieve_UserPassWithAllowPlaintextAuth_WritesTheStoredMessage()
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--auth", "basic", "--allow-plaintext-auth"], "-sS", "-v", "-u", Credentials, "{pop3}/1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"USER {AccountsFile.User}");
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    public async Task Retrieve_UserPassOverPlaintextWithoutAllowPlaintextAuth_ExitsLoginDenied()
    {
        var result = await RunAgainstSeededSurlAsync(["--auth", "basic"], "-sS", "-u", Credentials, "{pop3}/1");

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Retrieve_UserPassWrongPassword_ExitsLoginDenied()
    {
        var result = await RunAgainstSeededSurlAsync(
            ["--auth", "basic", "--allow-plaintext-auth"], "-sS", "-u", $"{AccountsFile.User}:wrong", "{pop3}/1");

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "APOP forced with --login-options AUTH=+APOP")]
    [DataRow(false, DisplayName = "APOP chosen from the greeting's timestamp")]
    public async Task Retrieve_Apop_WritesTheStoredMessage(bool forced)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--auth", "apop"],
            ["-sS", "-v", "-u", Credentials, .. forced ? (string[])["--login-options", "AUTH=+APOP"] : [], "{pop3}/1"]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"APOP {AccountsFile.User} ");
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    public async Task Retrieve_SslReqdWithSelfSigned_UpgradesWithStlsAndWritesTheStoredMessage()
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--self-signed"], "-sS", "-v", "-k", "--ssl-reqd", "-u", Credentials, "{pop3}/1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "STLS");
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    public async Task Retrieve_SslReqdWithoutACertificate_ExitsUseSslFailed()
    {
        var result = await RunAgainstSeededSurlAsync([], "-sS", "--ssl-reqd", "-u", Credentials, "{pop3}/1");

        Assert.AreEqual(UseSslFailed, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Retrieve_Pop3sWithSelfSigned_WritesTheStoredMessage()
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(["--self-signed"], "-sS", "-k", "-u", Credentials, "{pop3s}/1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    [DataRow("PLAIN", false)]
    [DataRow("PLAIN", true, DisplayName = "PLAIN with --sasl-ir")]
    [DataRow("LOGIN", false)]
    [DataRow("CRAM-MD5", false)]
    public async Task Retrieve_Pop3sForcedPasswordMechanism_LogsInAndWritesTheStoredMessage(string mechanism, bool initialResponse)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--self-signed"],
            [
                "-sS", "-v", "-k", .. initialResponse ? (string[])["--sasl-ir"] : [],
                "-u", Credentials, "--login-options", $"AUTH={mechanism}", "{pop3s}/1",
            ]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"AUTH {mechanism}");
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    [DataRow("XOAUTH2")]
    [DataRow("OAUTHBEARER")]
    public async Task Retrieve_Pop3sForcedBearerMechanism_LogsInAsTheNamedAccountAndWritesTheStoredMessage(string mechanism)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--self-signed"],
            "-sS", "-v", "-k", "-u", $"{AccountsFile.User}:", "--oauth2-bearer", AccountsFile.BearerToken,
            "--login-options", $"AUTH={mechanism}", "{pop3s}/1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"AUTH {mechanism}");
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    [DataRow("DIGEST-MD5", "digest-md5")]
    [DataRow("NTLM", "ntlm")]
    public async Task Retrieve_Pop3sForcedNamedChoiceMechanism_LogsInAndWritesTheStoredMessage(string mechanism, string authWord)
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--self-signed", "--auth", authWord],
            "-sS", "-v", "-k", "-u", Credentials, "--login-options", $"AUTH={mechanism}", "{pop3s}/1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"AUTH {mechanism}");
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    public async Task Retrieve_Pop3sUserPass_WritesTheStoredMessage()
    {
        var (result, stored) = await RunAgainstSeededSurlAndReadInboxAsync(
            ["--self-signed", "--auth", "basic"], "-sS", "-v", "-k", "-u", Credentials, "{pop3s}/1");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, $"USER {AccountsFile.User}");
        AssertIsTheDeliveredMessage(result.StandardOutput, stored, "ESMTPA");
    }

    [TestMethod]
    public async Task Retrieve_MaildropHeldByAnotherSession_ExitsLoginDenied()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync(NewTemporaryDirectory(), "pop3", "--user-file", accounts.Path, "--allow-plaintext-auth");
        await SendMailOverSmtpAsync(surl, Credentials);
        var pop3 = surl.BaseUrlOf("pop3");
        using var holder = new TcpClient();
        await holder.ConnectAsync(pop3.Host, pop3.Port, TestContext.CancellationToken);
        using var holderReader = new StreamReader(holder.GetStream(), Encoding.ASCII);
        StringAssert.StartsWith(await holderReader.ReadLineAsync(TestContext.CancellationToken), "+OK");
        await holder.GetStream().WriteAsync(Encoding.ASCII.GetBytes($"USER {AccountsFile.User}\r\n"), TestContext.CancellationToken);
        StringAssert.StartsWith(await holderReader.ReadLineAsync(TestContext.CancellationToken), "+OK");
        await holder.GetStream().WriteAsync(Encoding.ASCII.GetBytes($"PASS {AccountsFile.Password}\r\n"), TestContext.CancellationToken);
        Assert.AreEqual("+OK Logged in", await holderReader.ReadLineAsync(TestContext.CancellationToken));

        var result = await RunUpstreamCurlAsync("-sS", "-v", "-u", Credentials, Pop3Url(surl, "/1"));

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "[IN-USE]");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Retrieve_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync(NewTemporaryDirectory(), "pop3", "--user-file", accounts.Path, "--max-connections", "1");
        await SendMailOverSmtpAsync(surl, Credentials);
        var pop3 = surl.BaseUrlOf("pop3");
        using var holder = new TcpClient();
        await holder.ConnectAsync(pop3.Host, pop3.Port, TestContext.CancellationToken);
        var greeting = new byte[3];
        await holder.GetStream().ReadExactlyAsync(greeting, TestContext.CancellationToken);
        Assert.AreEqual("+OK", Encoding.ASCII.GetString(greeting));

        var result = await RunUpstreamCurlAsync("-sS", "-u", Credentials, Pop3Url(surl, "/1"));

        Assert.AreEqual(WeirdServerReply, result.ExitCode, result.StandardError);
    }

    // The stored message is ADR-0053 decision 6's Return-Path and Received fields, then mail.txt.
    private static void AssertIsTheDeliveredMessage(byte[] retrieved, IReadOnlyList<byte[]> stored, string protocol)
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
        CollectionAssert.AreEqual(stored[0], retrieved);
    }

    // Delivers mail.txt to tester over smtp, starts surl with the accounts file and serverOptions
    // on smtp and on the scheme curlArguments' URL names ({pop3} or {pop3s}), runs curl once with
    // the URL placeholder filled in, stops surl and reads tester's inbox back from the store. With
    // --auth among serverOptions the delivery is made by an earlier surl over the same directory
    // with the default login options, since SMTP then offers curl no mechanism it would log in with.
    private async Task<(UpstreamCurlRunResult Result, IReadOnlyList<byte[]> Stored)> RunAgainstSeededSurlAndReadInboxAsync(
        string[] serverOptions, params string[] curlArguments)
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var scheme = curlArguments.Any(argument => argument.StartsWith("{pop3s}", StringComparison.Ordinal)) ? "pop3s" : "pop3";
        var dataDirectory = NewTemporaryDirectory();
        var deliversFirst = serverOptions.Contains("--auth");
        if (deliversFirst)
        {
            await using var seeder = await StartSurlAsync(dataDirectory, "pop3", "--user-file", accounts.Path);
            await SendMailOverSmtpAsync(seeder, Credentials);
        }

        UpstreamCurlRunResult result;
        await using (var surl = await StartSurlAsync(dataDirectory, scheme, [.. serverOptions, "--user-file", accounts.Path]))
        {
            if (!deliversFirst)
            {
                await SendMailOverSmtpAsync(surl, Credentials);
            }

            var pop3Base = Pop3Url(surl, string.Empty);
            result = await RunUpstreamCurlAsync(
                [.. curlArguments.Select(argument => argument.Replace($"{{{scheme}}}", pop3Base, StringComparison.Ordinal))]);
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

    private static string Pop3Url(SurlOnLoopback surl, string path)
    {
        var pop3 = surl.BaseUrls.Single(url => url.Scheme is "pop3" or "pop3s");
        return $"{pop3.Scheme}://{pop3.Host}:{pop3.Port}{path}";
    }

    private string NewTemporaryDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-conformance-pop3-").FullName;
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
