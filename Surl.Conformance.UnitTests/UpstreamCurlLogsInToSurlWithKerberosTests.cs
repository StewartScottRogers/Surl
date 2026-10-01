namespace Surl.Conformance;

/// <summary>
/// The pinned Windows reference build logs in to a live, in-process <c>surl --keytab</c> with
/// Kerberos (ADR-0057, ADR-0065 decision 3): <c>--negotiate</c> over <c>http</c>, and SASL
/// <c>GSSAPI</c> over <c>smtp</c>, <c>imap</c> and <c>pop3</c> with and without <c>--sasl-ir</c>,
/// its tickets issued by the hand-built test KDC on <c>127.0.0.1:88</c> for
/// <c>tester@SURL.TEST</c>. Every URL names its service host (<c>web.surl.test</c>,
/// <c>mail.surl.test</c>) and pins it to loopback with <c>--resolve</c>, so curl's SPN is the one
/// the keytab holds. A ticket is a login only when <c>--user-file</c> has an account of the
/// principal's name (ADR-0057 decision 10); without one, the login is refused. Inconclusive off
/// Windows, without BL-265's <c>SURL.TEST</c> realm mapping, with port 88 taken, or where the
/// pinned build is not installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[OSCondition(OperatingSystems.Windows)]
[DoNotParallelize]
public sealed class UpstreamCurlLogsInToSurlWithKerberosTests
{
    // CURLE_HTTP_RETURNED_ERROR: with -f, a status of 400 or more.
    private const int HttpReturnedError = 22;

    // CURLE_LOGIN_DENIED: any SASL refusal (ADR-0049 section 7).
    private const int LoginDenied = 67;

    private const string WebHost = "web.surl.test";

    private const string MailHost = "mail.surl.test";

    private const string Recipient = "\"tester@SURL.TEST\"@example.com";

    private static readonly string Credentials = $"{KerberosTestKdcOnLoopback.UserPrincipal}:{KerberosTestKdcOnLoopback.Password}";

    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    private static readonly byte[] Mail = "From: a@x\r\nSubject: hi\r\n\r\nhello\r\n"u8.ToArray();

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
    public async Task NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes()
    {
        await using var kdc = await StartKdcAsync($"HTTP/{WebHost}");
        var accounts = await WriteKerberosAccountsFileAsync();
        await using var surl = await StartHttpSurlAsync(kdc, accounts);

        var result = await RunUpstreamCurlAsync(
            ["-sS", "-f", "--negotiate", "-u", Credentials, .. Resolve(WebHost, surl.BaseUrl), WebUrl(surl, "hello.txt")]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    public async Task NegotiateOverHttp_NoAccount_IsRefusedWithHttpReturnedError()
    {
        await using var kdc = await StartKdcAsync($"HTTP/{WebHost}");
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartHttpSurlAsync(kdc, accounts.Path);

        var result = await RunUpstreamCurlAsync(
            ["-sS", "-f", "--negotiate", "-u", Credentials, .. Resolve(WebHost, surl.BaseUrl), WebUrl(surl, "hello.txt")]);

        Assert.AreEqual(HttpReturnedError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "401");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "without --sasl-ir")]
    [DataRow(true, DisplayName = "with --sasl-ir")]
    public async Task GssapiOverSmtp_Account_StoresTheMessageInTheAccountsInbox(bool initialResponse)
    {
        await using var kdc = await StartKdcAsync($"smtp/{MailHost}");
        var accounts = await WriteKerberosAccountsFileAsync();
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult result;
        await using (var surl = await StartMailSurlAsync(["smtp"], dataDirectory, kdc, accounts))
        {
            result = await SendMailWithGssapiAsync(surl, initialResponse);
        }

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var stored = await ReadInboxAsync(dataDirectory);
        Assert.HasCount(1, stored);
        CollectionAssert.AreEqual(Mail, stored[0][^Mail.Length..]);
    }

    [TestMethod]
    public async Task GssapiOverSmtp_NoAccount_ExitsLoginDenied()
    {
        await using var kdc = await StartKdcAsync($"smtp/{MailHost}");
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartMailSurlAsync(["smtp"], NewTemporaryDirectory(), kdc, accounts.Path);

        var result = await SendMailWithGssapiAsync(surl, initialResponse: false);

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "without --sasl-ir")]
    [DataRow(true, DisplayName = "with --sasl-ir")]
    public async Task GssapiOverImap_Account_FetchesTheDeliveredMessage(bool initialResponse)
    {
        var (result, stored) = await FetchDeliveredMessageAsync("imap", "/INBOX;UID=1", initialResponse);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.HasCount(1, stored);
        CollectionAssert.AreEqual(stored[0], result.StandardOutput);
    }

    [TestMethod]
    public async Task GssapiOverImap_NoAccount_ExitsLoginDenied()
    {
        var result = await RunWithoutKerberosAccountAsync("imap", "/INBOX;UID=1");

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "without --sasl-ir")]
    [DataRow(true, DisplayName = "with --sasl-ir")]
    public async Task GssapiOverPop3_Account_RetrievesTheDeliveredMessage(bool initialResponse)
    {
        var (result, stored) = await FetchDeliveredMessageAsync("pop3", "/1", initialResponse);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.HasCount(1, stored);
        CollectionAssert.AreEqual(stored[0], result.StandardOutput);
    }

    [TestMethod]
    public async Task GssapiOverPop3_NoAccount_ExitsLoginDenied()
    {
        var result = await RunWithoutKerberosAccountAsync("pop3", "/1");

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    // Starts the KDC for smtp and the mail scheme, surl on both with the Kerberos account,
    // delivers mail.txt over smtp with GSSAPI, reads it back over the mail scheme with GSSAPI,
    // stops surl and reads the account's inbox back from the store.
    private async Task<(UpstreamCurlRunResult Result, IReadOnlyList<byte[]> Stored)> FetchDeliveredMessageAsync(
        string mailScheme, string path, bool initialResponse)
    {
        await using var kdc = await StartKdcAsync($"smtp/{MailHost}", $"{ServiceOf(mailScheme)}/{MailHost}");
        var accounts = await WriteKerberosAccountsFileAsync();
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult result;
        await using (var surl = await StartMailSurlAsync(["smtp", mailScheme], dataDirectory, kdc, accounts))
        {
            var delivery = await SendMailWithGssapiAsync(surl, initialResponse: false);
            Assert.AreEqual(0, delivery.ExitCode, $"The SMTP delivery failed: {delivery.StandardError}");
            result = await ReadMailWithGssapiAsync(surl, mailScheme, path, initialResponse);
        }

        return (result, await ReadInboxAsync(dataDirectory));
    }

    // surl has only AccountsFile's tester, not tester@SURL.TEST, so the KDC's ticket names no
    // account and the mail login is refused.
    private async Task<UpstreamCurlRunResult> RunWithoutKerberosAccountAsync(string mailScheme, string path)
    {
        await using var kdc = await StartKdcAsync($"{ServiceOf(mailScheme)}/{MailHost}");
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartMailSurlAsync([mailScheme], NewTemporaryDirectory(), kdc, accounts.Path);
        return await ReadMailWithGssapiAsync(surl, mailScheme, path, initialResponse: false);
    }

    // The URL ends /c so curl sends EHLO c (ADR-0053 decision 10).
    private async Task<UpstreamCurlRunResult> SendMailWithGssapiAsync(SurlOnLoopback surl, bool initialResponse)
    {
        var smtp = surl.BaseUrlOf("smtp");
        var mail = Path.Combine(NewTemporaryDirectory(), "mail.txt");
        await File.WriteAllBytesAsync(mail, Mail, TestContext.CancellationToken);
        return await RunUpstreamCurlAsync(
            [
                "-sS", "-u", Credentials, "--login-options", "AUTH=GSSAPI", .. InitialResponseOption(initialResponse),
                .. Resolve(MailHost, smtp), "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail,
                $"smtp://{MailHost}:{smtp.Port}/c",
            ]);
    }

    private Task<UpstreamCurlRunResult> ReadMailWithGssapiAsync(SurlOnLoopback surl, string mailScheme, string path, bool initialResponse)
    {
        var listener = surl.BaseUrlOf(mailScheme);
        return RunUpstreamCurlAsync(
            [
                "-sS", "-u", Credentials, "--login-options", "AUTH=GSSAPI", .. InitialResponseOption(initialResponse),
                .. Resolve(MailHost, listener), $"{mailScheme}://{MailHost}:{listener.Port}{path}",
            ]);
    }

    private static string[] InitialResponseOption(bool initialResponse) => initialResponse ? ["--sasl-ir"] : [];

    private static string[] Resolve(string host, Uri listener) => ["--resolve", $"{host}:{listener.Port}:127.0.0.1"];

    private static string WebUrl(SurlOnLoopback surl, string relativePath) => $"http://{WebHost}:{surl.BaseUrl.Port}/{relativePath}";

    // ADR-0057 decision 2: the service each mail scheme's tickets are for.
    private static string ServiceOf(string mailScheme) => mailScheme == "pop3" ? "pop" : mailScheme;

    private Task<KerberosTestKdcOnLoopback> StartKdcAsync(params string[] servicePrincipals) =>
        KerberosTestKdcOnLoopback.StartAsync(servicePrincipals, TestContext.CancellationToken);

    // A --user-file whose one account is the KDC's user principal; its password is never used.
    private async Task<string> WriteKerberosAccountsFileAsync()
    {
        var path = Path.Combine(NewTemporaryDirectory(), "users.txt");
        await File.WriteAllTextAsync(path, $"{KerberosTestKdcOnLoopback.UserPrincipal}:unused\n", TestContext.CancellationToken);
        return path;
    }

    private Task<SurlOnLoopback> StartHttpSurlAsync(KerberosTestKdcOnLoopback kdc, string accountsPath) =>
        SurlOnLoopback.StartAsync(
            "http",
            new Dictionary<string, byte[]> { ["hello.txt"] = Hello },
            [],
            ["--auth", "negotiate", "--keytab", kdc.KeytabPath, "--user-file", accountsPath],
            TestContext.CancellationToken);

    private Task<SurlOnLoopback> StartMailSurlAsync(string[] schemes, string dataDirectory, KerberosTestKdcOnLoopback kdc, string accountsPath) =>
        SurlOnLoopback.StartOverDirectoryAsync(
            schemes, dataDirectory, ["--auth", "gssapi", "--keytab", kdc.KeytabPath, "--user-file", accountsPath], TestContext.CancellationToken);

    private string NewTemporaryDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-conformance-kerberos-").FullName;
        temporaryDirectories.Add(directory);
        return directory;
    }

    private Task<IReadOnlyList<byte[]>> ReadInboxAsync(string dataDirectory) =>
        StoredMail.ReadInboxAsync(dataDirectory, KerberosTestKdcOnLoopback.UserPrincipal, TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunUpstreamCurlAsync(string[] arguments) =>
        PinnedUpstreamCurl.RunAsync(TestContext, arguments);
}
