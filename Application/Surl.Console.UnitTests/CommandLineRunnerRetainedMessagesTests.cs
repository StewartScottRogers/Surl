using Surl.Cli;
using Surl.Content;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Mqtt;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> composes the MQTT server's retained messages: kept in
/// <c>&lt;data directory&gt;/.surl/mqtt</c> and loaded before any listener binds with
/// <c>--directory</c>, in memory only without it (ADR-0031, decision 6). No test here touches
/// the disk: the data directory is read through <see cref="UnitTestReadOnlyContentFileSystem"/>.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerRetainedMessagesTests
{
    private static readonly string NewLine = Environment.NewLine;

    // SURL-MQTT-RETAINED-1 LF, then topic "t" holding "hi" (ADR-0031, decision 6).
    private static readonly byte[] RetainedHiOnT =
        [.. "SURL-MQTT-RETAINED-1\n"u8, 0x00, 0x01, (byte)'t', 0x00, 0x00, 0x00, 0x02, (byte)'h', (byte)'i'];

    private static readonly string RetainedMessageFilePath =
        Path.Join(Path.GetFullPath("served"), ".surl", "mqtt", MqttRetainedMessageFile.FileName);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ComposeRetainedMessageFile_DataDirectory_KeepsItInTheDotSurlMqttFolderOfTheFullPath()
    {
        var commandLine = ParseServing("--directory", "served", "mqtt://127.0.0.1:0/");

        var file = CommandLineRunner.ComposeRetainedMessageFile(commandLine, new UnitTestReadOnlyContentFileSystem());

        Assert.IsNotNull(file);
        Assert.AreEqual(Path.Join(Path.GetFullPath("served"), ".surl", "mqtt"), file.StateFolderPath);
        Assert.AreEqual(RetainedMessageFilePath, file.FilePath);
    }

    [TestMethod]
    public void ComposeRetainedMessageFile_NoDirectory_ComposesNone()
    {
        var commandLine = ParseServing("mqtt://127.0.0.1:0/");

        var file = CommandLineRunner.ComposeRetainedMessageFile(commandLine, new UnitTestReadOnlyContentFileSystem());

        Assert.IsNull(file);
    }

    [TestMethod]
    public async Task LoadRetainedMessagesAsync_NoFile_GivesAnEmptyStoreWithTheDefaultBounds()
    {
        var (retainedMessages, failureMessage) =
            await CommandLineRunner.LoadRetainedMessagesAsync(null, TestContext.CancellationToken);

        Assert.IsNotNull(retainedMessages);
        Assert.AreEqual(MqttRetainedMessages.DefaultMaxTopics, retainedMessages.MaxTopics);
        Assert.AreEqual(MqttRetainedMessages.DefaultMaxTotalPayloadBytes, retainedMessages.MaxTotalPayloadBytes);
        Assert.IsNull(failureMessage);
    }

    [TestMethod]
    public async Task LoadRetainedMessagesAsync_FileHoldsAMessage_ReadsItAndGivesAStoreWithoutFailure()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[RetainedMessageFilePath] = RetainedHiOnT;
        var file = CommandLineRunner.ComposeRetainedMessageFile(ParseServing("--directory", "served", "mqtt://127.0.0.1:0/"), fileSystem);

        var (retainedMessages, failureMessage) =
            await CommandLineRunner.LoadRetainedMessagesAsync(file, TestContext.CancellationToken);

        Assert.IsNotNull(retainedMessages);
        Assert.IsNull(failureMessage);
        CollectionAssert.Contains(fileSystem.AccessedPaths, RetainedMessageFilePath);
    }

    [TestMethod]
    public async Task LoadRetainedMessagesAsync_MalformedFile_GivesCouldNotReadNotARetainedMessageFile()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[RetainedMessageFilePath] = "not the header"u8.ToArray();
        var file = CommandLineRunner.ComposeRetainedMessageFile(ParseServing("--directory", "served", "mqtt://127.0.0.1:0/"), fileSystem);

        var (retainedMessages, failureMessage) =
            await CommandLineRunner.LoadRetainedMessagesAsync(file, TestContext.CancellationToken);

        Assert.IsNull(retainedMessages);
        Assert.AreEqual($"(37) Could not read {RetainedMessageFilePath}: not a retained-message file", failureMessage);
    }

    [TestMethod]
    public async Task LoadRetainedMessagesAsync_FileCannotBeOpenedForAnIoFailure_GivesCouldNotReadWithTheExceptionMessage()
    {
        var failureMessage = await LoadUnopenableFileAsync(new IOException("The file is in use."));

        Assert.AreEqual($"(37) Could not read {RetainedMessageFilePath}: The file is in use.", failureMessage);
    }

    [TestMethod]
    public async Task LoadRetainedMessagesAsync_FileCannotBeOpenedForLackOfPermission_GivesCouldNotReadWithTheExceptionMessage()
    {
        var failureMessage = await LoadUnopenableFileAsync(new UnauthorizedAccessException("Access is denied."));

        Assert.AreEqual($"(37) Could not read {RetainedMessageFilePath}: Access is denied.", failureMessage);
    }

    [TestMethod]
    public async Task LoadRetainedMessagesAsync_AnyOtherFailure_IsNotAnsweredAsCouldNotRead()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => LoadUnopenableFileAsync(new InvalidOperationException("A defect.")));
    }

    [TestMethod]
    public async Task RunAsync_DataDirectoryWithAMalformedRetainedMessageFile_WritesCouldNotReadAndReturnsCouldNotReadFileWithoutStartingAListener()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[RetainedMessageFilePath] = "not the header"u8.ToArray();
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
            .RunAsync(["--directory", "served", "mqtt://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual($"surl: (37) Could not read {RetainedMessageFilePath}: not a retained-message file" + NewLine, error.ToString());
        CollectionAssert.AreEqual(new[] { "lock taken with 0 paths read" }, events);
        Assert.IsEmpty(factory.StartedListenUrls);
        Assert.IsTrue(lockHolder.Disposed);
    }

    [TestMethod]
    public async Task RunAsync_DataDirectory_LoadsTheRetainedMessageFileFromTheDotSurlMqttFolderBeforeAnyListenerStarts()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[RetainedMessageFilePath] = RetainedHiOnT;
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
        CollectionAssert.Contains(accessedBeforeTheListenerFactory, RetainedMessageFilePath);
        CollectionAssert.AreEqual(fileSystem.AccessedPaths, accessedBeforeTheListenerFactory);
    }

    [TestMethod]
    public async Task RunAsync_StoppedWhileTheRetainedMessageFileLoads_ReturnsOkWithoutStartingAListener()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[RetainedMessageFilePath] = RetainedHiOnT;
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
        fileSystem.Files[RetainedMessageFilePath] = RetainedHiOnT;
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
    public async Task RunAsync_NoDirectory_ReadsNoRetainedMessageFileAndServesInMemory()
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
        fileSystem.Files[RetainedMessageFilePath] = "not the header"u8.ToArray();
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

    private static SurlCommandLine ParseServing(params string[] args) =>
        CommandLineParser.Parse(args).CommandLine ?? throw new AssertFailedException("The command line was refused.");

    private async Task<string?> LoadUnopenableFileAsync(Exception openFailure)
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem { OpenFailure = openFailure };
        fileSystem.Files[RetainedMessageFilePath] = RetainedHiOnT;
        var file = CommandLineRunner.ComposeRetainedMessageFile(ParseServing("--directory", "served", "mqtt://127.0.0.1:0/"), fileSystem);

        var (retainedMessages, failureMessage) =
            await CommandLineRunner.LoadRetainedMessagesAsync(file, TestContext.CancellationToken);

        Assert.IsNull(retainedMessages);
        return failureMessage;
    }
}
