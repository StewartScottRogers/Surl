using System.Text;
using Surl.Cli;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> composes the WebSocket server (BL-303): <c>ws</c> and,
/// through <see cref="ImplicitTlsSchemeServer"/>, <c>wss</c>, which needs <c>--cert</c> or
/// <c>--self-signed</c> (ADR-0071 decision 8), serving the one content store, and echoing with
/// <c>--ws-echo</c> (decision 4). No test here touches the disk or the network.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerWsTests
{
    private static readonly string NewLine = Environment.NewLine;

    // RFC 6455 section 1.3's sample key, and the accept value it gives.
    private static readonly byte[] UpgradeRequestForTheRoot = Encoding.ASCII.GetBytes(
        "GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
        + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_WsListenUrl_StartsAListenerAnsweredByTheWebSocketServer()
    {
        var run = await ServeOneConnectionAsync(UpgradeRequestForTheRoot, "ws://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("ws", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.AreEqual($"Listening on ws://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        Assert.IsNull(run.TlsSettings);
        StringAssert.StartsWith(run.Written, "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    public async Task RunAsync_WsEcho_UpgradesAPathTheContentStoreDoesNotHold()
    {
        var run = await ServeOneConnectionAsync(UpgradeRequestForTheRoot, "-s", "--ws-echo", "ws://127.0.0.1:0/");

        StringAssert.StartsWith(run.Written, "HTTP/1.1 101 Switching Protocols\r\n");
        StringAssert.Contains(run.Written, "Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n");
    }

    [TestMethod]
    public async Task RunAsync_WssListenUrlWithSelfSigned_StartsASecuredListenerAnsweredByTheWebSocketServer()
    {
        var run = await ServeOneConnectionAsync(UpgradeRequestForTheRoot, "-s", "--self-signed", "wss://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("wss", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.IsNotNull(run.TlsSettings);
        StringAssert.StartsWith(run.Written, "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    public async Task RunAsync_WssWithoutCertOrSelfSigned_WritesNeedsACertificateAndReturnsCertificateProblem()
    {
        var run = new Run(new FakeListenerFactory());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CreateRunner(run).RunAsync(["wss://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        Assert.AreEqual(
            "surl: (58) wss://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one" + NewLine,
            error.ToString());
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    // Serves until the connection has ended, then stops.
    private async Task<Run> ServeOneConnectionAsync(byte[] request, params string[] args)
    {
        var connection = new FakeConnection(request);
        var run = new Run(new FakeListenerFactory { Connection = connection });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(run).RunAsync(args, output, error, stop.Token);
        await run.Factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;
        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        run.Output = output.ToString();
        run.Written = Encoding.ASCII.GetString(connection.WrittenBytes);
        return run;
    }

    private static CommandLineRunner CreateRunner(Run run) =>
        new(run.Create, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System, null);

    private sealed class Run(FakeListenerFactory factory)
    {
        public FakeListenerFactory Factory { get; } = factory;

        public ServerTlsSettings? TlsSettings { get; private set; }

        public string Output { get; set; } = string.Empty;

        public string Written { get; set; } = string.Empty;

        public IListenerFactory Create(ServerTlsSettings? tlsSettings)
        {
            TlsSettings = tlsSettings;
            return Factory;
        }
    }
}
