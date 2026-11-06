using Surl.Cli;
using Surl.Composition;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> composes the one message bus the MQTT server keeps its
/// retained messages on: loaded from <c>&lt;data directory&gt;/.surl</c> before any listener binds
/// with <c>--directory</c>, an old MQTT 3.1.1 <c>.surl/mqtt/retained-messages</c> read when the
/// bus has no <c>retained</c> file, and in memory only without it (ADR-0100, decision 3.7). No
/// test here touches the disk: the data directory is read through
/// <see cref="UnitTestReadOnlyContentFileSystem"/>.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerMessageBusTests
{
    private static readonly string NewLine = Environment.NewLine;

    private static readonly string BusRetainedPath =
        Path.Join(Path.GetFullPath("served"), ".surl", MessageBusFiles.StateFolderName, MessageBusFiles.RetainedFileName);

    private static readonly string OldMqttRetainedMessageFilePath =
        Path.Join(Path.GetFullPath("served"), ".surl", "mqtt", "retained-messages");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_DataDirectoryWithAMalformedBusRetainedFile_WritesCouldNotReadAndReturnsCouldNotReadFileWithoutStartingAListener()
    {
        var (exitCode, error, startedListenUrls) = await RunRefusedAsync(BusRetainedPath, "not the header"u8.ToArray());

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual($"surl: (37) Could not read {BusRetainedPath}: not a message bus file" + NewLine, error);
        Assert.IsEmpty(startedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectoryWithAMalformedOldMqttRetainedMessageFile_WritesCouldNotReadNotARetainedMessageFile()
    {
        var (exitCode, error, startedListenUrls) = await RunRefusedAsync(OldMqttRetainedMessageFilePath, "not the header"u8.ToArray());

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual($"surl: (37) Could not read {OldMqttRetainedMessageFilePath}: not a retained-message file" + NewLine, error);
        Assert.IsEmpty(startedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectory_LoadsTheBusRetainedFileBeforeAnyListenerStarts()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[BusRetainedPath] = MessageBusFiles.RetainedHeader.ToArray();
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
            .RunAsync(["--directory", "served", "mqtt://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(string.Empty, error.ToString());
        CollectionAssert.Contains(accessedBeforeTheListenerFactory, BusRetainedPath);
        CollectionAssert.DoesNotContain(accessedBeforeTheListenerFactory, OldMqttRetainedMessageFilePath);
        CollectionAssert.AreEqual(fileSystem.AccessedPaths, accessedBeforeTheListenerFactory);
    }

    [TestMethod]
    public async Task RunAsync_StoppedWhileTheBusLoads_ReturnsOkWithoutStartingAListener()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[BusRetainedPath] = MessageBusFiles.RetainedHeader.ToArray();
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new CommandLineRunner(
                _ => factory,
                _ => true,
                _ =>
                {
                    stop.Cancel();
                    return DataDirectoryLockOutcome.Taken(new FakeLockHolder());
                },
                TimeProvider.System,
                fileSystem)
            .RunAsync(["--directory", "served", "mqtt://127.0.0.1:0/"], output, error, stop.Token);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(string.Empty, error.ToString());
        Assert.IsEmpty(factory.StartedListenUrls);
    }

    [TestMethod]
    public async Task RunAsync_LoadCancelledWithoutAStop_IsNotAnsweredAsAStop()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem { OpenFailure = new OperationCanceledException() };
        fileSystem.Files[BusRetainedPath] = MessageBusFiles.RetainedHeader.ToArray();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var runner = new CommandLineRunner(
            _ => new FakeListenerFactory(),
            _ => true,
            _ => DataDirectoryLockOutcome.Taken(new FakeLockHolder()),
            TimeProvider.System,
            fileSystem);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => runner.RunAsync(
                ["--directory", "served", "mqtt://127.0.0.1:0/"], output, error, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RunAsync_NoDirectory_ReadsNoBusFileAndServesInMemory()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(
                _ => factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System, fileSystem)
            .RunAsync(["mqtt://127.0.0.1:0/"], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsEmpty(fileSystem.AccessedPaths);
    }

    [TestMethod]
    public async Task RunAsync_VersionWithDirectory_ComposesNoPersistenceAndTouchesNoFile()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[BusRetainedPath] = "not the header"u8.ToArray();
        var lockedPaths = new List<string>();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new CommandLineRunner(
                _ => new FakeListenerFactory(),
                _ => true,
                path =>
                {
                    lockedPaths.Add(path);
                    return DataDirectoryLockOutcome.Taken(new FakeLockHolder());
                },
                TimeProvider.System,
                fileSystem)
            .RunAsync(["--directory", "served", "--version"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsEmpty(fileSystem.AccessedPaths);
        Assert.IsEmpty(lockedPaths);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    private async Task<(SurlExitCode ExitCode, string Error, IReadOnlyCollection<string> StartedListenUrls)> RunRefusedAsync(string path, byte[] bytes)
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[path] = bytes;
        var factory = new FakeListenerFactory();
        var lockHolder = new FakeLockHolder();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await new CommandLineRunner(
                _ => factory,
                _ => true,
                _ => DataDirectoryLockOutcome.Taken(lockHolder),
                TimeProvider.System,
                fileSystem)
            .RunAsync(["--directory", "served", "mqtt://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.IsTrue(lockHolder.Disposed);
        return (exitCode, error.ToString(), [.. factory.StartedListenUrls.Select(url => url.ToString())]);
    }
}
