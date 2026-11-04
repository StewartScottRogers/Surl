using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// The log levels, the trace dump, <c>--trace-time</c> and <c>--log-file</c> as the runner
/// composes them (ADR-0033, sections 1, 4, 5 and 6), over one fake HTTP connection.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerLogTests
{
    private const string Listen = "http://127.0.0.1:0/";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string ListeningLine = $"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine;

    private static readonly string OpenedLine = "#1 * Exchange 1 opened: http from 127.0.0.1:50000." + NewLine;

    private static readonly byte[] Request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: a\r\n\r\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_Silent_WritesNothingToOutputOrErrorForAConnection()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, "-s", Listen);

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_SilentWithAListenUrlNeedingACertificate_WritesNothingAndReturnsCertificateProblem()
    {
        var run = await RunRefusedAsync("-s", "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CertificateProblem, run.ExitCode);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_SilentWithACommandLineRefusal_StillWritesTheRefusal()
    {
        var run = await RunRefusedAsync("-s", "--bogus", Listen);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual(
            "surl: option --bogus: is unknown" + NewLine + "surl: try 'surl --help' or 'surl --manual' for more information" + NewLine,
            run.Error);
    }

    [TestMethod]
    public async Task RunAsync_SilentShowError_WritesNothingForAConnection()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, "-s", "-S", Listen);

        Assert.AreEqual(string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_SilentShowErrorWithAListenUrlNeedingACertificate_WritesOnlyTheFailureLine()
    {
        var run = await RunRefusedAsync("-s", "-S", "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CertificateProblem, run.ExitCode);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.AreEqual("surl: (58) https://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_DefaultLevel_WritesTheListeningLineAndOneInfoLineForTheConnection()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, Listen);

        Assert.AreEqual(ListeningLine, run.Output);
        Assert.AreEqual(OpenedLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_Verbose_WritesTheVerboseLinesFromOpenToClose()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, "-v", Listen);

        Assert.AreEqual(ListeningLine, run.Output);
        StringAssert.StartsWith(run.Error, OpenedLine + @"#1 < GET / HTTP/1.1\r\n" + NewLine);
        StringAssert.Contains(run.Error, @"#1 > HTTP/1.1 404 Not Found\r\n" + NewLine);
        StringAssert.EndsWith(run.Error, "#1 * Exchange 1 ended; closing the connection." + NewLine);
    }

    [TestMethod]
    public async Task RunAsync_TraceFile_DumpsToTheWriterOpenedForItAndNothingToError()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, "--trace", "dump.txt", Listen);

        var file = run.OpenedFiles.Single();
        Assert.AreEqual(("dump.txt", FileMode.Create), (file.Path, file.Mode));
        StringAssert.StartsWith(
            file.Text,
            OpenedLine + "#1 <= Recv data, 27 bytes (0x1b)" + NewLine
            + "0000: 47 45 54 20 2f 20 48 54 54 50 2f 31 2e 31 0d 0a GET / HTTP/1.1.." + NewLine);
        Assert.IsTrue(file.Disposed);
        Assert.AreEqual(ListeningLine, run.Output);
        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_TraceAsciiToStdout_DumpsToOutputAfterTheListeningLine()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, "--trace-ascii", "-", Listen);

        StringAssert.StartsWith(
            run.Output,
            ListeningLine + OpenedLine + "#1 <= Recv data, 27 bytes (0x1b)" + NewLine + "0000: GET / HTTP/1.1" + NewLine);
        Assert.IsEmpty(run.OpenedFiles);
        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_LogLevelTraceWithoutATraceFile_DumpsToTheLogStream()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, "--log-level", "trace", Listen);

        StringAssert.StartsWith(run.Error, OpenedLine + "#1 <= Recv data, 27 bytes (0x1b)" + NewLine);
        Assert.AreEqual(ListeningLine, run.Output);
    }

    [TestMethod]
    public async Task RunAsync_LogFile_AppendsTheLogToTheWriterOpenedForItAndNothingToError()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, "--log-file", "surl.log", Listen);

        var file = run.OpenedFiles.Single();
        Assert.AreEqual(("surl.log", FileMode.Append, OpenedLine), (file.Path, file.Mode, file.Text));
        Assert.IsTrue(file.Disposed);
        Assert.AreEqual(ListeningLine, run.Output);
        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_LogFileToStdout_WritesTheLogToOutput()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, "--log-file", "-", Listen);

        Assert.AreEqual(ListeningLine + OpenedLine, run.Output);
        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_VerboseSelfSignedHttpsWithLogFile_WritesTheWarningAndTheThrowawayCertificateNoteToTheLogFile()
    {
        var run = await ServeAsync(
            TimeProvider.System, connection: null, "-v", "--self-signed", "--log-file", "surl.log", "https://127.0.0.1:0/");

        Assert.MatchesRegex(
            "^surl: warning: --self-signed: serving a throwaway certificate; clients must skip verification \\(curl -k\\)" + NewLine
            + "\\* Serving a throwaway certificate, SHA-256 [0-9A-F]{64}" + NewLine + "$", run.OpenedFiles.Single().Text);
        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_TraceTime_StampsTheLinesWithTheInjectedClocksLocalTime()
    {
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 9, 29, 8, 7, 20, TimeSpan.Zero).AddTicks(8_951_230), TimeSpan.FromHours(2));

        var run = await ServeOneConnectionAsync(clock, "--trace-time", Listen);

        Assert.AreEqual("10:07:20.895123 " + OpenedLine, run.Error);
        Assert.AreEqual(ListeningLine, run.Output);
    }

    [TestMethod]
    public async Task RunAsync_LogFileCannotBeOpened_WritesCouldNotOpenAndReturnsCouldNotWriteFileBeforeAnyListenerStarts()
    {
        var run = await RunRefusedAsync(
            "no/such/surl.log", new DirectoryNotFoundException("Could not find a part of the path."), "--log-file", "no/such/surl.log", Listen);

        Assert.AreEqual(SurlExitCode.CouldNotWriteFile, run.ExitCode);
        Assert.AreEqual(
            "surl: (23) Could not open no/such/surl.log for --log-file: Could not find a part of the path." + NewLine,
            run.Error);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    [DataRow("--trace")]
    [DataRow("--trace-ascii")]
    public async Task RunAsync_TraceFileCannotBeOpened_ClosesTheLogFileAndNamesTheTraceOption(string option)
    {
        var run = await RunRefusedAsync(
            "dump.txt", new UnauthorizedAccessException("Access denied."), "--log-file", "surl.log", option, "dump.txt", Listen);

        Assert.AreEqual(SurlExitCode.CouldNotWriteFile, run.ExitCode);
        Assert.AreEqual($"surl: (23) Could not open dump.txt for {option}: Access denied." + NewLine, run.Error);
        Assert.IsTrue(run.OpenedFiles.Single().Disposed);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    [DataRow("--trace", "surl.log")]
    [DataRow("--trace-ascii", "surl.log")]
    [DataRow("--trace", "./surl.log")]
    [DataRow("--trace", "logs/../surl.log")]
    [DataRow("--trace-ascii", "SURL.LOG")]
    public async Task RunAsync_TraceFileIsTheLogFile_RefusesWithCouldNotWriteFileAndClosesTheLogFileBeforeAnyListenerStarts(
        string option, string traceFile)
    {
        var run = await RunRefusedAsync("--log-file", "surl.log", option, traceFile, Listen);

        Assert.AreEqual(SurlExitCode.CouldNotWriteFile, run.ExitCode);
        Assert.AreEqual(
            $"surl: (23) Could not open {traceFile} for {option}: --log-file names the same file" + NewLine, run.Error);
        var logFile = run.OpenedFiles.Single();
        Assert.AreEqual(("surl.log", FileMode.Append, true), (logFile.Path, logFile.Mode, logFile.Disposed));
        Assert.AreEqual(string.Empty, run.Output);
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_LogFileAndADifferentTraceFile_OpensEachSeparately()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, "--log-file", "surl.log", "--trace", "dump.txt", Listen);

        var opened = run.OpenedFiles.Select(file => (file.Path, file.Mode, file.Disposed)).ToArray();
        CollectionAssert.AreEqual(
            new[] { ("surl.log", FileMode.Append, true), ("dump.txt", FileMode.Create, true) }, opened);
        StringAssert.StartsWith(run.OpenedFiles[1].Text, OpenedLine + "#1 <= Recv data, 27 bytes (0x1b)" + NewLine);
        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_LogFileToStdoutAndATraceFile_OpensOnlyTheTraceFile()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, "--log-file", "-", "--trace", "dump.txt", Listen);

        var file = run.OpenedFiles.Single();
        Assert.AreEqual(("dump.txt", FileMode.Create), (file.Path, file.Mode));
        Assert.AreEqual(ListeningLine, run.Output);
    }

    [TestMethod]
    [DataRow(typeof(ArgumentException))]
    [DataRow(typeof(NotSupportedException))]
    public async Task RunAsync_LogFilePathTheFileSystemRejects_WritesCouldNotOpenAndReturnsCouldNotWriteFile(Type failureType)
    {
        var failure = (Exception)Activator.CreateInstance(failureType, "Bad path.")!;

        var run = await RunRefusedAsync("surl.log", failure, "--log-file", "surl.log", Listen);

        Assert.AreEqual(SurlExitCode.CouldNotWriteFile, run.ExitCode);
        Assert.AreEqual("surl: (23) Could not open surl.log for --log-file: Bad path." + NewLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_LogFileThatThrowsSomethingElse_LetsTheFailureThrough()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => RunRefusedAsync("surl.log", new InvalidOperationException("Not a file failure."), "--log-file", "surl.log", Listen));
    }

    [TestMethod]
    public async Task RunAsync_TraceThenVerbose_NeverOpensTheTraceFile()
    {
        var run = await ServeOneConnectionAsync(TimeProvider.System, "--trace", "dump.txt", "-v", Listen);

        Assert.IsEmpty(run.OpenedFiles);
        StringAssert.StartsWith(run.Error, OpenedLine + @"#1 < GET / HTTP/1.1\r\n" + NewLine);
    }

    [TestMethod]
    public async Task RunAsync_SilentShowErrorWithAFileThatCannotBeOpened_WritesOnlyTheCouldNotOpenLine()
    {
        var run = await RunRefusedAsync("surl.log", new IOException("Disk full."), "-s", "-S", "--log-file", "surl.log", Listen);

        Assert.AreEqual(SurlExitCode.CouldNotWriteFile, run.ExitCode);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.AreEqual("surl: (23) Could not open surl.log for --log-file: Disk full." + NewLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_SilentWithAFileThatCannotBeOpened_WritesNothingAndReturnsCouldNotWriteFile()
    {
        var run = await RunRefusedAsync("surl.log", new IOException("Disk full."), "-s", "--log-file", "surl.log", Listen);

        Assert.AreEqual(SurlExitCode.CouldNotWriteFile, run.ExitCode);
        Assert.AreEqual(string.Empty, run.Error);
    }

    private Task<Run> ServeOneConnectionAsync(TimeProvider clock, params string[] args) =>
        ServeAsync(clock, new FakeConnection(Request), args);

    // Serves until the connection, when there is one, has ended and been logged, then stops.
    private async Task<Run> ServeAsync(TimeProvider clock, FakeConnection? connection, params string[] args)
    {
        var run = new Run(new FakeListenerFactory { Connection = connection }, failingPath: null, failure: null);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(run, clock).RunAsync(args, output, error, stop.Token);
        await run.Factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        if (connection is not null)
        {
            await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        }

        await stop.CancelAsync();
        run.ExitCode = await running;
        run.Output = output.ToString();
        run.Error = error.ToString();
        return run;
    }

    private Task<Run> RunRefusedAsync(params string[] args) => RunRefusedAsync(failingPath: null, failure: null, args);

    private async Task<Run> RunRefusedAsync(string? failingPath, Exception? failure, params string[] args)
    {
        var run = new Run(new FakeListenerFactory(), failingPath, failure);
        using var output = new StringWriter();
        using var error = new StringWriter();

        run.ExitCode = await CreateRunner(run, TimeProvider.System).RunAsync(args, output, error, TestContext.CancellationToken);
        run.Output = output.ToString();
        run.Error = error.ToString();
        return run;
    }

    private static CommandLineRunner CreateRunner(Run run, TimeProvider clock) =>
        new(_ => run.Factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, clock, openLogFile: run.OpenLogFile);

    /// <summary>
    /// One run's outcome, and the log file seam it ran with: every path opened gets an
    /// in-memory file, except <c>failingPath</c>, which throws <c>failure</c>.
    /// </summary>
    private sealed class Run(FakeListenerFactory factory, string? failingPath, Exception? failure)
    {
        public FakeListenerFactory Factory { get; } = factory;

        public List<OpenedFile> OpenedFiles { get; } = [];

        public SurlExitCode ExitCode { get; set; }

        public string Output { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;

        public TextWriter OpenLogFile(string path, FileMode mode)
        {
            if (path == failingPath)
            {
                throw failure!;
            }

            var file = new OpenedFile(path, mode);
            OpenedFiles.Add(file);
            return file;
        }
    }

    private sealed class OpenedFile(string path, FileMode mode) : StringWriter
    {
        public string Path { get; } = path;

        public FileMode Mode { get; } = mode;

        public bool Disposed { get; private set; }

        public string Text => ToString();

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
