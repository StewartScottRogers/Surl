using System.Text;
using Surl.Api;
using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

/// <summary>
/// Recipe: a test suite makes the stand-in fail on purpose to check its client's error handling (BL-818). Each test
/// adds a <see cref="SurlFault"/> to a <see cref="SurlServer"/>, lets the pinned upstream curl 8.21.0 play the code
/// under test, asserts the exit code curl returned (measured with that build) and waits for the
/// <see cref="SurlEventKind.FaultApplied"/> event rather than sleeping. Inconclusive where no pinned build is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class StandInFaultRecipeTests
{
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
    public async Task RefuseLogin_ForAlice_MakesCurlFailWith401AndExit22()
    {
        // Arrange: an HTTP server with an account for alice, and a rule refusing her logins whatever the password.
        var options = HttpOptions();
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        options.AuthenticationMethods = ["basic"];
        options.AllowPlaintextAuthentication = true;
        options.Faults.Add(SurlFault.RefuseLogin() with { Account = "alice" });
        await using var server = await SurlServer.StartAsync(options, TestContext.CancellationToken);

        // Act: the code under test logs in as alice with the right password; -f turns the 401 into an error.
        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-f", "-u", "alice:secret", UrlOf(server, "http", "/hello.txt"));
        await server.WaitForEventAsync(FaultApplied(), WaitLimit, TestContext.CancellationToken);

        // Assert: curl 8.21.0 exits 22 (HTTP error with -f) and the server recorded the fault.
        Assert.AreEqual(22, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "401");
    }

    [TestMethod]
    public async Task ProtocolError_OnSmtpRcptTo_MakesCurlFailWithTheChosenReply()
    {
        // Arrange: an SMTP server whose RCPT TO for one recipient is answered 451 4.3.0.
        var options = new SurlServerOptions { RecordExchanges = true, AllowAnonymous = true };
        options.ListenUrls.Add("smtp://127.0.0.1:0");
        options.MailDomains.Add("example.test");
        options.Mailboxes.Add("bob@example.test:secret");
        options.Faults.Add(SurlFault.ProtocolError("451 4.3.0") with { Scheme = "smtp", Command = "RCPT", Path = "bob@example.test" });
        await using var server = await SurlServer.StartAsync(options, TestContext.CancellationToken);
        var file = await WriteMessageFileAsync();

        // Act: the code under test sends to bob.
        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "--mail-from", "ann@example.test", "--mail-rcpt", "bob@example.test", "-T", file, UrlOf(server, "smtp", "/c"));
        await server.WaitForEventAsync(FaultApplied(), WaitLimit, TestContext.CancellationToken);

        // Assert: curl 8.21.0 exits 55 (send error) and reports the chosen reply.
        Assert.AreEqual(55, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "451");
    }

    [TestMethod]
    public async Task DropConnection_Once_FailsTheFirstCurlAndLetsTheRetrySucceed()
    {
        // Arrange: an HTTP server that drops the first connection before any byte, then behaves.
        var options = HttpOptions();
        options.Faults.Add(SurlFault.DropConnection() with { Scheme = "http", Times = 1 });
        await using var server = await SurlServer.StartAsync(options, TestContext.CancellationToken);
        var url = UrlOf(server, "http", "/hello.txt");

        // Act: the code under test fetches twice, the second time being its retry.
        var first = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", url);
        await server.WaitForEventAsync(FaultApplied(), WaitLimit, TestContext.CancellationToken);
        var retry = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", url);

        // Assert: curl 8.21.0 exits 56 (receive failure) or 55 (send failure), by which side sees the reset first, on the dropped connection; the retry gets the file.
        Assert.IsTrue(first.ExitCode is 55 or 56, first.StandardError);
        Assert.AreEqual(0, retry.ExitCode, retry.StandardError);
        Assert.AreEqual("hello\n", Encoding.UTF8.GetString(retry.StandardOutput));
    }

    private static SurlEventQuery FaultApplied() => new() { Kind = SurlEventKind.FaultApplied };

    private static SurlServerOptions HttpOptions()
    {
        var options = new SurlServerOptions { RecordExchanges = true };
        options.ListenUrls.Add("http://127.0.0.1:0");
        options.Files["hello.txt"] = "hello\n"u8.ToArray();
        return options;
    }

    private static string UrlOf(SurlServer server, string scheme, string path)
    {
        var url = server.BoundUrls.Single(bound => bound.Scheme == scheme);
        return $"{scheme}://{url.Host}:{url.Port}{path}";
    }

    private async Task<string> WriteMessageFileAsync()
    {
        var directory = Directory.CreateTempSubdirectory("surl-recipe-fault-").FullName;
        temporaryDirectories.Add(directory);
        var path = Path.Combine(directory, "message.eml");
        await File.WriteAllTextAsync(path, "From: ann@example.test\r\nTo: bob@example.test\r\nSubject: hi\r\n\r\nhello bob\r\n", TestContext.CancellationToken);
        return path;
    }
}
