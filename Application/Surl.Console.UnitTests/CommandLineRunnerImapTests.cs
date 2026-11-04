using System.Text;
using Surl.Cli;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> composes the IMAP server (BL-208): <c>imap</c> and, through
/// <see cref="ImplicitTlsSchemeServer"/>, <c>imaps</c> (ADR-0055 decision 11), <c>STARTTLS</c>
/// offered once a certificate is configured, and the one mail store the SMTP server delivers
/// into, so mail sent over <c>smtp</c> is read over <c>imap</c> in the same run. No test here
/// touches the disk or the network.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerImapTests
{
    private static readonly string NewLine = Environment.NewLine;

    private static readonly byte[] Logout = Encoding.ASCII.GetBytes("a1 LOGOUT\r\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_ImapListenUrl_StartsAListenerAnsweredByTheImapServer()
    {
        var run = await ServeOneConnectionAsync(Logout, "imap://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("imap", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.AreEqual($"Listening on imap://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        StringAssert.StartsWith(run.Written, "* OK [CAPABILITY IMAP4rev1 ");
        StringAssert.Contains(run.Written, "a1 OK LOGOUT completed\r\n");
        Assert.IsNull(run.TlsSettings);
    }

    [TestMethod]
    public async Task RunAsync_ImapsListenUrlWithSelfSigned_StartsASecuredListenerAnsweredByTheImapServer()
    {
        var run = await ServeOneConnectionAsync(Logout, "-s", "--self-signed", "imaps://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("imaps", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.IsNotNull(run.TlsSettings);
        StringAssert.StartsWith(run.Written, "* OK [CAPABILITY IMAP4rev1 ");
    }

    [TestMethod]
    public async Task RunAsync_ImapsWithoutCertOrSelfSigned_WritesNeedsACertificateAndReturnsCertificateProblem()
    {
        var factory = new FakeListenerFactory();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new CommandLineRunner(
                _ => factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System)
            .RunAsync(["imaps://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        Assert.AreEqual(
            "surl: (58) imaps://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            error.ToString());
        Assert.IsEmpty(factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_ImapWithSelfSigned_MakesACertificateAndAdvertisesStartTls()
    {
        var run = await ServeOneConnectionAsync(Logout, "-s", "--self-signed", "imap://127.0.0.1:0/");

        Assert.IsNotNull(run.TlsSettings);
        StringAssert.Contains(run.Written.Split("\r\n")[0], " STARTTLS ");
    }

    [TestMethod]
    public async Task RunAsync_ImapWithoutACertificate_MakesNoTlsSettingsAndAnswersStartTlsNotAvailable()
    {
        var run = await ServeOneConnectionAsync(Encoding.ASCII.GetBytes("a1 STARTTLS\r\na2 LOGOUT\r\n"), "imap://127.0.0.1:0/");

        Assert.IsNull(run.TlsSettings);
        Assert.DoesNotContain("STARTTLS ", run.Written.Split("\r\n")[0]);
        StringAssert.Contains(run.Written, "a1 BAD STARTTLS not available\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AccountsWithoutALogin_RefusesSelectAuthenticationFailed()
    {
        var run = await ServeOneConnectionAsync(
            Encoding.ASCII.GetBytes("a1 SELECT INBOX\r\na2 LOGOUT\r\n"), "-s", "-u", "alice:secret", "imap://127.0.0.1:0/");

        StringAssert.Contains(run.Written, "a1 NO [AUTHENTICATIONFAILED] ");
    }

    [TestMethod]
    public async Task RunAsync_SmtpAndImapListenUrls_ImapReadsTheMailSmtpDeliveredIntoTheOneMailStore()
    {
        var smtpConnection = new FakeConnection(Encoding.ASCII.GetBytes(
            "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\nSubject: shared\r\n\r\nread over imap\r\n.\r\nQUIT\r\n"));
        var imapConnection = new FakeConnection(Encoding.ASCII.GetBytes("a1 SELECT INBOX\r\na2 FETCH 1 BODY[TEXT]\r\na3 LOGOUT\r\n"));
        var factory = new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>>
            {
                ["smtp"] = Task.FromResult(smtpConnection),
                ["imap"] = HandOutAfterAsync(smtpConnection, imapConnection),
            },
        };
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(_ => factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System)
            .RunAsync(["-s", "--allow-anonymous", "smtp://127.0.0.1:0/", "imap://127.0.0.1:0/"], output, error, stop.Token);
        await imapConnection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        StringAssert.Contains(Encoding.ASCII.GetString(smtpConnection.WrittenBytes), "250 2.0.0 Message accepted\r\n");
        var written = Encoding.ASCII.GetString(imapConnection.WrittenBytes);
        StringAssert.Contains(written, "* 1 EXISTS\r\n");
        StringAssert.Contains(written, "read over imap\r\n");
        StringAssert.Contains(written, "a2 OK FETCH completed\r\n");
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
