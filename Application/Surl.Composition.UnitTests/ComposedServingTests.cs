using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class ComposedServingTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_StartedThenCancelled_EndsWithOk()
    {
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var serving = await ComposeAsync(new StartTestSeams(), "http://127.0.0.1:0");

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, _ => bound.SetResult()), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var outcome = await serve;

        Assert.AreEqual(SurlExitCode.Ok, outcome.ExitCode);
        Assert.IsNull(outcome.Failure);
    }

    [TestMethod]
    public async Task ServeAsync_AListenerCannotBind_ReportsTheBindFailureAndNeverCallsAllListenersBound()
    {
        var listenUrl = new ListenUrl("http", "127.0.0.1", 80);
        var seams = new StartTestSeams(new FakeListenerFactory
        {
            BindFailure = new ListenerBindException(listenUrl, null, ListenerBindFailure.AddressInUse, null),
        });
        using var log = new StringWriter();
        var boundCalls = 0;
        await using var serving = await ComposeAsync(seams, "http://127.0.0.1:80");

        var outcome = await serving.ServeAsync(StartTestSeams.Outputs(log, _ => boundCalls++), TestContext.CancellationToken);

        Assert.AreEqual(outcome.ExitCode, outcome.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(45) Could not bind http://127.0.0.1:80/: Address already in use" }, outcome.Failure.Lines.ToArray());
        Assert.AreEqual(0, boundCalls);
        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public async Task ServeAsync_ServingFails_EndsWithInternalError()
    {
        var seams = new StartTestSeams(new FakeListenerFactory { AcceptFailure = new InvalidOperationException("accept broke") });
        using var log = new StringWriter();
        await using var serving = await ComposeAsync(seams, "http://127.0.0.1:0");

        var outcome = await serving.ServeAsync(StartTestSeams.Outputs(log), TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.InternalError, outcome.ExitCode);
        Assert.AreEqual(SurlExitCode.InternalError, outcome.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(125) Internal error: accept broke" }, outcome.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_SelfSignedAtVerbose_WritesTheThrowawayCertificateLinesBeforeTheStatusLine()
    {
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var serving = await ComposeAsync(new StartTestSeams(), "-v", "--self-signed", "https://127.0.0.1:0");

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, _ => bound.SetResult()), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        var fingerprint = serving.ThrowawayCertificateFingerprints.Single();
        Assert.AreEqual(
            "surl: warning: --self-signed: serving a throwaway certificate; clients must skip verification (curl -k)"
            + Environment.NewLine
            + $"* Serving a throwaway certificate, SHA-256 {fingerprint}{Environment.NewLine}"
            + $"Listening on https://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}",
            log.ToString());
    }

    [TestMethod]
    public async Task ServeAsync_GroupsAtVerbose_NoteEachGroupBeforeTheStatusLine()
    {
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var serving = await ComposeAsync(
            new StartTestSeams(), "-v", "-u", "alice:a", "-u", "bob:b", "--group", "ops=alice", "--group", "all=@ops,bob", "http://127.0.0.1:0");

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, _ => bound.SetResult()), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        Assert.AreEqual(
            $"* Group ops: 1 accounts{Environment.NewLine}"
            + $"* Group all: 2 accounts{Environment.NewLine}"
            + $"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}",
            log.ToString());
    }

    [TestMethod]
    [DataRow("-v", true)]
    [DataRow("--no-verbose", false)]
    public async Task ServeAsync_Http3_WritesOneStatusLineForTheSharedPortAndTheHttp3NoteFromVerboseUp(string level, bool writesNote)
    {
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var serving = await ComposeAsync(new StartTestSeams(), level, "--self-signed", "--http3", "https://127.0.0.1:0");

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        var boundListenUrls = await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var outcome = await serve;

        Assert.AreEqual(SurlExitCode.Ok, outcome.ExitCode);
        var lines = log.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        CollectionAssert.AreEqual(new[] { $"Listening on https://127.0.0.1:{FakeListenerFactory.BoundPort}/" }, lines.Where(line => line.StartsWith("Listening", StringComparison.Ordinal)).ToArray());
        Assert.AreEqual(writesNote, lines.Contains($"* Serving HTTP/3 over QUIC on UDP port {FakeListenerFactory.BoundPort}"));
        Assert.HasCount(1, boundListenUrls);
    }

    [TestMethod]
    public async Task ServeAsync_SilentLevel_WritesNoStatusLineAndNoThrowawayCertificateLine()
    {
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var serving = await ComposeAsync(new StartTestSeams(), "-s", "--self-signed", "https://127.0.0.1:0");

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, _ => bound.SetResult()), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public async Task ThrowawayCertificateFingerprints_NoSelfSigned_IsEmpty()
    {
        await using var serving = await ComposeAsync(new StartTestSeams(), "http://127.0.0.1:0");

        Assert.IsEmpty(serving.ThrowawayCertificateFingerprints);
    }

    [TestMethod]
    public async Task ServeAsync_ADataConnectionOpenerSeam_IsGivenTheTlsSettings()
    {
        var seams = new StartTestSeams();
        var openerCalls = 0;
        var withOpener = seams.Seams with
        {
            CreateDataConnectionOpener = tlsSettings =>
            {
                openerCalls++;
                return new SocketDataConnectionOpener(tlsSettings, TimeProvider.System);
            },
        };
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("ftp://127.0.0.1:0"), withOpener, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, _ => bound.SetResult()), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        Assert.AreEqual(1, openerCalls);
    }

    [TestMethod]
    public async Task ServeAsync_NullOutputs_ThrowsArgumentNullException()
    {
        await using var serving = await ComposeAsync(new StartTestSeams(), "http://127.0.0.1:0");

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => serving.ServeAsync(null!, TestContext.CancellationToken));
    }

    private async Task<ComposedServing> ComposeAsync(StartTestSeams seams, params string[] args)
    {
        var start = await SurlServerComposition.ComposeAsync(StartTestSeams.Parse(args), seams.Seams, TestContext.CancellationToken);
        return start.Serving ?? throw new AssertFailedException($"The start was refused: {string.Join(" ", start.Failure!.Lines)}");
    }
}
