using System.Text;
using Surl.Console;

namespace Surl.Conformance;

/// <summary>
/// One <c>surl</c> configured from a <c>-K</c> config file (ADR-0101) with a mail domain, mailboxes,
/// an alias, shares with per-account access and a topic grant (ADR-0102), and the pinned upstream
/// curl completing, or being refused as ADR-0102 says, the exchanges a real deployment sees:
/// the replies and exit codes ADR-0102's measurements R1, R2, R12 and the share table give. The
/// refused-config case needs no curl. Inconclusive where no pinned build is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlTalksToAConfiguredSurlTests
{
    // CURLE_REMOTE_ACCESS_DENIED: RCPT refused (ADR-0102 R1, exit 55 "RCPT failed").
    private const int RecipientRefused = 55;

    // CURLE_LOGIN_DENIED.
    private const int LoginDenied = 67;

    // CURLE_UPLOAD_FAILED (ADR-0102 table, FTP STOR denied).
    private const int UploadFailed = 25;

    // CURLE_WEIRD_SERVER_REPLY (ADR-0102 R12: SUBACK 0x80 then DISCONNECT).
    private const int WeirdServerReply = 8;

    // CURLE_BAD_FUNCTION_ARGUMENT-free usage failure: surl's FailedInit (ADR-0101 decision 5 row 7).
    private const int FailedInit = 2;

    private const string Password = "secret";

    private const string Ann = "ann@example.test";

    private const string Bob = "bob@example.test";

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
    public async Task MailToAMailboxOfTheDomain_IsDeliveredAndBobReadsItOverImap()
    {
        await using var surl = await StartMailAsync();

        var send = await SendMailAsync(surl, Ann, Bob);
        var read = await ReadAsync(surl, "imap", Bob, "/INBOX;UID=1");

        Assert.AreEqual(0, send.ExitCode, send.StandardError);
        Assert.AreEqual(0, read.ExitCode, read.StandardError);
        StringAssert.EndsWith(Encoding.UTF8.GetString(read.StandardOutput), Encoding.UTF8.GetString(Mail));
    }

    [TestMethod]
    public async Task MailToARecipientInAnotherDomain_IsRefusedAsRelayingAndCurlExits55()
    {
        await using var surl = await StartMailAsync();

        var send = await SendMailAsync(surl, Ann, "bob@elsewhere.example");

        Assert.AreEqual(RecipientRefused, send.ExitCode, send.StandardError);
        StringAssert.Contains(send.StandardError, "RCPT failed: 550");
    }

    [TestMethod]
    public async Task MailToAnAlias_IsDeliveredToTheMailboxTheAliasNames()
    {
        await using var surl = await StartMailAsync();

        var send = await SendMailAsync(surl, Ann, "team@example.test");
        var read = await ReadAsync(surl, "imap", Bob, "/INBOX;UID=1");

        Assert.AreEqual(0, send.ExitCode, send.StandardError);
        Assert.AreEqual(0, read.ExitCode, read.StandardError);
        StringAssert.EndsWith(Encoding.UTF8.GetString(read.StandardOutput), Encoding.UTF8.GetString(Mail));
    }

    [TestMethod]
    public async Task MailboxRestrictedToImapAndPop3_IsRefusedOverSmtpAndLogsInOverImap()
    {
        await using var surl = await StartMailAsync();

        var smtp = await SendMailAsync(surl, "carol@example.test", Bob);
        var imap = await ReadAsync(surl, "imap", "carol@example.test", "/");

        Assert.AreEqual(LoginDenied, smtp.ExitCode, smtp.StandardError);
        Assert.AreEqual(0, imap.ExitCode, imap.StandardError);
    }

    [TestMethod]
    public async Task ReadAccountOfAShare_FetchesOverHttpAndFtpAndIsRefusedAnFtpUpload()
    {
        await using var surl = await StartSharesAsync();

        var http = await FetchAsync(surl, "http", "reader", "/pub/f.txt");
        var ftp = await FetchAsync(surl, "ftp", "reader", "/pub/f.txt");
        var upload = await UploadAsync(surl, "reader", "/pub/new.txt");

        Assert.AreEqual(0, http.ExitCode, http.StandardError);
        CollectionAssert.AreEqual("shared\n"u8.ToArray(), http.StandardOutput);
        Assert.AreEqual(0, ftp.ExitCode, ftp.StandardError);
        CollectionAssert.AreEqual("shared\n"u8.ToArray(), ftp.StandardOutput);
        Assert.AreEqual(UploadFailed, upload.ExitCode, upload.StandardError);
    }

    [TestMethod]
    public async Task ReadWriteAccountOfAShare_UploadsOverFtpAndTheFileIsReadOverHttp()
    {
        await using var surl = await StartSharesAsync();

        var upload = await UploadAsync(surl, "writer", "/pub/new.txt");
        var http = await FetchAsync(surl, "http", "reader", "/pub/new.txt");

        Assert.AreEqual(0, upload.ExitCode, upload.StandardError);
        Assert.AreEqual(0, http.ExitCode, http.StandardError);
        CollectionAssert.AreEqual("uploaded\n"u8.ToArray(), http.StandardOutput);
    }

    [TestMethod]
    public async Task MqttSubscribeByAnAccountWithoutSub_IsRefusedAndCurlExits8()
    {
        await using var surl = await StartTopicsAsync();

        var subscribe = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-u", $"pubber:{Password}", UrlOf(surl, "mqtt", "/a/b"));

        Assert.AreEqual(WeirdServerReply, subscribe.ExitCode, subscribe.StandardError);
        Assert.IsEmpty(subscribe.StandardOutput);
    }

    [TestMethod]
    public async Task MqttPublishByAnAccountWithPub_IsAccepted()
    {
        await using var surl = await StartTopicsAsync();

        var publish = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-u", $"pubber:{Password}", "-d", "hi", UrlOf(surl, "mqtt", "/a/b"));

        Assert.AreEqual(0, publish.ExitCode, publish.StandardError);
    }

    [TestMethod]
    public async Task ConfigFileLineInError_IsRefusedWithTheFileAndLine()
    {
        var config = await WriteConfigAsync("# comment\n\nnosuchoption = 1\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["-K", config, "http://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(FailedInit, exitCode, error.ToString());
        StringAssert.Contains(error.ToString(), $"{config}:3: option --nosuchoption: is unknown");
    }

    private Task<SurlOnLoopback> StartMailAsync() =>
        StartAsync(
            ["smtp", "imap", "pop3"],
            $"""
            mail-domain = example.test
            mailbox = {Ann}:{Password}
            mailbox = {Bob}:{Password}
            mailbox = "carol@example.test:{Password};protocols=imap,pop3"
            mail-alias = team@example.test=bob@example.test
            """);

    private async Task<SurlOnLoopback> StartSharesAsync()
    {
        var share = NewTemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(share, "f.txt"), "shared\n", TestContext.CancellationToken);
        return await StartAsync(
            ["http", "ftp"],
            $"""
            allow-uploads
            allow-plaintext-auth
            user = reader:{Password}
            user = writer:{Password}
            share = "pub={share.Replace('\\', '/')}"
            share-access = pub=reader:r,writer:rw
            """);
    }

    private Task<SurlOnLoopback> StartTopicsAsync() =>
        StartAsync(
            ["mqtt"],
            $"""
            allow-plaintext-auth
            user = pubber:{Password}
            topic-access = a/b=pubber:pub
            """);

    private async Task<SurlOnLoopback> StartAsync(string[] schemes, string config) =>
        await SurlOnLoopback.StartInMemoryAsync(
            schemes, ["-K", await WriteConfigAsync(config)], TestContext.CancellationToken);

    private async Task<string> WriteConfigAsync(string text)
    {
        var path = Path.Combine(NewTemporaryDirectory(), "surl.cfg");
        await File.WriteAllTextAsync(path, text, TestContext.CancellationToken);
        return path;
    }

    private async Task<UpstreamCurlRunResult> SendMailAsync(SurlOnLoopback surl, string sender, string recipient)
    {
        var mail = Path.Combine(NewTemporaryDirectory(), "mail.txt");
        await File.WriteAllBytesAsync(mail, Mail, TestContext.CancellationToken);
        return await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-u", $"{sender}:{Password}", "--mail-from", sender, "--mail-rcpt", recipient,
            "-T", mail, UrlOf(surl, "smtp", "/c"));
    }

    private Task<UpstreamCurlRunResult> ReadAsync(SurlOnLoopback surl, string scheme, string account, string path) =>
        PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-u", $"{account}:{Password}", UrlOf(surl, scheme, path));

    private Task<UpstreamCurlRunResult> FetchAsync(SurlOnLoopback surl, string scheme, string account, string path) =>
        ReadAsync(surl, scheme, account, path);

    private async Task<UpstreamCurlRunResult> UploadAsync(SurlOnLoopback surl, string account, string path)
    {
        var file = Path.Combine(NewTemporaryDirectory(), "upload.txt");
        await File.WriteAllTextAsync(file, "uploaded\n", TestContext.CancellationToken);
        return await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-u", $"{account}:{Password}", "-T", file, UrlOf(surl, "ftp", path));
    }

    private static string UrlOf(SurlOnLoopback surl, string scheme, string path)
    {
        var url = surl.BaseUrlOf(scheme);
        return $"{url.Scheme}://{url.Host}:{url.Port}{path}";
    }

    private string NewTemporaryDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-conformance-config-").FullName;
        temporaryDirectories.Add(directory);
        return directory;
    }
}
