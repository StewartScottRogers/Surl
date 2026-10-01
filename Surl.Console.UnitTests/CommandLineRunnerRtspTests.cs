using System.Text;
using Surl.Cli;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> composes the RTSP server (BL-317): <c>rtsp</c>, with no
/// TLS scheme because curl has none (ADR-0026 decision 3), each request judged by the one
/// authentication policy as HTTP's are (ADR-0074 decision 7). No test here touches the disk or
/// the network.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerRtspTests
{
    private static readonly string NewLine = Environment.NewLine;

    // The request pinned upstream curl 8.21.0 sends for `curl rtsp://127.0.0.1:<port>/`
    // (Surl.Protocol.Rtsp.UnitTests/Fixtures/options-plain/request.bin).
    private static readonly byte[] CurlOptionsRequest = Encoding.ASCII.GetBytes(
        "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n\r\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_RtspListenUrl_StartsAListenerAnsweredByTheRtspServer()
    {
        var run = await ServeOneConnectionAsync(CurlOptionsRequest, "rtsp://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("rtsp", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.AreEqual($"Listening on rtsp://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        Assert.IsNull(run.TlsSettings);
        StringAssert.StartsWith(run.Written, "RTSP/1.0 200 OK\r\nCSeq: 1\r\n");
        StringAssert.Contains(
            run.Written,
            "Public: OPTIONS, DESCRIBE, ANNOUNCE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER, SET_PARAMETER, RECORD\r\n");
    }

    [TestMethod]
    public async Task RunAsync_RtspWithAnAccount_ChallengesTheRequestThroughTheAuthenticationPolicy()
    {
        var run = await ServeOneConnectionAsync(CurlOptionsRequest, "-s", "-u", "tester:secret", "rtsp://127.0.0.1:0/");

        StringAssert.StartsWith(run.Written, "RTSP/1.0 401 Unauthorized\r\nCSeq: 1\r\n");
        StringAssert.Contains(run.Written, "WWW-Authenticate: Digest realm=\"surl\"");
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
