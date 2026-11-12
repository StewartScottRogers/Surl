using Surl.Cli;
using Surl.Core;
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

    // A datagram listener that reports no bound port: the note names its listen URL's port, which the shared-port
    // pair set to the connection listener's bound port (BL-1056).
    [TestMethod]
    public async Task ServeAsync_Http3DatagramListenerReportingNoBoundPort_NamesItsListenUrlsPortInTheHttp3Note()
    {
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var seams = new StartTestSeams(new FakeListenerFactory { ReportsDatagramBoundPort = false });
        await using var serving = await ComposeAsync(seams, "-v", "--self-signed", "--http3", "https://127.0.0.1:0");

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        StringAssert.Contains(log.ToString(), $"* Serving HTTP/3 over QUIC on UDP port {FakeListenerFactory.BoundPort}" + Environment.NewLine);
    }

    // A dns listen URL binds UDP beside TCP on one port too (ADR-0092), so the shared-port note is asked
    // for, but it is no HTTP/3: verbose writes no HTTP/3 note for it (BL-1056).
    [TestMethod]
    public async Task ServeAsync_VerboseDnsOnASharedPort_WritesNoHttp3Note()
    {
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true };
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["-v", "dns://127.0.0.1:0"], ["dns"]).CommandLine!, seams, TestContext.CancellationToken);
        await using var serving = start.Serving ?? throw new AssertFailedException($"The start was refused: {string.Join(" ", start.Failure!.Lines)}");

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        var boundListenUrls = await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var outcome = await serve;

        Assert.AreEqual(SurlExitCode.Ok, outcome.ExitCode);
        Assert.HasCount(1, boundListenUrls);
        Assert.DoesNotContain("HTTP/3", log.ToString());
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

    [TestMethod]
    public async Task IdentifiersForExchange_TwoInstancesWithSeed42_DrawEqualBytesForExchange1()
    {
        await using var first = await ComposeAsync(new StartTestSeams(), "--random-seed", "42", "http://127.0.0.1:0");
        await using var second = await ComposeAsync(new StartTestSeams(), "--random-seed", "42", "http://127.0.0.1:0");

        var firstBytes = Draw(first.IdentifiersForExchange(1));
        var secondBytes = Draw(second.IdentifiersForExchange(1));

        CollectionAssert.AreEqual(firstBytes, secondBytes);
        CollectionAssert.AreEqual(Draw(new SeededIdentifierRandom(42).ForExchange(1)), firstBytes);
        Assert.AreNotSame(first.IdentifiersForExchange(1), first.IdentifiersForExchange(1));
    }

    [TestMethod]
    public async Task IdentifiersForExchange_Unseeded_IsTheCryptographicSource()
    {
        await using var serving = await ComposeAsync(new StartTestSeams(), "http://127.0.0.1:0");

        Assert.AreSame(CryptographicIdentifierRandom.Instance, serving.IdentifiersForExchange(1));
        Assert.AreSame(CryptographicIdentifierRandom.Instance, serving.IdentifiersForServer("nats"));
    }

    [TestMethod]
    public async Task IdentifiersForExchange_DifferentSeedsOrBothUnseeded_DrawDifferentBytes()
    {
        await using var seed42 = await ComposeAsync(new StartTestSeams(), "--random-seed", "42", "http://127.0.0.1:0");
        await using var seed43 = await ComposeAsync(new StartTestSeams(), "--random-seed", "43", "http://127.0.0.1:0");
        await using var unseededOne = await ComposeAsync(new StartTestSeams(), "http://127.0.0.1:0");
        await using var unseededTwo = await ComposeAsync(new StartTestSeams(), "http://127.0.0.1:0");

        CollectionAssert.AreNotEqual(Draw(seed42.IdentifiersForExchange(1)), Draw(seed43.IdentifiersForExchange(1)));
        CollectionAssert.AreNotEqual(Draw(unseededOne.IdentifiersForExchange(1)), Draw(unseededTwo.IdentifiersForExchange(1)));
    }

    [TestMethod]
    public async Task IdentifiersForServer_Seeded_IsDerivedFromTheSeedAndTheNameAlone()
    {
        await using var serving = await ComposeAsync(new StartTestSeams(), "--random-seed", "42", "http://127.0.0.1:0");

        CollectionAssert.AreEqual(Draw(new SeededIdentifierRandom(42).ForServer("nats")), Draw(serving.IdentifiersForServer("nats")));
        CollectionAssert.AreNotEqual(Draw(serving.IdentifiersForServer("nats")), Draw(serving.IdentifiersForServer("kafka")));
    }

    [TestMethod]
    public async Task IdentifiersForServer_EmptyName_Throws()
    {
        await using var serving = await ComposeAsync(new StartTestSeams(), "http://127.0.0.1:0");

        Assert.ThrowsExactly<ArgumentException>(() => serving.IdentifiersForServer(string.Empty));
    }

    private static byte[] Draw(IIdentifierRandom source)
    {
        var bytes = new byte[16];
        source.NextBytes(bytes);
        return bytes;
    }

    private async Task<ComposedServing> ComposeAsync(StartTestSeams seams, params string[] args)
    {
        var start = await SurlServerComposition.ComposeAsync(StartTestSeams.Parse(args), seams.Seams, TestContext.CancellationToken);
        return start.Serving ?? throw new AssertFailedException($"The start was refused: {string.Join(" ", start.Failure!.Lines)}");
    }
}
