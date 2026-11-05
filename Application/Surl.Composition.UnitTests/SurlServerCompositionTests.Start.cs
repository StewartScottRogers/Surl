using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ComposeAsync_HttpOnPortZero_ReportsTheBoundUrlWithTheFakesPortOnceEveryListenerIsBound()
    {
        var seams = new StartTestSeams();
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("http://127.0.0.1:0", "tftp://127.0.0.1:0"), seams.Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        var boundUrls = await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var outcome = await serve;

        Assert.IsNull(start.Failure);
        CollectionAssert.AreEqual(
            new[]
            {
                new ListenUrl("http", "127.0.0.1", 0).WithBoundPort(FakeListenerFactory.BoundPort),
                new ListenUrl("tftp", "127.0.0.1", 0).WithBoundPort(FakeListenerFactory.BoundPort),
            },
            boundUrls.ToArray());
        Assert.AreEqual(
            $"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}"
            + $"Listening on tftp://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}",
            log.ToString());
        Assert.AreEqual(new SurlServingOutcome(SurlExitCode.Ok, null), outcome);
    }

    [TestMethod]
    public async Task ComposeAsync_HttpsWithoutACertificate_FailsWithCertificateProblemAndTheTextSurlWrites()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("https://127.0.0.1:8443"), new StartTestSeams().Seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(58) https://127.0.0.1:8443/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" },
            start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task ComposeAsync_SftpWithoutAHostKey_FailsWithFailedInit()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("sftp://127.0.0.1:2222"), new StartTestSeams().Seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(2) sftp://127.0.0.1:2222/ needs a host key: give --hostkey <file>, or --throwaway-hostkey for a throwaway one" },
            start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task ComposeAsync_ASecondStartOnALockedDataDirectory_FailsWithDataDirectoryInUseUntilTheFirstIsDisposed()
    {
        var seams = new StartTestSeams();
        var commandLine = StartTestSeams.Parse("--directory", "served", "http://127.0.0.1:0");
        var first = await SurlServerComposition.ComposeAsync(commandLine, seams.Seams, TestContext.CancellationToken);

        var second = await SurlServerComposition.ComposeAsync(commandLine, seams.Seams, TestContext.CancellationToken);
        await first.Serving!.DisposeAsync();
        var third = await SurlServerComposition.ComposeAsync(commandLine, seams.Seams, TestContext.CancellationToken);
        await third.Serving!.DisposeAsync();

        Assert.IsNull(second.Serving);
        Assert.AreEqual((SurlExitCode)124, second.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(124) Directory served is in use by another surl process" }, second.Failure.Lines.ToArray());
        Assert.IsNull(third.Failure);
        Assert.IsFalse(seams.IsHeld("served"));
    }

    [TestMethod]
    public async Task ComposeAsync_GssapiWithoutAKeytab_FailsWithFailedInitBeforeAnythingElse()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--auth", "gssapi", "https://127.0.0.1:0"), new StartTestSeams().Seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(2) --auth gssapi needs --keytab" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task ComposeAsync_ADataDirectoryThatCannotBeOpened_FailsWithCouldNotReadFile()
    {
        var seams = new StartTestSeams().Seams with { CanOpenDataDirectory = _ => false };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--directory", "missing", "http://127.0.0.1:0"), seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(37) Could not open directory missing" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task ComposeAsync_AnUnreadableUserFile_FailsWithCouldNotReadFileThroughTheReadStartFileSeam()
    {
        var seams = new StartTestSeams().Seams with { ReadStartFile = path => throw new FileNotFoundException(path) };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--user-file", "users.txt", "http://127.0.0.1:0"), seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(37) Could not read user file users.txt" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "--group", "ops=dave" }, "(2) option --group: member dave names no account or group", DisplayName = "An unknown member")]
    [DataRow(new[] { "--group", "ops=@ops" }, "(2) option --group: group ops includes itself", DisplayName = "A cycle")]
    public async Task ComposeAsync_ARefusedGroup_FailsWithFailedInitBeforeAnyListenerBinds(string[] arguments, string message)
    {
        var listenerFactory = new FakeListenerFactory();

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse([.. arguments, "-u", "alice:secret", "http://127.0.0.1:0"]),
            new StartTestSeams(listenerFactory).Seams,
            TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { message }, start.Failure.Lines.ToArray());
        Assert.IsNull(start.Serving);
    }

    [TestMethod]
    public async Task ComposeAsync_AGroupOfAUserFileAccount_ExpandsAfterTheUserFileIsRead()
    {
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => "carol:secret\n"u8.ToArray() };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--group", "ops=carol", "--user-file", "users.txt", "http://127.0.0.1:0"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving;

        Assert.IsNull(start.Failure, start.Failure?.Lines.FirstOrDefault());
    }

    [TestMethod]
    public async Task ComposeAsync_AMalformedHostKeyFile_FailsWithFailedInit()
    {
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => "not a key"u8.ToArray() };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--hostkey", "host_key", "sftp://127.0.0.1:0"), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
    }

    [TestMethod]
    public async Task ComposeAsync_ACertificateFileThatCannotBeLoaded_FailsWithItsExitCodeAndReleasesTheLock()
    {
        var seams = new StartTestSeams();

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--directory", "served", "--cert", "no-such-cert.pem", "https://127.0.0.1:0"),
            seams.Seams,
            TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
        Assert.IsFalse(seams.IsHeld("served"));
    }

    [TestMethod]
    public async Task ComposeAsync_TheExtendedServersSeamThrows_ReleasesTheLockAndRethrows()
    {
        var seams = new StartTestSeams();
        var throwing = seams.Seams with
        {
            AreExtensionsTurnedOn = true,
            ComposeExtendedProtocolServers = _ => throw new InvalidOperationException("broken"),
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--directory", "served", "http://127.0.0.1:0"), throwing, TestContext.CancellationToken));

        Assert.IsFalse(seams.IsHeld("served"));
    }

    [TestMethod]
    public async Task ComposeAsync_CancelledWhileLoadingTheServiceState_StopsWithOkAndNoLines()
    {
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        var seams = new StartTestSeams().Seams with { DataDirectoryFileSystem = new UnitTestCancellingContentFileSystem() };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--directory", "served", "http://127.0.0.1:0"), seams, stop.Token);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.Ok, start.Failure!.ExitCode);
        Assert.IsEmpty(start.Failure.Lines);
    }

    [TestMethod]
    public async Task ComposeAsync_NullArguments_ThrowArgumentNullException()
    {
        var seams = new StartTestSeams().Seams;

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => SurlServerComposition.ComposeAsync(null!, seams, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => SurlServerComposition.ComposeAsync(StartTestSeams.Parse("http://127.0.0.1:0"), null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public void ComposeContentFileSystem_ContentWithoutADataDirectory_IsTheGivenFileSystem()
    {
        var given = new InMemoryContentFileSystem(TimeProvider.System);

        var chosen = SurlServerComposition.ComposeContentFileSystem(
            StartTestSeams.Parse("http://127.0.0.1:0"), TimeProvider.System, new DiskContentFileSystem(), given);

        Assert.AreSame(given, chosen);
    }

    [TestMethod]
    [DataRow(ListenerBindFailure.AddressInUse, "Address already in use")]
    [DataRow(ListenerBindFailure.AddressNotAvailable, "Address not available")]
    [DataRow(ListenerBindFailure.PermissionDenied, "Permission denied")]
    [DataRow(ListenerBindFailure.Other, "Bind failed")]
    public void FormatBindFailure_EachFailure_NamesItsReason(ListenerBindFailure failure, string reason)
    {
        var bindFailure = new ListenerBindException(new ListenUrl("http", "0.0.0.0", 80), null, failure, null);

        var message = SurlServerComposition.FormatBindFailure(bindFailure);

        Assert.AreEqual($"(45) Could not bind http://0.0.0.0:80/: {reason}", message);
    }

    [TestMethod]
    public void FormatBindFailure_HostNotFound_IsCouldNotResolveHost()
    {
        var bindFailure = new ListenerBindException(
            new ListenUrl("http", "no-such-host.invalid", 80), null, ListenerBindFailure.HostNotFound, null);

        var message = SurlServerComposition.FormatBindFailure(bindFailure);

        Assert.AreEqual("(6) Could not resolve host: no-such-host.invalid", message);
    }
}
