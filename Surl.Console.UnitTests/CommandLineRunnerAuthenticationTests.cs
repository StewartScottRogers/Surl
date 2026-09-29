using System.Text;
using Surl.Authentication;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// The accounts, the authentication policy and the loosening-option warnings <c>surl</c>
/// composes (ADR-0032, sections 1, 2, 4, 5 and 9; ADR-0033, section 7).
/// </summary>
[TestClass]
public sealed class CommandLineRunnerAuthenticationTests
{
    private const string Http = "http://127.0.0.1:0/";
    private const string UserFile = "users.txt";

    private static readonly string NewLine = Environment.NewLine;

    // CONNECT (MQTT 3.1.1) with client identifier c, user name alice and password pw.
    private static readonly byte[] MqttConnectAsAlice =
    [
        0x10, 0x18, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, 0xC2, 0x00, 0x3C,
        0x00, 0x01, (byte)'c',
        0x00, 0x05, (byte)'a', (byte)'l', (byte)'i', (byte)'c', (byte)'e',
        0x00, 0x02, (byte)'p', (byte)'w',
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_UserFileThatIsMissing_WritesCouldNotReadAndReturnsCouldNotReadFileBeforeAnyListenerBinds()
    {
        var run = await RunRefusedAsync(_ => throw new FileNotFoundException("gone"), "--user-file", UserFile, Http);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, run.ExitCode);
        Assert.AreEqual("surl: (37) Could not read user file users.txt" + NewLine, run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_UserFileThatIsUnreadable_WritesCouldNotReadAndReturnsCouldNotReadFileBeforeAnyListenerBinds()
    {
        var run = await RunRefusedAsync(_ => throw new UnauthorizedAccessException("denied"), "--user-file", UserFile, Http);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, run.ExitCode);
        Assert.AreEqual("surl: (37) Could not read user file users.txt" + NewLine, run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_UserFileThatIsADirectory_WritesCouldNotReadAndReturnsCouldNotReadFileBeforeAnyListenerBinds()
    {
        var run = await RunRefusedAsync(_ => throw new IOException("is a directory"), "--user-file", "accounts", Http);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, run.ExitCode);
        Assert.AreEqual("surl: (37) Could not read user file accounts" + NewLine, run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_MalformedUserFile_WritesTheLineAndItsFaultAndReturnsFailedInitBeforeAnyListenerBinds()
    {
        var run = await RunRefusedAsync(ReadsAs("alice:pw\nbob\n"), "--user-file", UserFile, Http);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual("surl: (2) User file users.txt, line 2: expected <user:password>" + NewLine, run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_UserFileNamingAUserTheUserOptionGave_WritesGivenTwiceAndReturnsFailedInit()
    {
        var run = await RunRefusedAsync(ReadsAs("alice:other\n"), "--user", "alice:pw", "--user-file", UserFile, Http);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual("surl: (2) User file users.txt, line 1: user alice is given twice" + NewLine, run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    [DataRow("negotiate")]
    [DataRow("aws-sigv4")]
    public async Task RunAsync_AuthWordThisBuildDoesNotImplement_WritesNotAvailableAndReturnsFailedInitBeforeAnyListenerBinds(string word)
    {
        var run = await RunRefusedAsync(ReadsAs(string.Empty), "--auth", $"basic,{word}", Http);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual($"surl: (2) --auth {word} is not available in this build" + NewLine, run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public void Compose_WithoutUserFile_NeverReadsAFile()
    {
        var (policy, exitCode, failureMessage) = AuthenticationComposition.Compose(
            Parse("--user", "alice:pw", Http), _ => throw new AssertFailedException("read"), TimeProvider.System);

        Assert.IsNotNull(policy);
        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsNull(failureMessage);
    }

    [TestMethod]
    public void ComposeSettings_UserOptionAndUserFileAccounts_BookHoldsEveryAccount()
    {
        var accounts = new[] { new Account("alice", "pw"), new Account("bob", "pw2") };

        var settings = AuthenticationComposition.ComposeSettings(Parse(Http), accounts);

        Assert.IsTrue(settings.Accounts.CheckPassword("alice", "pw"u8));
        Assert.IsTrue(settings.Accounts.CheckPassword("bob", "pw2"u8));
        Assert.IsFalse(settings.Accounts.CheckPassword("bob", "pw"u8));
    }

    [TestMethod]
    public void ComposeSettings_NoLooseningOption_IsSecureByDefaultWithTheDefaultMethods()
    {
        var settings = AuthenticationComposition.ComposeSettings(Parse(Http), []);

        Assert.IsFalse(settings.Accounts.HasAccounts);
        Assert.IsFalse(settings.AllowAnonymous);
        Assert.IsFalse(settings.AllowPlaintextAuth);
        Assert.AreSame(AuthenticationMethods.DefaultAccepted, settings.AcceptedMethods);
    }

    [TestMethod]
    public void ComposeSettings_EveryLooseningOption_CarriesEachIntoTheSettings()
    {
        var settings = AuthenticationComposition.ComposeSettings(
            Parse("--allow-anonymous", "--allow-plaintext-auth", "--auth", "bearer,digest", Http), []);

        Assert.IsTrue(settings.AllowAnonymous);
        Assert.IsTrue(settings.AllowPlaintextAuth);
        Assert.IsTrue(settings.AcceptedMethods.SetEquals([AuthenticationMethod.Digest, AuthenticationMethod.Bearer]));
    }

    [TestMethod]
    public async Task RunAsync_NoAccountsAndBasicOverHttp_TheComposedHttpServerAnswers403()
    {
        var run = await ServeOneConnectionAsync(HttpGetWithBasic("alice:pw"), ReadsAs(string.Empty), Http);

        StringAssert.StartsWith(Encoding.ASCII.GetString(run.Written), "HTTP/1.1 403 Forbidden\r\n");
    }

    [TestMethod]
    public async Task RunAsync_UserFileAccountAndBasicOverHttps_TheComposedHttpsServerServesTheRequest()
    {
        var run = await ServeOneConnectionAsync(
            HttpGetWithBasic("bob:pw2"),
            ReadsAs("bob:pw2\n"),
            "--user", "alice:pw", "--user-file", UserFile, "--self-signed", "https://127.0.0.1:0/");

        var response = Encoding.ASCII.GetString(run.Written);
        StringAssert.StartsWith(response, "HTTP/1.1 ");
        Assert.DoesNotStartWith("HTTP/1.1 401", response);
        Assert.DoesNotStartWith("HTTP/1.1 403", response);
    }

    [TestMethod]
    public async Task RunAsync_UserOptionAccountAndNoLoginOverHttp_TheComposedHttpServerAnswers401()
    {
        var run = await ServeOneConnectionAsync(
            Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: x\r\n\r\n"), ReadsAs(string.Empty), "--user", "alice:pw", Http);

        StringAssert.StartsWith(Encoding.ASCII.GetString(run.Written), "HTTP/1.1 401 Unauthorized\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AuthNtlmAndNoLoginOverHttp_TheComposedHttpServerOffersNtlmAlone()
    {
        var run = await ServeOneConnectionAsync(
            Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: x\r\n\r\n"), ReadsAs(string.Empty), "--user", "alice:pw", "--auth", "ntlm", Http);

        var response = Encoding.ASCII.GetString(run.Written);
        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
        StringAssert.Contains(response, "\r\nWWW-Authenticate: NTLM\r\n");
        Assert.AreEqual(1, response.Split("WWW-Authenticate:").Length - 1, response);
    }

    [TestMethod]
    public async Task RunAsync_NoAccountsAndMqttCredentialsOverMqtt_TheComposedMqttServerAnswersNotAuthorized()
    {
        var run = await ServeOneConnectionAsync(MqttConnectAsAlice, ReadsAs(string.Empty), "mqtt://127.0.0.1:0/");

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, 0x05 }, run.Written);
    }

    [TestMethod]
    public async Task RunAsync_UserFileAccountAndPlaintextAuthOverMqtt_TheComposedMqttServerAccepts()
    {
        var run = await ServeOneConnectionAsync(
            MqttConnectAsAlice, ReadsAs("alice:pw\n"), "--user-file", UserFile, "--allow-plaintext-auth", "mqtt://127.0.0.1:0/");

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, 0x00 }, run.Written);
    }

    [TestMethod]
    [DataRow("--allow-anonymous", "surl: warning: --allow-anonymous: every request and login is accepted without checking credentials")]
    [DataRow("--allow-plaintext-auth", "surl: warning: --allow-plaintext-auth: passwords and tokens are accepted over unencrypted connections")]
    public async Task RunAsync_LooseningSwitch_WritesItsWarningLineToTheLogOnStart(string option, string warning)
    {
        var run = await ServeOneConnectionAsync(null, ReadsAs(string.Empty), option, Http);

        Assert.AreEqual(warning + NewLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_Auth_WritesTheAcceptedMethodsInSectionThreesOrderOnStart()
    {
        var run = await ServeOneConnectionAsync(null, ReadsAs(string.Empty), "--auth", "BEARER,basic,digest", Http);

        Assert.AreEqual("surl: warning: --auth: accepted methods are digest, basic, bearer" + NewLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_EveryLooseningOptionWithSelfSigned_WritesTheWarningsInTheirOrderBeforeTheListeningLine()
    {
        var run = await ServeOneConnectionAsync(
            null,
            ReadsAs(string.Empty),
            "--self-signed", "--auth", "basic", "--allow-plaintext-auth", "--allow-anonymous", "https://127.0.0.1:0/");

        Assert.AreEqual(
            "surl: warning: --allow-anonymous: every request and login is accepted without checking credentials" + NewLine
            + "surl: warning: --allow-plaintext-auth: passwords and tokens are accepted over unencrypted connections" + NewLine
            + "surl: warning: --auth: accepted methods are basic" + NewLine
            + "surl: warning: --self-signed: serving a throwaway certificate; clients must skip verification (curl -k)" + NewLine,
            run.Error);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-S")]
    public async Task RunAsync_LooseningOptionAtInfoOrAbove_WritesItsWarning(string levelOption)
    {
        var run = await ServeOneConnectionAsync(null, ReadsAs(string.Empty), levelOption, "--allow-anonymous", Http);

        StringAssert.StartsWith(run.Error, "surl: warning: --allow-anonymous: ");
    }

    [TestMethod]
    [DataRow(new[] { "-s" })]
    [DataRow(new[] { "-s", "-S" })]
    public async Task RunAsync_SilentWithEveryLooseningOption_WritesNoWarning(string[] levelOptions)
    {
        var run = await ServeOneConnectionAsync(
            null,
            ReadsAs(string.Empty),
            [.. levelOptions, "--allow-anonymous", "--allow-plaintext-auth", "--auth", "basic", Http]);

        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_NoLooseningOption_WritesNoWarning()
    {
        var run = await ServeOneConnectionAsync(null, ReadsAs(string.Empty), "--user", "alice:pw", Http);

        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_RealUserFile_ItsAccountReachesTheComposedHttpServer()
    {
        var path = Path.Combine(Path.GetTempPath(), $"surl-users-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "# accounts\nalice:pw\n", TestContext.CancellationToken);
        try
        {
            var run = await ServeOneConnectionAsync(
                Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: x\r\nAuthorization: Basic YWxpY2U6cHc=\r\n\r\n"),
                readUserFile: null,
                "--user-file", path, "--allow-plaintext-auth", Http);

            Assert.DoesNotStartWith("HTTP/1.1 401", Encoding.ASCII.GetString(run.Written));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_RealUserFileThatIsMissing_ReturnsCouldNotReadFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"surl-missing-{Guid.NewGuid():N}.txt");

        var run = await RunRefusedAsync(null, "--user-file", path, Http);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, run.ExitCode);
        Assert.AreEqual($"surl: (37) Could not read user file {path}" + NewLine, run.Error);
    }

    private static Func<string, byte[]> ReadsAs(string content) => path =>
    {
        Assert.AreEqual(UserFile, path);
        return Encoding.UTF8.GetBytes(content);
    };

    private static byte[] HttpGetWithBasic(string userColonPassword) => Encoding.ASCII.GetBytes(
        $"GET / HTTP/1.1\r\nHost: x\r\nAuthorization: Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(userColonPassword))}\r\n\r\n");

    private static SurlCommandLine Parse(params string[] args) => CommandLineParser.Parse(args).CommandLine!;

    private async Task<Run> RunRefusedAsync(Func<string, byte[]>? readUserFile, params string[] args)
    {
        var factory = new FakeListenerFactory();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CreateRunner(factory, readUserFile).RunAsync(args, output, error, TestContext.CancellationToken);
        return new Run(factory, exitCode, error.ToString(), []);
    }

    // Serves until the connection, when there is one, has ended, then stops.
    private async Task<Run> ServeOneConnectionAsync(byte[]? request, Func<string, byte[]>? readUserFile, params string[] args)
    {
        var connection = request is null ? null : new FakeConnection(request);
        var factory = new FakeListenerFactory { Connection = connection };
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(factory, readUserFile).RunAsync(args, output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        if (connection is not null)
        {
            await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        }

        await stop.CancelAsync();
        var exitCode = await running;
        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        return new Run(factory, exitCode, error.ToString(), connection?.WrittenBytes ?? []);
    }

    private static CommandLineRunner CreateRunner(FakeListenerFactory factory, Func<string, byte[]>? readUserFile) =>
        new(_ => factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System, readUserFile: readUserFile);

    private sealed record Run(FakeListenerFactory Factory, SurlExitCode ExitCode, string Error, byte[] Written);
}
