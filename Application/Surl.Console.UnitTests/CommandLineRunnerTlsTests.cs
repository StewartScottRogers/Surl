using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

[TestClass]
public sealed class CommandLineRunnerTlsTests
{
    private static readonly string NewLine = Environment.NewLine;

    public TestContext TestContext { get; set; } = null!;

    private const string SelfSignedWarning =
        "surl: warning: --self-signed: serving a throwaway certificate; clients must skip verification (curl -k)";

    [TestMethod]
    [DataRow("https")]
    [DataRow("gophers")]
    [DataRow("mqtts")]
    public async Task RunAsync_ImplicitTlsWithoutCertOrSelfSigned_WritesNeedsACertificateAndReturnsCertificateProblemBindingNothing(string scheme)
    {
        var run = await RunRefusedAsync("http://127.0.0.1:0/", $"{scheme}://127.0.0.1:0/", $"{scheme}://[::1]:0/");

        Assert.AreEqual(SurlExitCode.CertificateProblem, run.ExitCode);
        Assert.AreEqual(
            $"surl: (58) {scheme}://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            run.Error);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public async Task RunAsync_SilentImplicitTlsWithoutCertOrSelfSigned_ReturnsCertificateProblemWritingNothing()
    {
        var run = await RunRefusedAsync("-s", "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CertificateProblem, run.ExitCode);
        Assert.AreEqual(string.Empty, run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    [DataRow("https")]
    [DataRow("gophers")]
    [DataRow("mqtts")]
    public async Task RunAsync_ImplicitTlsWithSelfSigned_ServesAThrowawayCertificateAndWritesTheWarning(string scheme)
    {
        var run = await ServeUntilListeningAsync("--self-signed", $"{scheme}://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode);
        CollectionAssert.AreEqual(new[] { new ListenUrl(scheme, "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.IsNotNull(run.TlsSettings);
        Assert.IsFalse(run.TlsSettings.RequiresClientCertificate);
        Assert.AreEqual($"Listening on {scheme}://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        Assert.AreEqual(SelfSignedWarning + NewLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_SelfSignedTwoStarts_WritesTheWarningOnEachStart()
    {
        var first = await ServeUntilListeningAsync("--self-signed", "https://127.0.0.1:0/");
        var second = await ServeUntilListeningAsync("--self-signed", "https://127.0.0.1:0/");

        Assert.AreEqual(SelfSignedWarning + NewLine, first.Error);
        Assert.AreEqual(SelfSignedWarning + NewLine, second.Error);
    }

    [TestMethod]
    public async Task RunAsync_VerboseSelfSigned_WritesTheWarningThenTheThrowawayCertificatesFingerprintOnce()
    {
        var run = await ServeUntilListeningAsync("-v", "--self-signed", "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode);
        Assert.MatchesRegex(
            "^" + System.Text.RegularExpressions.Regex.Escape(SelfSignedWarning) + NewLine
            + "\\* Serving a throwaway certificate, SHA-256 [0-9A-F]{64}" + NewLine + "$",
            run.Error);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("-sS")]
    public async Task RunAsync_SilentSelfSigned_ServesTheThrowawayCertificateWithoutTheWarning(string level)
    {
        var run = await ServeUntilListeningAsync(level, "--self-signed", "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode);
        Assert.IsNotNull(run.TlsSettings);
        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_HttpOnly_CreatesTheListenerFactoryWithoutTlsSettings()
    {
        var run = await ServeUntilListeningAsync("http://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode);
        Assert.IsTrue(run.FactoryCreated);
        Assert.IsNull(run.TlsSettings);
    }

    [TestMethod]
    public async Task RunAsync_HttpOnlyWithSelfSigned_MakesNoCertificateAndWritesNoWarning()
    {
        var run = await ServeUntilListeningAsync("-v", "--self-signed", "http://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode);
        Assert.IsNull(run.TlsSettings);
        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    [DataRow("https")]
    [DataRow("gophers")]
    [DataRow("mqtts")]
    public async Task RunAsync_VerboseImplicitTlsWithCert_ServesTheCertificateWithoutTheWarningOrTheThrowawayNote(string scheme)
    {
        using var files = TestCertificateFiles.Create();

        var run = await ServeUntilListeningAsync(
            "-v", "--cert", files.CertificateFile, "--key", files.KeyFile, $"{scheme}://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode);
        Assert.IsNotNull(run.TlsSettings);
        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_SelfSignedWithCert_IsRefusedWithFailedInitBindingNothing()
    {
        using var files = TestCertificateFiles.Create();

        var run = await RunRefusedAsync("--self-signed", "--cert", files.CertificateFile, "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual(
            "surl: option --self-signed: cannot be used with --cert" + NewLine
            + "surl: try 'surl --help' or 'surl --manual' for more information" + NewLine,
            run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public void FormatMissingCertificate_IPv6ListenUrl_BracketsTheHostAndKeepsThePortAsGiven()
    {
        Assert.AreEqual(
            "(58) https://[::1]:8443/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one",
            CommandLineRunner.FormatMissingCertificate(new ListenUrl("https", "::1", 8443)));
    }

    [TestMethod]
    public async Task RunAsync_CertFileMissing_WritesCouldNotLoadTheServerCertificateAndReturnsCertificateProblem()
    {
        using var files = TestCertificateFiles.Create();

        var run = await RunRefusedAsync("--cert", files.MissingFile, "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CertificateProblem, run.ExitCode);
        Assert.AreEqual(
            $"surl: (58) Could not load the server certificate: Cannot read the --cert file '{files.MissingFile}'." + NewLine,
            run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public async Task RunAsync_CacertFileMissing_WritesCurlsTwoLinesAndReturnsFailedInit()
    {
        using var files = TestCertificateFiles.Create();

        var run = await RunRefusedAsync("--self-signed", "--cacert", files.MissingFile, "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual(
            $"surl: The file '{files.MissingFile}' provided to --cacert does not exist" + NewLine
            + "surl: option --cacert: is badly used here" + NewLine,
            run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public async Task RunAsync_CacertHoldsNoCertificate_WritesCouldNotLoadTheCaCertificatesAndReturnsCaCertificateBadFile()
    {
        using var files = TestCertificateFiles.Create();

        var run = await RunRefusedAsync("--self-signed", "--cacert", files.GarbageFile, "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CaCertificateBadFile, run.ExitCode);
        Assert.AreEqual(
            $"surl: (77) Could not load the CA certificates: The --cacert file '{files.GarbageFile}' holds no certificate Surl can read." + NewLine,
            run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public void CreateListenerFactory_AnySettings_CreatesASocketListenerFactory()
    {
        Assert.IsInstanceOfType<SocketListenerFactory>(Program.CreateListenerFactory(null));
    }

    private async Task<RunOutcome> ServeUntilListeningAsync(params string[] args)
    {
        var factory = new FakeListenerFactory();
        var outcome = new RunOutcome(factory);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(outcome.Create, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System)
            .RunAsync(args, output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        outcome.ExitCode = await running;
        outcome.Output = output.ToString();
        outcome.Error = error.ToString();

        return outcome;
    }

    private async Task<RunOutcome> RunRefusedAsync(params string[] args)
    {
        var outcome = new RunOutcome(new FakeListenerFactory());
        using var output = new StringWriter();
        using var error = new StringWriter();

        outcome.ExitCode = await new CommandLineRunner(outcome.Create, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System)
            .RunAsync(args, output, error, TestContext.CancellationToken);
        outcome.Output = output.ToString();
        outcome.Error = error.ToString();

        return outcome;
    }

    private sealed class RunOutcome(FakeListenerFactory factory)
    {
        public FakeListenerFactory Factory { get; } = factory;

        public bool FactoryCreated { get; private set; }

        public ServerTlsSettings? TlsSettings { get; private set; }

        public SurlExitCode ExitCode { get; set; }

        public string Output { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;

        public IListenerFactory Create(ServerTlsSettings? tlsSettings)
        {
            FactoryCreated = true;
            TlsSettings = tlsSettings;
            return Factory;
        }
    }
}
