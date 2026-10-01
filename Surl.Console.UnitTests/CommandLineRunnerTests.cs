using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

[TestClass]
public sealed class CommandLineRunnerTests
{
    private static readonly string NewLine = Environment.NewLine;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(null, new[] { "--help" })]
    [DataRow(null, new[] { "-h" })]
    [DataRow("all", new[] { "--help", "all" })]
    [DataRow("category", new[] { "--help", "category" })]
    [DataRow("tls", new[] { "-h", "tls" })]
    [DataRow("--max-line", new[] { "--help", "--max-line" })]
    [DataRow("nosuch", new[] { "--help", "nosuch" }, DisplayName = "Unknown subject")]
    public async Task RunAsync_Help_WritesTheSubjectsTextToOutputAndReturnsOk(string? subject, string[] arguments)
    {
        var (exitCode, output, error) = await RunAsync(new FakeListenerFactory(), arguments);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(HelpText.Answer(subject).Output, output);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public async Task RunAsync_HelpForAnOptionSurlDoesNotHave_WritesTheIncorrectOptionLineToErrorAndReturnsOk()
    {
        var (exitCode, output, error) = await RunAsync(new FakeListenerFactory(), "--help", "--nosuch");

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual("surl: Incorrect option name to show help for, see surl -h" + NewLine, error);
    }

    [TestMethod]
    [DataRow(null, new[] { "--aihelp" })]
    [DataRow("mqtt", new[] { "--aihelp", "mqtt" })]
    [DataRow("all", new[] { "--aihelp", "all" })]
    [DataRow(null, new[] { "-s", "--aihelp" }, DisplayName = "At the none level too")]
    [DataRow("nosuch", new[] { "--aihelp", "nosuch" }, DisplayName = "Unknown topic")]
    [DataRow("--user", new[] { "--aihelp", "--user" }, DisplayName = "Option-like topic")]
    public async Task RunAsync_AiHelp_WritesTheTopicsMarkdownToOutputAndReturnsOk(string? topic, string[] arguments)
    {
        var listenerFactory = new FakeListenerFactory();

        var (exitCode, output, error) = await RunAsync(listenerFactory, arguments);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(AiHelpText.Answer(topic).Output, output);
        Assert.AreEqual(string.Empty, error);
        Assert.IsEmpty(listenerFactory.StartedListenUrls);
    }

    [TestMethod]
    [DataRow("nosuch")]
    [DataRow("--user")]
    public async Task RunAsync_AiHelpUnknownTopic_WritesTheUnknownTopicAnswerAndReturnsOk(string topic)
    {
        var (exitCode, output, error) = await RunAsync(new FakeListenerFactory(), "--aihelp", topic);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        StringAssert.StartsWith(
            output,
            "# surl --aihelp: unknown topic" + NewLine + NewLine + "Unknown topic provided, here is a list of all topics:" + NewLine);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public async Task RunAsync_Version_WritesVersionWithEveryRegisteredSchemeAndReturnsOk()
    {
        var informationalVersion = typeof(CommandLineRunner).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        var (exitCode, output, error) = await RunAsync(new FakeListenerFactory(), "--version");

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(VersionText.Compose(informationalVersion, RuntimeInformation.RuntimeIdentifier, ["dict", "ftp", "ftps", "gopher", "gophers", "http", "https", "imap", "imaps", "mqtt", "mqtts", "pop3", "pop3s", "scp", "sftp", "smb", "smbs", "smtp", "smtps", "telnet", "tftp", "ws", "wss"]), output);
        StringAssert.EndsWith(output, NewLine + "Protocols: dict ftp ftps gopher gophers http https imap imaps mqtt mqtts pop3 pop3s scp sftp smb smbs smtp smtps telnet tftp ws wss" + NewLine);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    [DataRow("--manual")]
    [DataRow("-M")]
    [DataRow("-s", "--manual", DisplayName = "At the none level too")]
    public async Task RunAsync_Manual_WritesTheManualToOutputAndReturnsOk(params string[] arguments)
    {
        var listenerFactory = new FakeListenerFactory();

        var (exitCode, output, error) = await RunAsync(listenerFactory, arguments);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(ManualText.Text, output);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public async Task RunAsync_UnknownOption_WritesTheRefusalAndTheTryLineAndReturnsFailedInit()
    {
        var (exitCode, output, error) = await RunAsync(new FakeListenerFactory(), "--bogus", "http://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(
            "surl: option --bogus: is unknown" + NewLine + "surl: try 'surl --help' or 'surl --manual' for more information" + NewLine,
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
    public async Task RunAsync_NoDirectory_NeverProbesNorLocksADataDirectoryAndServesInMemory()
    {
        var factory = new FakeListenerFactory();
        var probedPaths = new List<string>();
        var lockedPaths = new List<string>();
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
                path =>
                {
                    lockedPaths.Add(path);
                    return DataDirectoryLockOutcome.InUse(path);
                },
                TimeProvider.System)
            .RunAsync(["http://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsEmpty(probedPaths);
        Assert.IsEmpty(lockedPaths);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    [DataRow("--help")]
    [DataRow("--version")]
    public async Task RunAsync_HelpOrVersionWithDirectory_NeverTakesTheDataDirectoryLock(string option)
    {
        var lockedPaths = new List<string>();

        var (exitCode, _, _) = await RunWithLockAsync(
            _ => new FakeListenerFactory(),
            path =>
            {
                lockedPaths.Add(path);
                return DataDirectoryLockOutcome.InUse(path);
            },
            "--directory",
            "served",
            option);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsEmpty(lockedPaths);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectory_TakesTheLockBeforeTheListenerFactoryIsAskedForAnyListenerAndDisposesItWhenCancelled()
    {
        var factory = new FakeListenerFactory();
        var events = new List<string>();
        var holder = new FakeLockHolder();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(
                _ =>
                {
                    events.Add("listener factory created");
                    return factory;
                },
                AnyDirectoryOpens,
                path =>
                {
                    events.Add($"lock taken on {path} with {factory.StartedListenUrls.Count} listeners started");
                    return DataDirectoryLockOutcome.Taken(holder);
                },
                TimeProvider.System,
                new UnitTestReadOnlyContentFileSystem())
            .RunAsync(["--directory", "served", "http://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        var disposedWhileServing = holder.Disposed;
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        CollectionAssert.AreEqual(
            new[] { "lock taken on served with 0 listeners started", "listener factory created" }, events);
        Assert.IsFalse(disposedWhileServing);
        Assert.IsTrue(holder.Disposed);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_DataDirectoryInUse_WritesInUseAndReturnsDataDirectoryInUseWithoutStartingAListener()
    {
        var factoryCreated = false;

        var (exitCode, output, error) = await RunWithLockAsync(
            _ =>
            {
                factoryCreated = true;
                return new FakeListenerFactory();
            },
            DataDirectoryLockOutcome.InUse,
            "--directory",
            "served",
            "http://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.DataDirectoryInUse, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual("surl: (124) Directory served is in use by another surl process" + NewLine, error);
        Assert.IsFalse(factoryCreated);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectoryLockCannotBeCreated_WritesCouldNotCreateAndReturnsCouldNotWriteFile()
    {
        var factory = new FakeListenerFactory();

        var (exitCode, _, error) = await RunWithLockAsync(
            _ => factory,
            _ => DataDirectoryLockOutcome.CouldNotCreate("full/served/.surl", new UnauthorizedAccessException("Access denied.")),
            "--directory",
            "served",
            "http://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CouldNotWriteFile, exitCode);
        Assert.AreEqual("surl: (23) Could not create full/served/.surl: Access denied." + NewLine, error);
        Assert.IsEmpty(factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectoryAndSchemeWithNoRegisteredServer_ReturnsUnsupportedProtocolWithoutTakingTheLock()
    {
        var lockedPaths = new List<string>();

        var (exitCode, _, _) = await RunWithLockAsync(
            _ => new FakeListenerFactory(),
            path =>
            {
                lockedPaths.Add(path);
                return DataDirectoryLockOutcome.Taken(new FakeLockHolder());
            },
            "--directory",
            "served",
            "rtsp://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.IsEmpty(lockedPaths);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectoryAndListenerCannotBind_DisposesTheLock()
    {
        var listenUrl = new ListenUrl("http", "127.0.0.1", 8080);
        var factory = new FakeListenerFactory
        {
            BindFailure = new ListenerBindException(
                listenUrl, new IPEndPoint(IPAddress.Loopback, 8080), ListenerBindFailure.AddressInUse, null),
        };
        var holder = new FakeLockHolder();

        var (exitCode, _, _) = await RunWithLockAsync(
            _ => factory, _ => DataDirectoryLockOutcome.Taken(holder), "--directory", "served", "http://127.0.0.1:8080/");

        Assert.AreEqual(SurlExitCode.BindFailed, exitCode);
        Assert.IsTrue(holder.Disposed);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectoryAndEngineThrows_DisposesTheLock()
    {
        var factory = new FakeListenerFactory { AcceptFailure = new InvalidOperationException("accept broke") };
        var holder = new FakeLockHolder();

        var (exitCode, _, _) = await RunWithLockAsync(
            _ => factory, _ => DataDirectoryLockOutcome.Taken(holder), "--directory", "served", "http://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.InternalError, exitCode);
        Assert.IsTrue(holder.Disposed);
    }

    [TestMethod]
    public async Task RunAsync_ListenUrls_StartsOneListenerEachWritesTheStatusLinesAndReturnsOkWhenCancelled()
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, AnyLockIsTaken, TimeProvider.System, new UnitTestReadOnlyContentFileSystem())
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

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, AnyLockIsTaken, TimeProvider.System, new UnitTestReadOnlyContentFileSystem())
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

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, AnyLockIsTaken, TimeProvider.System, new UnitTestReadOnlyContentFileSystem())
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

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, AnyLockIsTaken, TimeProvider.System, new UnitTestReadOnlyContentFileSystem())
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

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, AnyLockIsTaken, TimeProvider.System, new UnitTestReadOnlyContentFileSystem())
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

        var running = new CommandLineRunner(_ => factory, AnyDirectoryOpens, AnyLockIsTaken, TimeProvider.System, new UnitTestReadOnlyContentFileSystem())
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
    public void ComposeContentStore_DataDirectory_ServesItsFullPathThroughTheDataDirectoryFileSystem()
    {
        var commandLine = ParseServing("--directory", "served", "http://127.0.0.1:0/");

        var store = CommandLineRunner.ComposeContentStore(commandLine, TimeProvider.System);

        Assert.AreEqual(Path.GetFullPath("served"), store.ServedRoot);
        var dataDirectoryFileSystem = new UnitTestReadOnlyContentFileSystem();
        Assert.AreSame(
            dataDirectoryFileSystem,
            CommandLineRunner.ComposeContentFileSystem(commandLine, TimeProvider.System, dataDirectoryFileSystem));
    }

    [TestMethod]
    public void ComposeContentStore_NoDirectory_ServesANewInMemoryFileSystemAtItsRoot()
    {
        var commandLine = ParseServing("http://127.0.0.1:0/");

        var store = CommandLineRunner.ComposeContentStore(commandLine, TimeProvider.System);

        Assert.AreEqual(InMemoryContentFileSystem.RootPath, store.ServedRoot);
        var dataDirectoryFileSystem = new UnitTestReadOnlyContentFileSystem();
        var fileSystem = CommandLineRunner.ComposeContentFileSystem(commandLine, TimeProvider.System, dataDirectoryFileSystem);
        Assert.IsInstanceOfType<InMemoryContentFileSystem>(fileSystem);
        Assert.AreEqual(0L, ((InMemoryContentFileSystem)fileSystem).TotalBytes);
        Assert.AreNotSame(
            fileSystem, CommandLineRunner.ComposeContentFileSystem(commandLine, TimeProvider.System, dataDirectoryFileSystem));
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
            + "surl: try 'surl --help' or 'surl --manual' for more information" + NewLine,
            error);
        Assert.IsEmpty(factory.StartedListenUrls);
    }

    private static SurlCommandLine ParseServing(params string[] args) =>
        CommandLineParser.Parse(args).CommandLine ?? throw new AssertFailedException("The command line was refused.");

    private static bool AnyDirectoryOpens(string path) => true;

    private static DataDirectoryLockOutcome AnyLockIsTaken(string path) => DataDirectoryLockOutcome.Taken(new FakeLockHolder());

    private async Task<(SurlExitCode ExitCode, string Output, string Error)> RunWithLockAsync(
        Func<ServerTlsSettings?, IListenerFactory> createListenerFactory,
        Func<string, DataDirectoryLockOutcome> takeDataDirectoryLock,
        params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new CommandLineRunner(createListenerFactory, AnyDirectoryOpens, takeDataDirectoryLock, TimeProvider.System, new UnitTestReadOnlyContentFileSystem())
            .RunAsync(args, output, error, TestContext.CancellationToken);

        return (exitCode, output.ToString(), error.ToString());
    }

    private Task<(SurlExitCode ExitCode, string Output, string Error)> RunAsync(
        FakeListenerFactory factory, params string[] args) =>
        RunAsync(factory, AnyDirectoryOpens, args);

    private async Task<(SurlExitCode ExitCode, string Output, string Error)> RunAsync(
        FakeListenerFactory factory, Func<string, bool> canOpenDataDirectory, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new CommandLineRunner(_ => factory, canOpenDataDirectory, AnyLockIsTaken, TimeProvider.System, new UnitTestReadOnlyContentFileSystem())
            .RunAsync(args, output, error, TestContext.CancellationToken);

        return (exitCode, output.ToString(), error.ToString());
    }
}
