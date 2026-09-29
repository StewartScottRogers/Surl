using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

[TestClass]
public sealed class CommandLineRunnerTests
{
    private static readonly string NewLine = Environment.NewLine;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("--help")]
    [DataRow("-h")]
    public async Task RunAsync_Help_WritesHelpTextAndReturnsOk(string option)
    {
        var (exitCode, output, error) = await RunAsync(new FakeListenerFactory(), option);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(HelpText.Text, output);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public async Task RunAsync_Version_WritesVersionWithTheDictGopherGophersHttpHttpsMqttMqttsTelnetAndTftpSchemesAndReturnsOk()
    {
        var informationalVersion = typeof(CommandLineRunner).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        var (exitCode, output, error) = await RunAsync(new FakeListenerFactory(), "--version");

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(VersionText.Compose(informationalVersion, RuntimeInformation.RuntimeIdentifier, ["dict", "gopher", "gophers", "http", "https", "mqtt", "mqtts", "telnet", "tftp"]), output);
        StringAssert.EndsWith(output, NewLine + "Protocols: dict gopher gophers http https mqtt mqtts telnet tftp" + NewLine);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public async Task RunAsync_UnknownOption_WritesTheRefusalAndTheTryLineAndReturnsFailedInit()
    {
        var (exitCode, output, error) = await RunAsync(new FakeListenerFactory(), "--bogus", "http://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(
            "surl: option --bogus: is unknown" + NewLine + "surl: try 'surl --help' for more information" + NewLine,
            error);
    }

    [TestMethod]
    public async Task RunAsync_MalformedListenUrl_WritesTheRefusalWithoutTheTryLineAndReturnsMalformedUrl()
    {
        var (exitCode, _, error) = await RunAsync(new FakeListenerFactory(), "http://127.0.0.1:0/path");

        Assert.AreEqual(SurlExitCode.MalformedUrl, exitCode);
        Assert.AreEqual("surl: (3) URL rejected: A listen URL cannot have a path, query or fragment" + NewLine, error);
    }

    [TestMethod]
    public async Task RunAsync_SchemeWithNoRegisteredServer_WritesProtocolNotSupportedAndReturnsUnsupportedProtocol()
    {
        var factory = new FakeListenerFactory();

        var (exitCode, output, error) = await RunAsync(factory, "http://127.0.0.1:0/", "rtsp://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual("surl: (1) Protocol \"rtsp\" not supported" + NewLine, error);
        Assert.IsEmpty(factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectoryCannotBeOpened_WritesCouldNotOpenDirectoryAndReturnsCouldNotReadFile()
    {
        var factory = new FakeListenerFactory();
        var probedPaths = new List<string>();

        var (exitCode, _, error) = await RunAsync(
            factory,
            path =>
            {
                probedPaths.Add(path);
                return false;
            },
            "--directory",
            "no-such-dir",
            "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual("surl: (37) Could not open directory no-such-dir" + NewLine, error);
        CollectionAssert.AreEqual(new[] { "no-such-dir" }, probedPaths);
        Assert.IsEmpty(factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_NoDirectory_NeverProbesADataDirectoryAndServesInMemory()
    {
        var factory = new FakeListenerFactory();
        var probedPaths = new List<string>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(
                _ => factory,
                path =>
                {
                    probedPaths.Add(path);
                    return false;
                },
                TimeProvider.System)
            .RunAsync(["http://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsEmpty(probedPaths);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_ListenUrls_StartsOneListenerEachWritesTheStatusLinesAndReturnsOkWhenCancelled()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, TimeProvider.System)
            .RunAsync(["http://127.0.0.1:0/", "http://[::1]:8080/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        var statusLines = output.ToString();
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        CollectionAssert.AreEqual(
            new[] { new ListenUrl("http", "127.0.0.1", 0), new ListenUrl("http", "::1", 8080) },
            factory.StartedListenUrls);
        Assert.AreEqual(
            $"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine
            + $"Listening on http://[::1]:{FakeListenerFactory.BoundPort}/" + NewLine,
            statusLines);
        Assert.AreEqual(statusLines, output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_DictListenUrl_StartsADictListenerAndReturnsOkWhenCancelled()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, TimeProvider.System)
            .RunAsync(["dict://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        CollectionAssert.AreEqual(new[] { new ListenUrl("dict", "127.0.0.1", 0) }, factory.StartedListenUrls);
        Assert.AreEqual($"Listening on dict://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_GopherListenUrl_StartsAGopherListenerAndReturnsOkWhenCancelled()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, TimeProvider.System)
            .RunAsync(["gopher://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        CollectionAssert.AreEqual(new[] { new ListenUrl("gopher", "127.0.0.1", 0) }, factory.StartedListenUrls);
        Assert.AreEqual($"Listening on gopher://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_MqttListenUrl_StartsAnMqttListenerAndReturnsOkWhenCancelled()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, TimeProvider.System)
            .RunAsync(["mqtt://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        CollectionAssert.AreEqual(new[] { new ListenUrl("mqtt", "127.0.0.1", 0) }, factory.StartedListenUrls);
        Assert.AreEqual($"Listening on mqtt://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_TelnetListenUrl_StartsATelnetListenerAndReturnsOkWhenCancelled()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, TimeProvider.System)
            .RunAsync(["telnet://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        CollectionAssert.AreEqual(new[] { new ListenUrl("telnet", "127.0.0.1", 0) }, factory.StartedListenUrls);
        Assert.AreEqual($"Listening on telnet://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_TftpListenUrl_StartsADatagramListenerAndReturnsOkWhenCancelled()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, TimeProvider.System)
            .RunAsync(["tftp://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        CollectionAssert.AreEqual(new[] { new ListenUrl("tftp", "127.0.0.1", 0) }, factory.StartedDatagramListenUrls);
        Assert.AreEqual($"Listening on tftp://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_ListenerCannotBind_WritesCouldNotBindAndReturnsBindFailed()
    {
        var listenUrl = new ListenUrl("http", "127.0.0.1", 8080);
        var factory = new FakeListenerFactory
        {
            BindFailure = new ListenerBindException(
                listenUrl, new IPEndPoint(IPAddress.Loopback, 8080), ListenerBindFailure.AddressInUse, null),
        };

        var (exitCode, output, error) = await RunAsync(factory, "http://127.0.0.1:8080/");

        Assert.AreEqual(SurlExitCode.BindFailed, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual("surl: (45) Could not bind http://127.0.0.1:8080/: Address already in use" + NewLine, error);
    }

    [TestMethod]
    public async Task RunAsync_HostDoesNotResolve_WritesCouldNotResolveHostAndReturnsCouldNotResolveHost()
    {
        var listenUrl = new ListenUrl("http", "nowhere.invalid", 80);
        var factory = new FakeListenerFactory
        {
            BindFailure = new ListenerBindException(listenUrl, null, ListenerBindFailure.HostNotFound, null),
        };

        var (exitCode, _, error) = await RunAsync(factory, "http://nowhere.invalid/");

        Assert.AreEqual(SurlExitCode.CouldNotResolveHost, exitCode);
        Assert.AreEqual("surl: (6) Could not resolve host: nowhere.invalid" + NewLine, error);
    }

    [TestMethod]
    public async Task RunAsync_EngineThrows_WritesInternalErrorAndReturnsInternalError()
    {
        var factory = new FakeListenerFactory { AcceptFailure = new InvalidOperationException("accept broke") };

        var (exitCode, _, error) = await RunAsync(factory, "http://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.InternalError, exitCode);
        Assert.AreEqual("surl: (125) Internal error: accept broke" + NewLine, error);
    }

    [TestMethod]
    [DataRow(ListenerBindFailure.AddressInUse, "Address already in use")]
    [DataRow(ListenerBindFailure.AddressNotAvailable, "Address not available")]
    [DataRow(ListenerBindFailure.PermissionDenied, "Permission denied")]
    [DataRow(ListenerBindFailure.Other, "Bind failed")]
    public void FormatBindFailure_EachFailure_NamesItsReason(ListenerBindFailure failure, string reason)
    {
        var listenUrl = new ListenUrl("http", "0.0.0.0", 80);
        var bindFailure = new ListenerBindException(listenUrl, new IPEndPoint(IPAddress.Any, 80), failure, null);

        Assert.AreEqual($"(45) Could not bind http://0.0.0.0:80/: {reason}", CommandLineRunner.FormatBindFailure(bindFailure));
    }

    [TestMethod]
    public void FormatBindFailure_Ipv6EndPoint_BracketsTheAddressAndKeepsThePortAsAsked()
    {
        var listenUrl = new ListenUrl("http", "localhost", 0);
        var bindFailure = new ListenerBindException(
            listenUrl, new IPEndPoint(IPAddress.IPv6Loopback, 50000), ListenerBindFailure.AddressNotAvailable, null);

        Assert.AreEqual(
            "(45) Could not bind http://[::1]:0/: Address not available", CommandLineRunner.FormatBindFailure(bindFailure));
    }

    [TestMethod]
    public void FormatBindFailure_NoEndPoint_WritesTheHostAsWritten()
    {
        var listenUrl = new ListenUrl("http", "::1", 8080);
        var bindFailure = new ListenerBindException(listenUrl, null, ListenerBindFailure.Other, null);

        Assert.AreEqual("(45) Could not bind http://[::1]:8080/: Bind failed", CommandLineRunner.FormatBindFailure(bindFailure));
    }

    [TestMethod]
    public void ComposeContentStore_NoExposureOption_ServesWithAdr0006Defaults()
    {
        var store = CommandLineRunner.ComposeContentStore(ParseServing("http://127.0.0.1:0/"), TimeProvider.System);

        Assert.AreEqual(new ContentExposureOptions(), store.ExposureOptions);
    }

    [TestMethod]
    public void ComposeContentStore_AllowUploads_ReachesTheStore()
    {
        var store = CommandLineRunner.ComposeContentStore(ParseServing("--allow-uploads", "http://127.0.0.1:0/"), TimeProvider.System);

        Assert.AreEqual(new ContentExposureOptions { AllowUploads = true }, store.ExposureOptions);
    }

    [TestMethod]
    public void ComposeContentStore_ListDirectories_ReachesTheStore()
    {
        var store = CommandLineRunner.ComposeContentStore(ParseServing("--list-directories", "http://127.0.0.1:0/"), TimeProvider.System);

        Assert.AreEqual(new ContentExposureOptions { ListDirectories = true }, store.ExposureOptions);
    }

    [TestMethod]
    public void ComposeContentStore_FollowSymlinks_ReachesTheStore()
    {
        var store = CommandLineRunner.ComposeContentStore(ParseServing("--follow-symlinks", "http://127.0.0.1:0/"), TimeProvider.System);

        Assert.AreEqual(new ContentExposureOptions { FollowSymbolicLinks = true }, store.ExposureOptions);
    }

    [TestMethod]
    public void ComposeContentStore_ServeDotFiles_ReachesTheStore()
    {
        var store = CommandLineRunner.ComposeContentStore(ParseServing("--serve-dot-files", "http://127.0.0.1:0/"), TimeProvider.System);

        Assert.AreEqual(new ContentExposureOptions { ServeDotFiles = true }, store.ExposureOptions);
    }

    [TestMethod]
    public void ComposeContentStore_MaxFilesize_ReachesTheStore()
    {
        var store = CommandLineRunner.ComposeContentStore(ParseServing("--max-filesize", "4096", "http://127.0.0.1:0/"), TimeProvider.System);

        Assert.AreEqual(new ContentExposureOptions { MaxUploadBytes = 4096 }, store.ExposureOptions);
    }

    [TestMethod]
    public void ComposeContentStore_DataDirectory_ServesItsFullPathOnDisk()
    {
        var commandLine = ParseServing("--directory", "served", "http://127.0.0.1:0/");

        var store = CommandLineRunner.ComposeContentStore(commandLine, TimeProvider.System);

        Assert.AreEqual(Path.GetFullPath("served"), store.ServedRoot);
        Assert.IsInstanceOfType<DiskContentFileSystem>(
            CommandLineRunner.ComposeContentFileSystem(commandLine, TimeProvider.System));
    }

    [TestMethod]
    public void ComposeContentStore_NoDirectory_ServesANewInMemoryFileSystemAtItsRoot()
    {
        var commandLine = ParseServing("http://127.0.0.1:0/");

        var store = CommandLineRunner.ComposeContentStore(commandLine, TimeProvider.System);

        Assert.AreEqual(InMemoryContentFileSystem.RootPath, store.ServedRoot);
        var fileSystem = CommandLineRunner.ComposeContentFileSystem(commandLine, TimeProvider.System);
        Assert.IsInstanceOfType<InMemoryContentFileSystem>(fileSystem);
        Assert.AreEqual(0L, ((InMemoryContentFileSystem)fileSystem).TotalBytes);
        Assert.AreNotSame(fileSystem, CommandLineRunner.ComposeContentFileSystem(commandLine, TimeProvider.System));
    }

    [TestMethod]
    public async Task ComposeContentStore_NoDirectoryWithAllowUploads_KeepsAnUploadInMemoryAndServesItsBytesBack()
    {
        byte[] uploaded = [0x00, 0x01, 0xFE, 0xFF, (byte)'s', (byte)'u', (byte)'r', (byte)'l'];
        var store = CommandLineRunner.ComposeContentStore(
            ParseServing("--allow-uploads", "tftp://127.0.0.1:0/"), TimeProvider.System);
        var mapping = store.MapRequestPath("/up.bin");

        using var source = new MemoryStream(uploaded);
        var result = await store.WriteUploadAsync(mapping, source, TestContext.CancellationToken);
        using var readBack = new MemoryStream();
        var written = store.MapRequestPath("/up.bin");
        await store.CopyFileBytesAsync(
            written, ContentByteRange.WholeFile(uploaded.Length), readBack, TestContext.CancellationToken);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual(uploaded, readBack.ToArray());
        Assert.IsFalse(File.Exists(Path.Combine(InMemoryContentFileSystem.RootPath, "up.bin")));
    }

    [TestMethod]
    public async Task ComposeContentStore_NoDirectoryWithoutAllowUploads_RefusesTheUpload()
    {
        var store = CommandLineRunner.ComposeContentStore(ParseServing("tftp://127.0.0.1:0/"), TimeProvider.System);
        var mapping = store.MapRequestPath("/up.bin");

        using var source = new MemoryStream([1, 2, 3]);
        var result = await store.WriteUploadAsync(mapping, source, TestContext.CancellationToken);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
        Assert.AreEqual(ContentEntryKind.None, store.GetEntryKind(store.MapRequestPath("/up.bin")));
    }

    [TestMethod]
    public void ComposeConnectionLimits_NoLimitOption_EnforcesAdr0006Defaults()
    {
        var limits = CommandLineRunner.ComposeConnectionLimits(ParseServing("http://127.0.0.1:0/"));

        Assert.AreEqual(ConnectionLimits.Default, limits);
    }

    [TestMethod]
    public void ComposeConnectionLimits_MaxConnections_ReachesTheEngine()
    {
        var limits = CommandLineRunner.ComposeConnectionLimits(ParseServing("--max-connections", "7", "http://127.0.0.1:0/"));

        Assert.AreEqual(ConnectionLimits.Default with { MaxConnections = 7 }, limits);
    }

    [TestMethod]
    public void ComposeConnectionLimits_MaxConnectionsPerAddress_ReachesTheEngine()
    {
        var limits = CommandLineRunner.ComposeConnectionLimits(
            ParseServing("--max-connections-per-address", "3", "http://127.0.0.1:0/"));

        Assert.AreEqual(ConnectionLimits.Default with { MaxConnectionsPerAddress = 3 }, limits);
    }

    [TestMethod]
    public void ComposeConnectionLimits_IdleTimeout_ReachesTheEngine()
    {
        var limits = CommandLineRunner.ComposeConnectionLimits(ParseServing("--idle-timeout", "2.5", "http://127.0.0.1:0/"));

        Assert.AreEqual(ConnectionLimits.Default with { IdleTimeout = TimeSpan.FromSeconds(2.5) }, limits);
    }

    [TestMethod]
    [DataRow("-m")]
    [DataRow("--max-time")]
    public void ComposeConnectionLimits_MaxTime_ReachesTheEngine(string option)
    {
        var limits = CommandLineRunner.ComposeConnectionLimits(ParseServing(option, "90", "http://127.0.0.1:0/"));

        Assert.AreEqual(ConnectionLimits.Default with { MaxExchangeDuration = TimeSpan.FromSeconds(90) }, limits);
    }

    [TestMethod]
    public void ComposeConnectionLimits_EveryLimitZero_EnforcesNoLimit()
    {
        var limits = CommandLineRunner.ComposeConnectionLimits(ParseServing(
            "--max-connections", "0", "--max-connections-per-address", "0", "--idle-timeout", "0", "--max-time", "0",
            "http://127.0.0.1:0/"));

        Assert.AreEqual(ConnectionLimits.None, limits);
    }

    [TestMethod]
    [DataRow("--idle-timeout")]
    [DataRow("--max-time")]
    public async Task RunAsync_DurationAboveTheLongestTimeout_WritesTheRefusalAndReturnsFailedInit(string option)
    {
        var aboveMaxTimeout = (ConnectionLimits.MaxTimeout.TotalSeconds + 1).ToString(CultureInfo.InvariantCulture);
        var factory = new FakeListenerFactory();

        var (exitCode, output, error) = await RunAsync(factory, option, aboveMaxTimeout, "http://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(
            $"surl: option {option}: expected a proper numerical parameter" + NewLine
            + "surl: try 'surl --help' for more information" + NewLine,
            error);
        Assert.IsEmpty(factory.StartedListenUrls);
    }

    private static SurlCommandLine ParseServing(params string[] args) =>
        CommandLineParser.Parse(args).CommandLine ?? throw new AssertFailedException("The command line was refused.");

    private static bool AnyDirectoryOpens(string path) => true;

    private Task<(SurlExitCode ExitCode, string Output, string Error)> RunAsync(
        FakeListenerFactory factory, params string[] args) =>
        RunAsync(factory, AnyDirectoryOpens, args);

    private async Task<(SurlExitCode ExitCode, string Output, string Error)> RunAsync(
        FakeListenerFactory factory, Func<string, bool> canOpenDataDirectory, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new CommandLineRunner(_ => factory, canOpenDataDirectory, TimeProvider.System)
            .RunAsync(args, output, error, TestContext.CancellationToken);

        return (exitCode, output.ToString(), error.ToString());
    }
}
