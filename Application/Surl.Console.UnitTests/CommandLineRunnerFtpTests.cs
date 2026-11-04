using System.Net;
using System.Text;
using Surl.Cli;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> composes the FTP server (BL-182): <c>ftp</c> and, TLS from
/// the first byte, <c>ftps</c>, both answered by the one server (ADR-0052 decision 5); <c>AUTH
/// TLS</c> offered once a certificate is configured; logins judged by the one policy; and the
/// data connections opened through the opener the runner creates with the process's TLS settings
/// (ADR-0052 decision 9). No test here touches the disk or the network.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerFtpTests
{
    private const string Greeting = "220 surl FTP server ready\r\n";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly byte[] Quit = Encoding.ASCII.GetBytes("QUIT\r\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_FtpListenUrl_StartsAListenerAnsweredByTheFtpServer()
    {
        var run = await ServeOneConnectionAsync(Quit, null, "ftp://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("ftp", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.AreEqual($"Listening on ftp://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        StringAssert.StartsWith(run.Written, Greeting);
        Assert.IsNull(run.TlsSettings);
    }

    [TestMethod]
    public async Task RunAsync_FtpsListenUrlWithSelfSigned_StartsASecuredListenerAnsweredByTheFtpServer()
    {
        var run = await ServeOneConnectionAsync(Quit, null, "-s", "--self-signed", "ftps://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("ftps", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.IsNotNull(run.TlsSettings);
        StringAssert.StartsWith(run.Written, Greeting);
    }

    [TestMethod]
    public async Task RunAsync_FtpsWithoutCertOrSelfSigned_WritesNeedsACertificateAndReturnsCertificateProblem()
    {
        var factory = new FakeListenerFactory();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new CommandLineRunner(_ => factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System)
            .RunAsync(["ftps://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        Assert.AreEqual(
            "surl: (58) ftps://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            error.ToString());
        Assert.IsEmpty(factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_FtpWithSelfSigned_MakesACertificateAndOffersAuthTls()
    {
        var run = await ServeOneConnectionAsync(Encoding.ASCII.GetBytes("FEAT\r\nQUIT\r\n"), null, "-s", "--self-signed", "ftp://127.0.0.1:0/");

        Assert.IsNotNull(run.TlsSettings);
        StringAssert.Contains(run.Written, " AUTH TLS\r\n");
    }

    [TestMethod]
    public async Task RunAsync_FtpWithoutACertificate_MakesNoTlsSettingsAndAnswersAuthTls534()
    {
        var run = await ServeOneConnectionAsync(Encoding.ASCII.GetBytes("FEAT\r\nAUTH TLS\r\nQUIT\r\n"), null, "ftp://127.0.0.1:0/");

        Assert.IsNull(run.TlsSettings);
        Assert.DoesNotContain(" AUTH TLS\r\n", run.Written);
        StringAssert.Contains(run.Written, "\r\n534 ");
    }

    [TestMethod]
    public async Task RunAsync_AccountOverPlainFtp_RefusesThePasswordUncheckedWith530()
    {
        var run = await ServeOneConnectionAsync(
            Encoding.ASCII.GetBytes("USER alice\r\nPASS secret\r\nQUIT\r\n"), null, "-s", "-u", "alice:secret", "ftp://127.0.0.1:0/");

        StringAssert.Contains(run.Written, "530 Login needs TLS first: send AUTH TLS\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AccountWithAllowPlaintextAuth_LogsInWith230()
    {
        var run = await ServeOneConnectionAsync(
            Encoding.ASCII.GetBytes("USER alice\r\nPASS secret\r\nQUIT\r\n"),
            null,
            "-s",
            "--allow-plaintext-auth",
            "-u",
            "alice:secret",
            "ftp://127.0.0.1:0/");

        StringAssert.Contains(run.Written, "230 Logged in\r\n");
    }

    [TestMethod]
    public async Task RunAsync_Epsv_OpensItsPassiveListenerThroughTheOpenerCreatedWithTheProcesssTlsSettings()
    {
        var dataConnections = new InMemoryDataConnections().ScriptPassiveListener(new IPEndPoint(IPAddress.Loopback, 50001), null);

        var run = await ServeOneConnectionAsync(
            Encoding.ASCII.GetBytes("USER anonymous\r\nPASS ftp@example.com\r\nEPSV\r\nQUIT\r\n"),
            dataConnections,
            "-s",
            "--allow-anonymous",
            "--self-signed",
            "ftp://127.0.0.1:0/");

        StringAssert.Contains(run.Written, "229 Entering Extended Passive Mode (|||50001|)\r\n");
        Assert.HasCount(1, dataConnections.PassiveRequests);
        Assert.IsNotNull(run.TlsSettings);
        Assert.AreSame(run.TlsSettings, run.OpenerTlsSettings);
    }

    // Serves until the connection has ended, then stops.
    private async Task<Run> ServeOneConnectionAsync(byte[] request, IDataConnectionOpener? dataConnectionOpener, params string[] args)
    {
        var connection = new FakeConnection(request);
        var run = new Run(new FakeListenerFactory { Connection = connection }, dataConnectionOpener);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(
                run.Create,
                _ => true,
                _ => DataDirectoryLockOutcome.NoLock,
                TimeProvider.System,
                createDataConnectionOpener: dataConnectionOpener is null ? null : run.CreateDataConnectionOpener)
            .RunAsync(args, output, error, stop.Token);
        await run.Factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;
        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        run.Output = output.ToString();
        run.Written = Encoding.ASCII.GetString(connection.WrittenBytes);
        return run;
    }

    private sealed class Run(FakeListenerFactory factory, IDataConnectionOpener? dataConnectionOpener)
    {
        public FakeListenerFactory Factory { get; } = factory;

        public ServerTlsSettings? TlsSettings { get; private set; }

        public ServerTlsSettings? OpenerTlsSettings { get; private set; }

        public string Output { get; set; } = string.Empty;

        public string Written { get; set; } = string.Empty;

        public IListenerFactory Create(ServerTlsSettings? tlsSettings)
        {
            TlsSettings = tlsSettings;
            return Factory;
        }

        public IDataConnectionOpener CreateDataConnectionOpener(ServerTlsSettings? tlsSettings)
        {
            OpenerTlsSettings = tlsSettings;
            return dataConnectionOpener!;
        }
    }
}
