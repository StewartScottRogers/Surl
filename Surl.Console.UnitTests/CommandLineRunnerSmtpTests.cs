using System.Text;
using Surl.Cli;
using Surl.MailStore;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> composes the SMTP server (BL-207): <c>smtp</c> and, through
/// <see cref="ImplicitTlsSchemeServer"/>, <c>smtps</c> (ADR-0053 decision 5), <c>STARTTLS</c>
/// offered once a certificate is configured, and the one mail store it delivers into - kept in
/// <c>&lt;data directory&gt;/.surl/mail</c> and loaded before any listener binds with
/// <c>--directory</c>, in memory only without it (ADR-0050, decision 7). No test here touches the
/// disk or the network.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerSmtpTests
{
    private static readonly string NewLine = Environment.NewLine;

    private static readonly string MailFolderPath = Path.Join(Path.GetFullPath("served"), ".surl", "mail");

    private static readonly string IndexPath = Path.Join(MailFolderPath, MailStoreFiles.IndexFileName);

    // An empty store: the header, next message file number 0, last UIDVALIDITY 0, no owner.
    private static readonly byte[] EmptyIndex = [.. "SURL-MAIL-INDEX-1\n"u8, .. new byte[16]];

    private static readonly byte[] OneMessage = Encoding.ASCII.GetBytes(
        "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\nSubject: hi\r\n\r\nhello\r\n.\r\nQUIT\r\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_SmtpListenUrl_StartsAListenerAnsweredByTheSmtpServer()
    {
        var run = await ServeOneConnectionAsync(Encoding.ASCII.GetBytes("QUIT\r\n"), null, "smtp://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("smtp", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.AreEqual($"Listening on smtp://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        StringAssert.StartsWith(run.Written, "220 surl ESMTP ready\r\n");
        Assert.IsNull(run.TlsSettings);
    }

    [TestMethod]
    public async Task RunAsync_SmtpsListenUrlWithSelfSigned_StartsASecuredListenerAnsweredByTheSmtpServer()
    {
        var run = await ServeOneConnectionAsync(Encoding.ASCII.GetBytes("QUIT\r\n"), null, "-s", "--self-signed", "smtps://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("smtps", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.IsNotNull(run.TlsSettings);
        StringAssert.StartsWith(run.Written, "220 surl ESMTP ready\r\n");
    }

    [TestMethod]
    public async Task RunAsync_SmtpsWithoutCertOrSelfSigned_WritesNeedsACertificateAndReturnsCertificateProblem()
    {
        var run = await RunRefusedAsync(null, "smtps://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CertificateProblem, run.ExitCode);
        Assert.AreEqual(
            "surl: (58) smtps://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            run.Error);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_SmtpWithSelfSigned_MakesACertificateAndAdvertisesStartTls()
    {
        var run = await ServeOneConnectionAsync(Encoding.ASCII.GetBytes("EHLO c\r\nQUIT\r\n"), null, "-s", "--self-signed", "smtp://127.0.0.1:0/");

        Assert.IsNotNull(run.TlsSettings);
        StringAssert.Contains(run.Written, "250-STARTTLS\r\n");
    }

    [TestMethod]
    public async Task RunAsync_SmtpWithoutACertificate_MakesNoTlsSettingsAndAnswersStartTls454()
    {
        var run = await ServeOneConnectionAsync(Encoding.ASCII.GetBytes("EHLO c\r\nSTARTTLS\r\nQUIT\r\n"), null, "smtp://127.0.0.1:0/");

        Assert.IsNull(run.TlsSettings);
        Assert.DoesNotContain("STARTTLS\r\n", run.Written);
        StringAssert.Contains(run.Written, "454 4.7.0 TLS not available\r\n");
    }

    [TestMethod]
    public async Task RunAsync_AllowAnonymousWithoutDirectory_DeliversIntoAnInMemoryMailStoreTouchingNoFile()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();

        var run = await ServeOneConnectionAsync(OneMessage, fileSystem, "-s", "--allow-anonymous", "smtp://127.0.0.1:0/");

        StringAssert.Contains(run.Written, "250 2.0.0 Message accepted\r\n");
        Assert.IsEmpty(fileSystem.AccessedPaths);
    }

    [TestMethod]
    public async Task RunAsync_AccountsWithoutALogin_RefusesMailWith530()
    {
        var run = await ServeOneConnectionAsync(OneMessage, null, "-s", "-u", "alice:secret", "smtp://127.0.0.1:0/");

        StringAssert.Contains(run.Written, "\r\n530 ");
        Assert.DoesNotContain("Message accepted", run.Written);
    }

    [TestMethod]
    public void ComposeMailStoreFiles_DataDirectory_KeepsThemInTheDotSurlMailFolderOfTheFullPath()
    {
        var files = CommandLineRunner.ComposeMailStoreFiles(
            ParseServing("--directory", "served", "smtp://127.0.0.1:0/"), new UnitTestReadOnlyContentFileSystem());

        Assert.IsNotNull(files);
        Assert.AreEqual(MailFolderPath, files.StateFolderPath);
        Assert.AreEqual(IndexPath, files.IndexPath);
    }

    [TestMethod]
    public void ComposeMailStoreFiles_NoDirectory_ComposesNone()
    {
        Assert.IsNull(CommandLineRunner.ComposeMailStoreFiles(ParseServing("smtp://127.0.0.1:0/"), new UnitTestReadOnlyContentFileSystem()));
    }

    [TestMethod]
    public async Task LoadMailStoreAsync_NoFiles_GivesAnInMemoryStoreBoundedByMaxFilesize()
    {
        var (mailStore, failureMessage) = await CommandLineRunner.LoadMailStoreAsync(
            null, ParseServing("--max-filesize", "1000", "smtp://127.0.0.1:0/"), ["alice"], TimeProvider.System, TestContext.CancellationToken);

        Assert.IsNotNull(mailStore);
        Assert.IsNull(failureMessage);
        Assert.AreEqual(1000, mailStore.MaxMessageBytes);
        Assert.AreEqual(MailboxStore.DefaultMaxMessages, mailStore.MaxMessages);
        Assert.AreEqual(MailRecipientLookup.Deliverable, mailStore.LookUpRecipient("<alice@example.com>", out _));
    }

    [TestMethod]
    public async Task LoadMailStoreAsync_Index_ReadsItAndGivesAStoreWithoutFailure()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[IndexPath] = EmptyIndex;
        var commandLine = ParseServing("--directory", "served", "--allow-anonymous", "smtp://127.0.0.1:0/");

        var (mailStore, failureMessage) = await CommandLineRunner.LoadMailStoreAsync(
            CommandLineRunner.ComposeMailStoreFiles(commandLine, fileSystem), commandLine, [], TimeProvider.System, TestContext.CancellationToken);

        Assert.IsNotNull(mailStore);
        Assert.IsNull(failureMessage);
        CollectionAssert.Contains(fileSystem.AccessedPaths, IndexPath);
    }

    [TestMethod]
    public async Task LoadMailStoreAsync_MalformedIndex_GivesCouldNotReadNotAMailStoreIndex()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[IndexPath] = "not the header"u8.ToArray();
        var commandLine = ParseServing("--directory", "served", "smtp://127.0.0.1:0/");

        var (mailStore, failureMessage) = await CommandLineRunner.LoadMailStoreAsync(
            CommandLineRunner.ComposeMailStoreFiles(commandLine, fileSystem), commandLine, [], TimeProvider.System, TestContext.CancellationToken);

        Assert.IsNull(mailStore);
        Assert.AreEqual($"(37) Could not read {IndexPath}: {MailStoreFiles.MalformedIndexReason}", failureMessage);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectoryWithAMalformedMailStore_WritesCouldNotReadAndReturnsCouldNotReadFileWithoutStartingAListener()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[IndexPath] = "not the header"u8.ToArray();
        var factory = new FakeListenerFactory();
        var lockHolder = new FakeLockHolder();
        var events = new List<string>();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new CommandLineRunner(
                _ => factory,
                _ => true,
                _ =>
                {
                    events.Add($"lock taken with {fileSystem.AccessedPaths.Count} paths read");
                    return DataDirectoryLockOutcome.Taken(lockHolder);
                },
                TimeProvider.System,
                fileSystem)
            .RunAsync(["--directory", "served", "smtp://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual($"surl: (37) Could not read {IndexPath}: {MailStoreFiles.MalformedIndexReason}" + NewLine, error.ToString());
        CollectionAssert.AreEqual(new[] { "lock taken with 0 paths read" }, events);
        Assert.IsEmpty(factory.StartedListenUrls);
        Assert.IsTrue(lockHolder.Disposed);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectory_LoadsTheMailStoreFromTheDotSurlMailFolderBeforeAnyListenerStarts()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[IndexPath] = EmptyIndex;
        var factory = new FakeListenerFactory();
        var accessedBeforeTheListenerFactory = new List<string>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(
                _ =>
                {
                    accessedBeforeTheListenerFactory.AddRange(fileSystem.AccessedPaths);
                    return factory;
                },
                _ => true,
                _ => DataDirectoryLockOutcome.Taken(new FakeLockHolder()),
                TimeProvider.System,
                fileSystem)
            .RunAsync(["--directory", "served", "smtp://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(string.Empty, error.ToString());
        CollectionAssert.Contains(accessedBeforeTheListenerFactory, IndexPath);
        CollectionAssert.AreEqual(fileSystem.AccessedPaths, accessedBeforeTheListenerFactory);
    }

    private static SurlCommandLine ParseServing(params string[] args) =>
        CommandLineParser.Parse(args).CommandLine ?? throw new AssertFailedException("The command line was refused.");

    private async Task<Run> RunRefusedAsync(UnitTestReadOnlyContentFileSystem? fileSystem, params string[] args)
    {
        var run = new Run(new FakeListenerFactory());
        using var output = new StringWriter();
        using var error = new StringWriter();

        run.ExitCode = await CreateRunner(run, fileSystem).RunAsync(args, output, error, TestContext.CancellationToken);
        run.Error = error.ToString();
        return run;
    }

    // Serves until the connection has ended, then stops.
    private async Task<Run> ServeOneConnectionAsync(byte[] request, UnitTestReadOnlyContentFileSystem? fileSystem, params string[] args)
    {
        var connection = new FakeConnection(request);
        var run = new Run(new FakeListenerFactory { Connection = connection });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(run, fileSystem).RunAsync(args, output, error, stop.Token);
        await run.Factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        run.ExitCode = await running;
        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode, error.ToString());
        run.Output = output.ToString();
        run.Error = error.ToString();
        run.Written = Encoding.ASCII.GetString(connection.WrittenBytes);
        return run;
    }

    private static CommandLineRunner CreateRunner(Run run, UnitTestReadOnlyContentFileSystem? fileSystem) =>
        new(run.Create, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System, fileSystem);

    private sealed class Run(FakeListenerFactory factory)
    {
        public FakeListenerFactory Factory { get; } = factory;

        public ServerTlsSettings? TlsSettings { get; private set; }

        public SurlExitCode ExitCode { get; set; }

        public string Output { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;

        public string Written { get; set; } = string.Empty;

        public IListenerFactory Create(ServerTlsSettings? tlsSettings)
        {
            TlsSettings = tlsSettings;
            return Factory;
        }
    }
}
