using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

[TestClass]
public sealed class CommandLineRunnerTlsTests
{
    private static readonly string NewLine = Environment.NewLine;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_HttpsWithoutCert_StartsAnHttpsListenerWithThrowawayTlsSettingsAndReturnsOkWhenCancelled()
    {
        var run = await ServeUntilListeningAsync("https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode);
        CollectionAssert.AreEqual(new[] { new ListenUrl("https", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.IsNotNull(run.TlsSettings);
        Assert.IsFalse(run.TlsSettings.RequiresClientCertificate);
        Assert.AreEqual($"Listening on https://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
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
    public async Task RunAsync_VerboseHttpsWithoutCert_NotesTheThrowawayCertificatesFingerprintOnce()
    {
        var run = await ServeUntilListeningAsync("-v", "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode);
        Assert.MatchesRegex(
            "^\\* Serving a throwaway certificate, SHA-256 [0-9A-F]{64}" + NewLine + "$", run.Error);
    }

    [TestMethod]
    public async Task RunAsync_VerboseHttpsWithCert_ServesTheCertificateWithoutTheThrowawayNote()
    {
        using var files = TestCertificateFiles.Create();

        var run = await ServeUntilListeningAsync("-v", "--cert", files.CertificateFile, "--key", files.KeyFile, "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode);
        Assert.IsNotNull(run.TlsSettings);
        Assert.AreEqual(string.Empty, run.Error);
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

        var run = await RunRefusedAsync("--cacert", files.MissingFile, "https://127.0.0.1:0/");

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

        var run = await RunRefusedAsync("--cacert", files.GarbageFile, "https://127.0.0.1:0/");

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
