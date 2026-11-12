using System.Text;
using Surl.Api;
using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

/// <summary>
/// Recipe: a test suite uses one <see cref="SurlServer"/> as the mail server behind the code it tests (BL-817).
/// The server runs <c>smtp</c>, <c>imap</c> and <c>pop3</c> on port 0 over one mail store; the pinned upstream
/// curl plays the code under test, sending over SMTP and reading over IMAP and POP3. Nothing sleeps or polls:
/// the test waits on the mail store's change event with <see cref="SurlServer.WaitForEventAsync"/>. Inconclusive
/// where no pinned build is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class StandInMailRoundTripRecipeTests
{
    private const string Domain = "example.test";
    private const string Ann = "ann@example.test";
    private const string Bob = "bob@example.test";
    private const string Password = "secret";
    private const string Message = "From: ann@example.test\r\nTo: bob@example.test\r\nSubject: recipe\r\n\r\nhello bob\r\n";

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

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
    public async Task SendOverSmtp_ThenReadOverImapAndPop3_ReturnsTheSameMessage()
    {
        // Arrange: one server, three mail protocols, one domain, two mailboxes; the store is shared.
        await using var server = await StartAsync();
        var messageFile = await WriteMessageFileAsync();

        // Act: the code under test sends bob a mail over SMTP; the test waits for the store's delivery event
        // instead of polling, then reads the mail back over two other protocols.
        var sent = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "--mail-from", Ann, "--mail-rcpt", Bob, "-T", messageFile, UrlOf(server, "smtp", "/c"));
        await server.WaitForEventAsync(MailChange(), WaitLimit, TestContext.CancellationToken);
        var overImap = await ReadAsync(server, "imap", "/INBOX;UID=1");
        var overPop3 = await ReadAsync(server, "pop3", "/1");

        // Assert: the delivery came in over SMTP and both readers return the message.
        Assert.AreEqual(0, sent.ExitCode, sent.StandardError);
        StringAssert.Contains(Encoding.ASCII.GetString(server.FindExchanges(new SurlEventQuery { Scheme = "smtp" }).Single().Received), "RCPT TO:<bob@example.test>");
        Assert.AreEqual(0, overImap.ExitCode, overImap.StandardError);
        Assert.AreEqual(0, overPop3.ExitCode, overPop3.StandardError);
        StringAssert.Contains(Encoding.UTF8.GetString(overImap.StandardOutput), "hello bob");
        StringAssert.Contains(Encoding.UTF8.GetString(overPop3.StandardOutput), "hello bob");
    }

    [TestMethod]
    public async Task SeedMessage_ThenReadOverPop3_ReturnsTheSeededMessage()
    {
        // Arrange: a server with bob's mailbox.
        await using var server = await StartAsync();

        // Act: the test seeds bob's inbox directly (no SMTP needed), then the code under test reads it over POP3.
        await server.Mail.DeliverAsync(Bob, Encoding.UTF8.GetBytes(Message), TestContext.CancellationToken);
        var read = await ReadAsync(server, "pop3", "/1");

        // Assert: the seeded text comes back.
        Assert.AreEqual(0, read.ExitCode, read.StandardError);
        StringAssert.Contains(Encoding.UTF8.GetString(read.StandardOutput), "hello bob");
    }

    [TestMethod]
    public async Task ResetAsync_AfterSeeding_LeavesTheMailboxEmptyForTheNextTest()
    {
        // Arrange: a server holding one seeded message.
        await using var server = await StartAsync();
        await server.Mail.DeliverAsync(Bob, Encoding.UTF8.GetBytes(Message), TestContext.CancellationToken);

        // Act: the test resets the stores between tests, and the code under test reads again.
        await server.ResetAsync(TestContext.CancellationToken);
        var read = await ReadAsync(server, "pop3", "/1");

        // Assert: the seeded message is gone.
        Assert.AreNotEqual(0, read.ExitCode, "the reset mailbox must not serve message 1");
        Assert.DoesNotContain("hello bob", Encoding.UTF8.GetString(read.StandardOutput));
    }

    private static SurlEventQuery MailChange() =>
        new() { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail };

    private static string UrlOf(SurlServer server, string scheme, string path)
    {
        var url = server.BoundUrls.Single(bound => bound.Scheme == scheme);
        return $"{scheme}://{url.Host}:{url.Port}{path}";
    }

    private Task<SurlServer> StartAsync()
    {
        var options = new SurlServerOptions { RecordExchanges = true, AllowAnonymous = true };
        foreach (var scheme in new[] { "smtp", "imap", "pop3" })
        {
            options.ListenUrls.Add($"{scheme}://127.0.0.1:0");
        }

        options.MailDomains.Add(Domain);
        options.Mailboxes.Add($"{Ann}:{Password}");
        options.Mailboxes.Add($"{Bob}:{Password}");
        return SurlServer.StartAsync(options, TestContext.CancellationToken);
    }

    private async Task<string> WriteMessageFileAsync()
    {
        var directory = Directory.CreateTempSubdirectory("surl-recipe-mail-").FullName;
        temporaryDirectories.Add(directory);
        var path = Path.Combine(directory, "message.eml");
        await File.WriteAllTextAsync(path, Message, TestContext.CancellationToken);
        return path;
    }

    private Task<UpstreamCurlRunResult> ReadAsync(SurlServer server, string scheme, string path) =>
        PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-u", $"{Bob}:{Password}", UrlOf(server, scheme, path));
}
