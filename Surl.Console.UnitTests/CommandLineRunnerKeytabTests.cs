using Surl.Authentication;
using Surl.Cli;
using Surl.Kerberos;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// <c>--keytab</c> (ADR-0057, decision 1): the keytab read before any listener binds, its three
/// refusals, its two warnings, <c>--auth gssapi</c>'s need for it, and the Kerberos acceptor it
/// puts on the composed <see cref="AuthenticationSettings"/> (decision 6).
/// </summary>
[TestClass]
public sealed class CommandLineRunnerKeytabTests
{
    private const string Http = "http://127.0.0.1:0/";
    private const string Keytab = "http.keytab";
    private const string AuthNegotiateWarning = "surl: warning: --auth: accepted methods are negotiate";
    private const string UnusedWarning = "surl: warning: --keytab is unused: --auth accepts neither negotiate nor gssapi";

    private static readonly string NewLine = Environment.NewLine;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_KeytabThatIsMissing_WritesCouldNotReadAndReturnsCouldNotReadFileBeforeAnyListenerBinds()
    {
        var run = await RunRefusedAsync(_ => throw new FileNotFoundException("gone"), "--auth", "negotiate", "--keytab", Keytab, Http);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, run.ExitCode);
        Assert.AreEqual("surl: (37) Could not read keytab http.keytab" + NewLine, run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_KeytabThatIsUnreadable_WritesCouldNotReadAndReturnsCouldNotReadFile()
    {
        var run = await RunRefusedAsync(_ => throw new UnauthorizedAccessException("denied"), "--keytab", Keytab, Http);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, run.ExitCode);
        Assert.AreEqual("surl: (37) Could not read keytab http.keytab" + NewLine, run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_KeytabThatIsADirectory_WritesCouldNotReadAndReturnsCouldNotReadFile()
    {
        var run = await RunRefusedAsync(_ => throw new IOException("is a directory"), "--keytab", "keys", Http);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, run.ExitCode);
        Assert.AreEqual("surl: (37) Could not read keytab keys" + NewLine, run.Error);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x05, 0x01, 0x00, 0x00, 0x00, 0x00 }, 0, DisplayName = "Version 0x0501")]
    [DataRow(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x40, 0x00, 0x02 }, 2, DisplayName = "An entry running past the end")]
    public async Task RunAsync_MalformedKeytab_WritesTheOffsetAndReturnsFailedInitBeforeAnyListenerBinds(byte[] content, int offset)
    {
        var run = await RunRefusedAsync(_ => content, "--auth", "negotiate", "--keytab", Keytab, Http);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual($"surl: (2) Keytab http.keytab is malformed at byte {offset}" + NewLine, run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_KeytabHoldingOnlyRc4Hmac_WritesHoldsNoKeyAndReturnsFailedInitBeforeAnyListenerBinds()
    {
        var run = await RunRefusedAsync(_ => TestKeytabFiles.Rc4Only, "--auth", "negotiate", "--keytab", Keytab, Http);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual("surl: (2) Keytab http.keytab holds no key surl can use" + NewLine, run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_KeytabWithAnAesAndAnRc4HmacKey_StartsAndWarnsOfTheSkippedKey()
    {
        var run = await RunUntilListeningAsync(_ => TestKeytabFiles.Rc4ThenAes, "--auth", "negotiate", "--keytab", Keytab, Http);

        Assert.AreEqual(
            AuthNegotiateWarning + NewLine
                + $"surl: warning: --keytab: skipped the rc4-hmac key of {TestKeytabFiles.Principal}" + NewLine,
            run.Error);
    }

    [TestMethod]
    [DataRow("gssapi")]
    [DataRow("plain,GSSAPI")]
    public async Task RunAsync_AuthGssapiWithoutKeytab_WritesNeedsKeytabAndReturnsFailedInitBeforeAnyListenerBinds(string words)
    {
        var run = await RunRefusedAsync(_ => throw new AssertFailedException("no file is read"), "--auth", words, Http);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual("surl: (2) --auth gssapi needs --keytab" + NewLine, run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_AuthGssapiWithKeytab_IsStillNotAvailableAndReadsNoFile()
    {
        var run = await RunRefusedAsync(_ => throw new AssertFailedException("no file is read"), "--auth", "gssapi", "--keytab", Keytab, Http);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual("surl: (2) --auth gssapi is not available in this build" + NewLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_KeytabWithTheDefaultAuth_StartsAndWarnsItIsUnused()
    {
        var run = await RunUntilListeningAsync(_ => TestKeytabFiles.AesOnly, "--keytab", Keytab, Http);

        Assert.AreEqual(UnusedWarning + NewLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_KeytabWithAnAuthNamingNeitherKerberosMethod_WarnsAfterTheSkippedKeys()
    {
        var run = await RunUntilListeningAsync(_ => TestKeytabFiles.Rc4ThenAes, "--auth", "basic", "--keytab", Keytab, Http);

        Assert.AreEqual(
            "surl: warning: --auth: accepted methods are basic" + NewLine
                + $"surl: warning: --keytab: skipped the rc4-hmac key of {TestKeytabFiles.Principal}" + NewLine
                + UnusedWarning + NewLine,
            run.Error);
    }

    [TestMethod]
    public async Task RunAsync_AuthNegotiateWithoutKeytab_StartsWithNoNewWarning()
    {
        var run = await RunUntilListeningAsync(_ => throw new AssertFailedException("no file is read"), "--auth", "negotiate", Http);

        Assert.AreEqual(AuthNegotiateWarning + NewLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_KeytabBelowTheInfoLevel_WritesNoKeytabWarning()
    {
        var run = await RunUntilListeningAsync(_ => TestKeytabFiles.Rc4ThenAes, "-s", "-S", "--keytab", Keytab, Http);

        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_KeytabAtTheVerboseLevel_WritesNoKeyByte()
    {
        var run = await RunUntilListeningAsync(_ => TestKeytabFiles.Rc4ThenAes, "-v", "--auth", "negotiate", "--keytab", Keytab, Http);

        var written = run.Output + run.Error;
        foreach (var key in new[] { TestKeytabFiles.AesKey, TestKeytabFiles.Rc4Key })
        {
            Assert.DoesNotContain(Convert.ToHexString(key), written, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Convert.ToBase64String(key), written);
            Assert.DoesNotContain(Convert.ToHexString(key.AsSpan(0, 4)), written, StringComparison.OrdinalIgnoreCase);
        }
    }

    [TestMethod]
    public void Compose_WithKeytab_SettingsCarryAKerberosAcceptor()
    {
        var (policy, _, skipped, exitCode, _) = AuthenticationComposition.Compose(
            Parse("--auth", "negotiate", "--keytab", Keytab, Http), _ => TestKeytabFiles.Rc4ThenAes, TimeProvider.System);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsNotNull(policy!.Settings.KerberosAcceptor);
        Assert.AreEqual(new KerberosKeytabSkippedEntry(new KerberosPrincipalName("EXAMPLE.COM", ["HTTP", "www.example.com"]), 23), skipped.Single());
    }

    [TestMethod]
    public void Compose_WithoutKeytab_SettingsCarryNoKerberosAcceptorAndReadNoFile()
    {
        var (policy, _, skipped, _, _) = AuthenticationComposition.Compose(
            Parse("--auth", "negotiate", Http), _ => throw new AssertFailedException("no file is read"), TimeProvider.System);

        Assert.IsNull(policy!.Settings.KerberosAcceptor);
        Assert.IsEmpty(skipped);
    }

    [TestMethod]
    public void WriteStartLines_AuthGssapi_WritesNoUnusedWarning()
    {
        using var log = new StringWriter();

        KeytabComposition.WriteStartLines(Parse("--auth", "gssapi", "--keytab", Keytab, Http), [], log);

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    [DataRow(1, "des-cbc-crc")]
    [DataRow(2, "des-cbc-md4")]
    [DataRow(3, "des-cbc-md5")]
    [DataRow(16, "des3-cbc-sha1")]
    [DataRow(23, "rc4-hmac")]
    [DataRow(24, "rc4-hmac-exp")]
    [DataRow(25, "camellia128-cts-cmac")]
    [DataRow(26, "camellia256-cts-cmac")]
    [DataRow(99, "enctype 99")]
    public void NameEncryptionType_IsMitKrb5sNameOrTheNumber(int encryptionType, string name) =>
        Assert.AreEqual(name, KeytabComposition.NameEncryptionType(encryptionType));

    [TestMethod]
    public void RandomKerberosRandomSource_Fill_WritesRandomBytes()
    {
        var bytes = new byte[32];

        new RandomKerberosRandomSource().Fill(bytes);

        Assert.IsTrue(bytes.Any(value => value != 0));
    }

    private static SurlCommandLine Parse(params string[] args) => CommandLineParser.Parse(args).CommandLine!;

    private static CommandLineRunner CreateRunner(FakeListenerFactory factory, Func<string, byte[]> readStartFile) =>
        new(_ => factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System, readStartFile: readStartFile);

    private async Task<Run> RunRefusedAsync(Func<string, byte[]> readStartFile, params string[] args)
    {
        var factory = new FakeListenerFactory();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CreateRunner(factory, readStartFile).RunAsync(args, output, error, TestContext.CancellationToken);
        return new Run(factory, exitCode, output.ToString(), error.ToString());
    }

    // Serves until every listener has started, then stops.
    private async Task<Run> RunUntilListeningAsync(Func<string, byte[]> readStartFile, params string[] args)
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(factory, readStartFile).RunAsync(args, output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;
        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        return new Run(factory, exitCode, output.ToString(), error.ToString());
    }

    private sealed record Run(FakeListenerFactory Factory, SurlExitCode ExitCode, string Output, string Error);
}
