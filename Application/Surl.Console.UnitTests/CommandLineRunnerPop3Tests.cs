using System.Text;
using Surl.Cli;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> composes the POP3 server (BL-209): <c>pop3</c> and, through
/// <see cref="ImplicitTlsSchemeServer"/>, <c>pop3s</c> (ADR-0056 decision 8), <c>STLS</c> offered
/// once a certificate is configured, and the one mail store the SMTP server delivers into, so
/// mail sent over <c>smtp</c> is retrieved over <c>pop3</c> in the same run. No test here touches
/// the disk or the network.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerPop3Tests
{
    private static readonly string NewLine = Environment.NewLine;

    private static readonly byte[] Quit = Encoding.ASCII.GetBytes("QUIT\r\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_Pop3ListenUrl_StartsAListenerAnsweredByThePop3Server()
    {
        var run = await ServeOneConnectionAsync(Quit, "pop3://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("pop3", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.AreEqual($"Listening on pop3://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        Assert.AreEqual("+OK surl ready\r\n+OK surl signing off\r\n", run.Written);
        Assert.IsNull(run.TlsSettings);
    }

    [TestMethod]
    public async Task RunAsync_Pop3sListenUrlWithSelfSigned_StartsASecuredListenerAnsweredByThePop3Server()
    {
        var run = await ServeOneConnectionAsync(Quit, "-s", "--self-signed", "pop3s://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("pop3s", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.IsNotNull(run.TlsSettings);
        StringAssert.StartsWith(run.Written, "+OK surl ready\r\n");
    }

    [TestMethod]
    public async Task RunAsync_Pop3sWithoutCertOrSelfSigned_WritesNeedsACertificateAndReturnsCertificateProblem()
    {
        var factory = new FakeListenerFactory();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new CommandLineRunner(
                _ => factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System)
            .RunAsync(["pop3s://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        Assert.AreEqual(
            "surl: (58) pop3s://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            error.ToString());
        Assert.IsEmpty(factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_Pop3WithSelfSigned_MakesACertificateAndAdvertisesStls()
    {
        var run = await ServeOneConnectionAsync(Encoding.ASCII.GetBytes("CAPA\r\nQUIT\r\n"), "-s", "--self-signed", "pop3://127.0.0.1:0/");

        Assert.IsNotNull(run.TlsSettings);
        StringAssert.Contains(run.Written, "\r\nSTLS\r\n.\r\n");
    }

    [TestMethod]
    public async Task RunAsync_Pop3WithoutACertificate_MakesNoTlsSettingsAndAnswersStlsNotAvailable()
    {
        var run = await ServeOneConnectionAsync(Encoding.ASCII.GetBytes("CAPA\r\nSTLS\r\nQUIT\r\n"), "pop3://127.0.0.1:0/");

        Assert.IsNull(run.TlsSettings);
        Assert.DoesNotContain("STLS\r\n.", run.Written);
        StringAssert.Contains(run.Written, "-ERR STLS not available\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AccountsWithoutALogin_RefusesStatAuthenticationRequired()
    {
        var run = await ServeOneConnectionAsync(
            Encoding.ASCII.GetBytes("STAT\r\nQUIT\r\n"), "-s", "-u", "alice:secret", "pop3://127.0.0.1:0/");

        StringAssert.Contains(run.Written, "-ERR [AUTH] Authentication required\r\n");
    }

    [TestMethod]
    public async Task RunAsync_SmtpAndPop3ListenUrls_Pop3RetrievesTheMailSmtpDeliveredIntoTheOneMailStore()
    {
        var smtpConnection = new FakeConnection(Encoding.ASCII.GetBytes(
            "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\nSubject: shared\r\n\r\nretrieved over pop3\r\n.\r\nQUIT\r\n"));
        var pop3Connection = new FakeConnection(Encoding.ASCII.GetBytes("STAT\r\nRETR 1\r\nQUIT\r\n"));
        var factory = new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>>
            {
                ["smtp"] = Task.FromResult(smtpConnection),
                ["pop3"] = HandOutAfterAsync(smtpConnection, pop3Connection),
            },
        };
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(_ => factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System)
            .RunAsync(["-s", "--allow-anonymous", "smtp://127.0.0.1:0/", "pop3://127.0.0.1:0/"], output, error, stop.Token);
        await pop3Connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        StringAssert.Contains(Encoding.ASCII.GetString(smtpConnection.WrittenBytes), "250 2.0.0 Message accepted\r\n");
        var written = Encoding.ASCII.GetString(pop3Connection.WrittenBytes);
        StringAssert.Contains(written, "\r\n+OK 1 ");
        StringAssert.Contains(written, "retrieved over pop3\r\n.\r\n");
        StringAssert.EndsWith(written, "+OK surl signing off\r\n");
    }

    private static async Task<FakeConnection> HandOutAfterAsync(FakeConnection first, FakeConnection then)
    {
        await first.Disposed.Task;
        return then;
    }

    // Serves until the connection has ended, then stops.
    private async Task<Run> ServeOneConnectionAsync(byte[] request, params string[] args)
    {
        var connection = new FakeConnection(request);
        var run = new Run(new FakeListenerFactory { Connection = connection });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(run.Create, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System)
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

    private sealed class Run(FakeListenerFactory factory)
    {
        public FakeListenerFactory Factory { get; } = factory;

        public ServerTlsSettings? TlsSettings { get; private set; }

        public string Output { get; set; } = string.Empty;

        public string Written { get; set; } = string.Empty;

        public IListenerFactory Create(ServerTlsSettings? tlsSettings)
        {
            TlsSettings = tlsSettings;
            return Factory;
        }
    }
}
