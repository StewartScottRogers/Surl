using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build sends mail to a live <c>surl</c> over <c>smtp</c> or
/// <c>smtps</c> and reads it back over <c>imap</c>, <c>pop3</c>, <c>imaps</c> and <c>pop3s</c>
/// from the same <c>surl</c>: one process, one mail store (ADR-0050; BL-622's ADR, decision 5).
/// Every message read must be, byte for byte, the message the stopped surl's persisted store holds
/// for that owner (ADR-0050 decisions 3 and 7: the store adds nothing), and that message is
/// ADR-0053 decision 6's trace fields followed by <c>mail.txt</c>. A single IMAP fetch is already
/// proven by <see cref="UpstreamCurlReadsMailFromSurlOverImapTests"/>; this class covers the
/// matrix: both readers, two recipients, the TLS schemes, a restart over <c>--directory</c>, and
/// a POP3 deletion IMAP then sees. Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlSharesMailAcrossSurlProtocolsTests
{
    // CURLE_REMOTE_FILE_NOT_FOUND: IMAP has no message with that UID (ADR-0055 decision 15, row 12).
    private const int RemoteFileNotFound = 78;

    private const string Alice = "alice";

    private const string Bob = "bob";

    private const string Password = "secret";

    // ADR-0055's and ADR-0056's mail.txt: 33 bytes, every line ending CRLF.
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
    public async Task SendOverSmtp_ThenReadOverImapAndPop3_BothWriteTheStoredMessageByteForByte()
    {
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult imap;
        UpstreamCurlRunResult pop3;
        await using (var surl = await StartSurlAsync(dataDirectory, ["smtp", "imap", "pop3"]))
        {
            await SendMailAsync(surl, "smtp", [], Alice);
            imap = await ReadAsync(surl, "imap", [], Alice, "/INBOX;UID=1");
            pop3 = await ReadAsync(surl, "pop3", [], Alice, "/1");
        }

        var stored = await ReadInboxAsync(dataDirectory, Alice);
        Assert.AreEqual(0, imap.ExitCode, imap.StandardError);
        Assert.AreEqual(0, pop3.ExitCode, pop3.StandardError);
        AssertIsTheDeliveredMessage(stored, "ESMTPA", imap.StandardOutput, pop3.StandardOutput);
    }

    [TestMethod]
    public async Task SendOverSmtpToTwoRecipients_EachReadsTheirOwnCopyOverImapAndPop3()
    {
        var dataDirectory = NewTemporaryDirectory();
        var reads = new Dictionary<string, UpstreamCurlRunResult[]>();
        await using (var surl = await StartSurlAsync(dataDirectory, ["smtp", "imap", "pop3"]))
        {
            await SendMailAsync(surl, "smtp", [], Alice, Bob);
            foreach (var account in (string[])[Alice, Bob])
            {
                reads[account] =
                [
                    await ReadAsync(surl, "imap", [], account, "/INBOX;UID=1"),
                    await ReadAsync(surl, "pop3", [], account, "/1"),
                ];
            }
        }

        foreach (var (account, results) in reads)
        {
            foreach (var result in results)
            {
                Assert.AreEqual(0, result.ExitCode, $"{account}: {result.StandardError}");
            }

            AssertIsTheDeliveredMessage(
                await ReadInboxAsync(dataDirectory, account), "ESMTPA", [.. results.Select(result => result.StandardOutput)]);
        }
    }

    [TestMethod]
    public async Task SendOverSmtps_ThenReadOverImapsAndPop3s_BothWriteTheStoredMessageByteForByte()
    {
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult imaps;
        UpstreamCurlRunResult pop3s;
        await using (var surl = await StartSurlAsync(dataDirectory, ["smtps", "imaps", "pop3s"], "--self-signed"))
        {
            await SendMailAsync(surl, "smtps", ["-k"], Alice);
            imaps = await ReadAsync(surl, "imaps", ["-k"], Alice, "/INBOX;UID=1");
            pop3s = await ReadAsync(surl, "pop3s", ["-k"], Alice, "/1");
        }

        var stored = await ReadInboxAsync(dataDirectory, Alice);
        Assert.AreEqual(0, imaps.ExitCode, imaps.StandardError);
        Assert.AreEqual(0, pop3s.ExitCode, pop3s.StandardError);
        AssertIsTheDeliveredMessage(stored, "ESMTPSA", imaps.StandardOutput, pop3s.StandardOutput);
    }

    [TestMethod]
    public async Task SendOverSmtp_ThenRestartSurlOverTheSameDirectory_ImapAndPop3StillWriteTheStoredMessage()
    {
        var dataDirectory = NewTemporaryDirectory();
        await using (var sender = await StartSurlAsync(dataDirectory, ["smtp"]))
        {
            await SendMailAsync(sender, "smtp", [], Alice);
        }

        UpstreamCurlRunResult imap;
        UpstreamCurlRunResult pop3;
        await using (var reader = await StartSurlAsync(dataDirectory, ["imap", "pop3"]))
        {
            imap = await ReadAsync(reader, "imap", [], Alice, "/INBOX;UID=1");
            pop3 = await ReadAsync(reader, "pop3", [], Alice, "/1");
        }

        var stored = await ReadInboxAsync(dataDirectory, Alice);
        Assert.AreEqual(0, imap.ExitCode, imap.StandardError);
        Assert.AreEqual(0, pop3.ExitCode, pop3.StandardError);
        AssertIsTheDeliveredMessage(stored, "ESMTPA", imap.StandardOutput, pop3.StandardOutput);
    }

    [TestMethod]
    public async Task DeleOverPop3_ThenFetchOverImap_ExitsRemoteFileNotFoundAndTheStoreHoldsNothing()
    {
        var dataDirectory = NewTemporaryDirectory();
        UpstreamCurlRunResult delete;
        UpstreamCurlRunResult fetch;
        await using (var surl = await StartSurlAsync(dataDirectory, ["smtp", "imap", "pop3"]))
        {
            await SendMailAsync(surl, "smtp", [], Alice);
            delete = await RunUpstreamCurlAsync("-sS", "-u", CredentialsOf(Alice), "-X", "DELE 1", UrlOf(surl, "pop3", "/"));
            fetch = await ReadAsync(surl, "imap", [], Alice, "/INBOX;UID=1");
        }

        Assert.AreEqual(0, delete.ExitCode, delete.StandardError);
        Assert.IsEmpty(delete.StandardOutput);
        Assert.AreEqual(RemoteFileNotFound, fetch.ExitCode, fetch.StandardError);
        Assert.IsEmpty(fetch.StandardOutput);
        Assert.IsEmpty(await ReadInboxAsync(dataDirectory, Alice));
    }

    // The stored message is ADR-0053 decision 6's Return-Path and Received fields, then mail.txt,
    // and every read of it is those bytes exactly.
    private static void AssertIsTheDeliveredMessage(IReadOnlyList<byte[]> stored, string protocol, params byte[][] reads)
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
        foreach (var read in reads)
        {
            CollectionAssert.AreEqual(stored[0], read);
        }
    }

    private static string CredentialsOf(string account) => $"{account}:{Password}";

    private static string UrlOf(SurlOnLoopback surl, string scheme, string path)
    {
        var url = surl.BaseUrlOf(scheme);
        return $"{url.Scheme}://{url.Host}:{url.Port}{path}";
    }

    // curl delivers mail.txt as BL-210 does, logged in as alice; the URL ends /c so the trace
    // field names EHLO c.
    private async Task SendMailAsync(SurlOnLoopback surl, string scheme, string[] tlsOptions, params string[] recipients)
    {
        var mail = Path.Combine(NewTemporaryDirectory(), "mail.txt");
        await File.WriteAllBytesAsync(mail, Mail, TestContext.CancellationToken);
        var result = await RunUpstreamCurlAsync(
            [
                "-sS", .. tlsOptions, "-u", CredentialsOf(Alice), "--mail-from", "a@x",
                .. recipients.SelectMany(recipient => (string[])["--mail-rcpt", $"{recipient}@x"]),
                "-T", mail, UrlOf(surl, scheme, "/c"),
            ]);
        Assert.AreEqual(0, result.ExitCode, $"The {scheme} delivery failed: {result.StandardError}");
    }

    private Task<UpstreamCurlRunResult> ReadAsync(SurlOnLoopback surl, string scheme, string[] tlsOptions, string account, string path) =>
        RunUpstreamCurlAsync(["-sS", .. tlsOptions, "-u", CredentialsOf(account), UrlOf(surl, scheme, path)]);

    // surl over dataDirectory on every scheme, with a --user-file holding alice and bob.
    private async Task<SurlOnLoopback> StartSurlAsync(string dataDirectory, string[] schemes, params string[] options)
    {
        var accounts = Path.Combine(NewTemporaryDirectory(), "users.txt");
        await File.WriteAllTextAsync(accounts, $"{CredentialsOf(Alice)}\n{CredentialsOf(Bob)}\n", TestContext.CancellationToken);
        return await SurlOnLoopback.StartOverDirectoryAsync(
            schemes, dataDirectory, [.. options, "--user-file", accounts], TestContext.CancellationToken);
    }

    private string NewTemporaryDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-conformance-mail-").FullName;
        temporaryDirectories.Add(directory);
        return directory;
    }

    private Task<IReadOnlyList<byte[]>> ReadInboxAsync(string dataDirectory, string accountName) =>
        StoredMail.ReadInboxAsync(dataDirectory, accountName, TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunUpstreamCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunAsync(TestContext, arguments);
}
