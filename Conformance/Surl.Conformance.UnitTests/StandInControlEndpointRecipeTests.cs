using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Recipe: the whole loop of a test suite that is not written in .NET, with nothing but <c>surl</c> and an HTTP
/// client (BL-819). <c>surl</c> runs as a process with <c>smtp</c>, <c>imap</c>, a mailbox and the loopback control
/// endpoint (<c>--control</c>, <c>--control-token-file</c>); the pinned upstream curl plays the test suite, driving
/// the control endpoint's <c>/v1/</c> requests and reading the mail over <c>imap://</c>. Nothing sleeps or polls:
/// the test waits on the control endpoint's <c>GET /v1/events/wait</c>. Inconclusive where no pinned build is
/// installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class StandInControlEndpointRecipeTests
{
    // CURLE_LOGIN_DENIED: the IMAP login is refused.
    private const int LoginDenied = 67;

    private const string Mailbox = "ann@example.test";

    private const string Password = "secret";

    // The control token: 32 to 1024 visible ASCII bytes on the first line of the token file.
    private const string Token = "recipe-control-token-0123456789abcdef";

    private static readonly string Mail = "From: a@x\r\nSubject: seeded\r\n\r\nhello from the seed\r\n";

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
    public async Task SeedMail_ThenReadOverImap_ReturnsTheSeededMessage()
    {
        await using var surl = await StartAsync();

        // 1. The suite seeds ann's inbox through the control endpoint: no SMTP, no .NET.
        var seed = await SeedMailAsync(surl);
        // 2. The code under test reads it over IMAP.
        var read = await ReadInboxAsync(surl);

        Assert.AreEqual(0, seed.ExitCode, seed.StandardError);
        Assert.AreEqual(0, read.ExitCode, read.StandardError);
        StringAssert.Contains(Encoding.UTF8.GetString(read.StandardOutput), "hello from the seed");
    }

    [TestMethod]
    public async Task RefuseLoginFault_ThenReadOverImap_FailsWithLoginDeniedUntilTheFaultIsRemoved()
    {
        await using var surl = await StartAsync();
        await SeedMailAsync(surl);

        // The suite makes ann's login fail on purpose; the fault grammar is --fault's.
        var fault = await ControlAsync(surl, "POST", "/v1/faults", "-H", "Content-Type: text/plain", "-d", $"refuse-login;account={Mailbox}");
        var refused = await ReadInboxAsync(surl);
        var faultId = Encoding.UTF8.GetString(fault.StandardOutput).Split('"')[3];
        var removed = await ControlAsync(surl, "DELETE", $"/v1/faults/{faultId}");
        var read = await ReadInboxAsync(surl);

        Assert.AreEqual(0, fault.ExitCode, fault.StandardError);
        StringAssert.Contains(Encoding.UTF8.GetString(fault.StandardOutput), "fault_id");
        Assert.AreEqual(LoginDenied, refused.ExitCode, refused.StandardError);
        Assert.AreEqual(0, removed.ExitCode, removed.StandardError);
        Assert.AreEqual(0, read.ExitCode, read.StandardError);
    }

    [TestMethod]
    public async Task WaitForEvent_AfterTheImapExchange_ReturnsThatExchange()
    {
        await using var surl = await StartAsync();
        await SeedMailAsync(surl);
        await ReadInboxAsync(surl);

        // The bounded wait returns the first IMAP exchange_ended event, or {"timed_out":true} after timeout_ms.
        var waited = await ControlAsync(surl, "GET", "/v1/events/wait?kind=exchange_ended&scheme=imap&timeout_ms=10000");

        Assert.AreEqual(0, waited.ExitCode, waited.StandardError);
        var body = Encoding.UTF8.GetString(waited.StandardOutput);
        StringAssert.Contains(body, "exchange_ended");
        Assert.DoesNotContain("timed_out", body);
    }

    [TestMethod]
    public async Task Reset_AfterSeeding_LeavesTheMailboxEmpty()
    {
        await using var surl = await StartAsync();
        await SeedMailAsync(surl);

        var reset = await ControlAsync(surl, "POST", "/v1/reset");
        var read = await ReadInboxAsync(surl);

        Assert.AreEqual(0, reset.ExitCode, reset.StandardError);
        // UID 1 is gone, so the fetch finds no message (or no body): the seeded text is not there.
        Assert.DoesNotContain("hello from the seed", Encoding.UTF8.GetString(read.StandardOutput));
        Assert.AreNotEqual(0, read.ExitCode, "the reset mailbox must not serve UID 1");
    }

    [TestMethod]
    public async Task ControlRequestWithoutTheToken_IsRefusedWith401()
    {
        await using var surl = await StartAsync();

        var refused = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-s", "-o", "-", "-w", "%{http_code}", "-X", "POST", $"{surl.ControlUrl!.AbsoluteUri}v1/reset");

        Assert.AreEqual(0, refused.ExitCode, refused.StandardError);
        Assert.AreEqual("401", Encoding.UTF8.GetString(refused.StandardOutput).Trim().Split('\n')[^1].Trim()[^3..]);
    }

    private async Task<SurlOnLoopback> StartAsync()
    {
        var directory = Directory.CreateTempSubdirectory("surl-conformance-control-").FullName;
        temporaryDirectories.Add(directory);
        var tokenFile = Path.Combine(directory, "control.token");
        await File.WriteAllTextAsync(tokenFile, Token + "\n", TestContext.CancellationToken);

        return await SurlOnLoopback.StartInMemoryWithControlAsync(
            ["smtp", "imap"],
            ["--mail-domain", "example.test", "--mailbox", $"{Mailbox}:{Password}"],
            tokenFile,
            TestContext.CancellationToken);
    }

    private async Task<UpstreamCurlRunResult> SeedMailAsync(SurlOnLoopback surl)
    {
        var message = Path.Combine(temporaryDirectories[^1], "seed.eml");
        await File.WriteAllTextAsync(message, Mail, TestContext.CancellationToken);

        return await ControlAsync(
            surl, "POST", $"/v1/mail?to={Mailbox}", "-H", "Content-Type: message/rfc822", "--data-binary", $"@{message}");
    }

    private Task<UpstreamCurlRunResult> ControlAsync(SurlOnLoopback surl, string method, string path, params string[] more) =>
        PinnedUpstreamCurl.RunAsync(
            TestContext,
            [
                "-sS", "--fail-with-body", "-X", method, "-H", $"Authorization: Bearer {Token}", .. more,
                $"{surl.ControlUrl!.AbsoluteUri.TrimEnd('/')}{path}",
            ]);

    private Task<UpstreamCurlRunResult> ReadInboxAsync(SurlOnLoopback surl)
    {
        var imap = surl.BaseUrlOf("imap");

        return PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-u", $"{Mailbox}:{Password}", $"imap://{imap.Host}:{imap.Port}/INBOX;UID=1");
    }
}
