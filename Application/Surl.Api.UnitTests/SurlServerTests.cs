using Surl.Cli;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

[TestClass]
public sealed class SurlServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartAsync_HttpOnLoopbackPortZero_ReturnsOnceBoundWithTheFakesPortInTheBoundUrl()
    {
        var seams = new ApiTestSeams();

        await using var server = await SurlServer.StartAsync(
            ApiTestSeams.Options("http://127.0.0.1:0"), seams.Host, TestContext.CancellationToken);

        Assert.AreEqual(new Uri($"http://127.0.0.1:{FakeListenerFactory.BoundPort}/"), server.BoundUrl);
        Assert.AreEqual(FakeListenerFactory.BoundPort, server.BoundUrl.Port);
        Assert.AreEqual(1, seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task StartAsync_TwoListenUrls_ReportsTwoBoundUrlsInOrder()
    {
        var seams = new ApiTestSeams();

        await using var server = await SurlServer.StartAsync(
            ApiTestSeams.Options("http://127.0.0.1:0", "ftp://[::1]:0"), seams.Host, TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[]
            {
                new Uri($"http://127.0.0.1:{FakeListenerFactory.BoundPort}/"),
                new Uri($"ftp://[::1]:{FakeListenerFactory.BoundPort}/"),
            },
            server.BoundUrls.ToArray());
    }

    [TestMethod]
    public async Task StartAsync_HttpsWithoutCertificate_ThrowsCertificateProblemWithSurlsText()
    {
        var seams = new ApiTestSeams();

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(() => SurlServer.StartAsync(
            ApiTestSeams.Options("https://127.0.0.1:0"), seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.CertificateProblem, exception.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(58) https://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" },
            exception.Lines.ToArray());
        Assert.AreEqual(exception.Lines[0], exception.Message);
        Assert.AreEqual(0, seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task StartAsync_ProductionSeamsAndARefusedStart_ThrowsBeforeAnyListenerBinds()
    {
        var options = ApiTestSeams.Options("https://127.0.0.1:0");
        options.AreExtensionsTurnedOn = false;

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.CertificateProblem, exception.ExitCode);
    }

    [TestMethod]
    public async Task StartAsync_ARefusedCommandLine_ThrowsWithTheParsersExitCodeAndText()
    {
        var seams = new ApiTestSeams();
        var expected = CommandLineParser.Parse(["nosuch://127.0.0.1:0"]).Failure!;

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(() => SurlServer.StartAsync(
            ApiTestSeams.Options("nosuch://127.0.0.1:0"), seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exception.ExitCode);
        CollectionAssert.AreEqual(
            SurlServerArguments.ToStartFailure(expected).Lines.ToArray(), exception.Lines.ToArray());
    }

    [TestMethod]
    public async Task StartAsync_AListenerThatCannotBind_ThrowsTheBindFailureAndHoldsNoLock()
    {
        var seams = new ApiTestSeams(new FakeListenerFactory { BindFails = true });
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.DataDirectory = "served";

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.BindFailed, exception.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(45) Could not bind http://127.0.0.1:0/: Address already in use" }, exception.Lines.ToArray());
        Assert.IsFalse(seams.IsHeld("served"));
    }

    [TestMethod]
    public async Task StartAsync_CancelledBeforeEveryListenerBinds_ThrowsOperationCanceledAndHoldsNoLock()
    {
        var seams = new ApiTestSeams(new FakeListenerFactory { StartWaitsUntilCancelled = true });
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.DataDirectory = "served";
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => SurlServer.StartAsync(options, seams.Host, cancel.Token));

        Assert.IsFalse(seams.IsHeld("served"));
    }

    [TestMethod]
    public async Task StopAsync_ThenDisposeAsync_EndsServingWithNoException()
    {
        var seams = new ApiTestSeams();
        var server = await SurlServer.StartAsync(
            ApiTestSeams.Options("http://127.0.0.1:0"), seams.Host, TestContext.CancellationToken);

        var exitCode = await server.StopAsync(TestContext.CancellationToken);
        await server.DisposeAsync();

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(0, seams.ListenerFactory.OpenListenerCount);
        Assert.AreEqual(SurlExitCode.Ok, await server.StopAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DisposeAsync_WithoutStopping_StopsServingAndReleasesTheLock()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.DataDirectory = "served";
        var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        await server.DisposeAsync();

        Assert.AreEqual(0, seams.ListenerFactory.OpenListenerCount);
        Assert.IsFalse(seams.IsHeld("served"));
    }

    [TestMethod]
    public async Task StartAsync_ASecondServerOnTheSameDataDirectory_IsRefusedWith124WhileTheFirstRuns()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.DataDirectory = "served";
        await using var first = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual((SurlExitCode)124, exception.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(124) Directory served is in use by another surl process" }, exception.Lines.ToArray());
        Assert.IsTrue(seams.IsHeld("served"));
    }

    [TestMethod]
    public async Task StartAsync_WithALog_WritesTheStatusLineAsSurlDoes()
    {
        var seams = new ApiTestSeams();
        var log = new StringWriter();
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.Log = log;

        await using (await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken))
        {
        }

        Assert.AreEqual($"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/" + Environment.NewLine, log.ToString());
    }

    [TestMethod]
    public async Task StartAsync_AtTheTraceLevel_ServesWithTheTraceDump()
    {
        var seams = new ApiTestSeams();
        var log = new StringWriter();
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.Log = log;
        options.LogLevel = LogLevel.Trace;

        var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.Ok, await server.StopAsync(TestContext.CancellationToken));
        StringAssert.Contains(log.ToString(), "Listening on http://127.0.0.1:");
    }

    [TestMethod]
    public async Task StartAsync_ExtensionsLeftToTheProcess_ReadsSurlExtensions()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.AreExtensionsTurnedOn = null;

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        Assert.HasCount(1, server.BoundUrls);
    }

    [TestMethod]
    public async Task StartAsync_ExtensionsTurnedOnByTheOptions_StartsWithoutReadingTheProcess()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.AreExtensionsTurnedOn = true;

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        Assert.HasCount(1, server.BoundUrls);
    }

    [TestMethod]
    public async Task StartAsync_InMemoryFiles_StartsWithThem()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("http://127.0.0.1:0");
        options.Files["docs/readme.txt"] = "hello"u8.ToArray();

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        Assert.HasCount(1, server.BoundUrls);
    }

    [TestMethod]
    public async Task StartAsync_NullOptions_ThrowsArgumentNull()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => SurlServer.StartAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public void CreateExchangeLogFactory_BelowTrace_IsTheLevelledLog()
    {
        var commandLine = CommandLineParser.Parse(["-v", "http://127.0.0.1:0"]).CommandLine!;

        var factory = SurlServer.CreateExchangeLogFactory(commandLine, TextWriter.Null, TimeProvider.System);

        Assert.IsInstanceOfType<LevelledExchangeLogFactory>(factory);
    }
}
