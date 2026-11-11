using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Composition;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> serves the HTTP proxy for <c>-x http://...</c> and the HTTPS proxy for
/// <c>-x https://...</c> (ADR-0088 decision 7, BL-1004): each forwards one request and tunnels one
/// <c>CONNECT</c> to an origin the test starts on loopback. The client's connection is a fake; the origin is a
/// real loopback listener, which the default origin connector reaches.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerProxyTests
{
    private static readonly string NewLine = Environment.NewLine;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_ProxyHttp_ForwardsARequestToTheOrigin()
    {
        using var origin = new LoopbackOrigin("HTTP/1.1 200 OK\r\nContent-Length: 6\r\nConnection: close\r\n\r\norigin");
        var request = $"GET http://127.0.0.1:{origin.Port}/hello HTTP/1.1\r\nHost: 127.0.0.1:{origin.Port}\r\n\r\n";

        var run = await ServeAsync(request, stalls: false, "origin", "-x", "http://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("http", "127.0.0.1", 0) { IsProxy = true } }, run.Factory.StartedListenUrls);
        Assert.AreEqual($"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        StringAssert.StartsWith(run.Written, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(run.Written, "origin");
        StringAssert.StartsWith(await origin.ReceivedHead, "GET /hello HTTP/1.1\r\n");
    }

    [TestMethod]
    public async Task RunAsync_ProxyHttp_TunnelsAConnectToTheOrigin()
    {
        using var origin = new LoopbackOrigin("pong");
        var request = $"CONNECT 127.0.0.1:{origin.Port} HTTP/1.1\r\nHost: 127.0.0.1:{origin.Port}\r\n\r\nping";

        var run = await ServeAsync(request, stalls: true, "pong", "-x", "http://127.0.0.1:0/");

        StringAssert.StartsWith(run.Written, "HTTP/1.1 200 ");
        StringAssert.EndsWith(run.Written, "pong");
        Assert.AreEqual("ping", await origin.ReceivedHead);
    }

    [TestMethod]
    public async Task RunAsync_ProxyHttps_ForwardsARequestToTheOrigin()
    {
        using var origin = new LoopbackOrigin("HTTP/1.1 200 OK\r\nContent-Length: 6\r\nConnection: close\r\n\r\norigin");
        var request = $"GET http://127.0.0.1:{origin.Port}/hello HTTP/1.1\r\nHost: 127.0.0.1:{origin.Port}\r\n\r\n";

        var run = await ServeAsync(request, stalls: false, "origin", "--proxy", "https://127.0.0.1:0/", "--self-signed");

        CollectionAssert.AreEqual(new[] { new ListenUrl("https", "127.0.0.1", 0) { IsProxy = true } }, run.Factory.StartedListenUrls);
        Assert.IsNotNull(run.TlsSettings);
        Assert.AreEqual($"Listening on https://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        StringAssert.StartsWith(run.Written, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(run.Written, "origin");
    }

    [TestMethod]
    public async Task RunAsync_ProxyHttps_TunnelsAConnectToTheOrigin()
    {
        using var origin = new LoopbackOrigin("pong");
        var request = $"CONNECT 127.0.0.1:{origin.Port} HTTP/1.1\r\nHost: 127.0.0.1:{origin.Port}\r\n\r\nping";

        var run = await ServeAsync(request, stalls: true, "pong", "--proxy", "https://127.0.0.1:0/", "--self-signed");

        StringAssert.StartsWith(run.Written, "HTTP/1.1 200 ");
        StringAssert.EndsWith(run.Written, "pong");
        Assert.AreEqual("ping", await origin.ReceivedHead);
    }

    [TestMethod]
    public async Task RunAsync_ProxyAccountGiven_AnswersARequestWithoutLogin407()
    {
        var request = "GET http://127.0.0.1:1/ HTTP/1.1\r\nHost: 127.0.0.1:1\r\n\r\n";

        var run = await ServeAsync(request, stalls: false, "407", "-x", "http://127.0.0.1:0/", "-U", "alice:secret", "--allow-plaintext-auth");

        StringAssert.StartsWith(run.Written, "HTTP/1.1 407 ");
        StringAssert.Contains(run.Written, "Proxy-Authenticate: ");
    }

    [TestMethod]
    public async Task RunAsync_ProxyHttpsWithoutCertOrSelfSigned_ReturnsCertificateProblem()
    {
        var run = new Run(new FakeListenerFactory());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CreateRunner(run).RunAsync(["-x", "https://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.CertificateProblem, exitCode);
        StringAssert.StartsWith(error.ToString(), "surl: (58) https://127.0.0.1:0/ needs a certificate");
        Assert.IsEmpty(run.Factory.StartedListenUrls);
    }

    // Serves one client connection that sends request, waits until it was answered with something ending
    // in answerEnd (or, without stalling, until the connection ended), then stops.
    private async Task<Run> ServeAsync(string request, bool stalls, string answerEnd, params string[] args)
    {
        var connection = new FakeConnection(Encoding.ASCII.GetBytes(request), stalls);
        var run = new Run(new FakeListenerFactory { Connection = connection });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(run).RunAsync(args, output, error, stop.Token);
        await run.Factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await WaitUntilAnsweredAsync(connection, answerEnd, stalls);
        await stop.CancelAsync();
        var exitCode = await running;
        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        run.Output = output.ToString();
        run.Written = Encoding.ASCII.GetString(connection.WrittenBytes);
        return run;
    }

    private async Task WaitUntilAnsweredAsync(FakeConnection connection, string answerEnd, bool stalls)
    {
        if (!stalls)
        {
            await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
            return;
        }

        while (!Encoding.ASCII.GetString(connection.WrittenBytes).EndsWith(answerEnd, StringComparison.Ordinal))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), TestContext.CancellationToken);
        }
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

    // An origin on loopback: accepts one connection, reads until a blank line (a request head) or, for a
    // tunnel, whatever arrives first, answers with its fixed reply and closes.
    private sealed class LoopbackOrigin : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);

        public LoopbackOrigin(string reply)
        {
            listener.Start();
            ReceivedHead = AnswerOneConnectionAsync(reply);
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        /// <summary>What the origin read before it answered.</summary>
        public Task<string> ReceivedHead { get; }

        public void Dispose() => listener.Stop();

        private async Task<string> AnswerOneConnectionAsync(string reply)
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var received = new StringBuilder();
            var buffer = new byte[1024];
            while (!IsComplete(received.ToString()))
            {
                var count = await stream.ReadAsync(buffer);
                if (count == 0)
                {
                    break;
                }

                received.Append(Encoding.ASCII.GetString(buffer, 0, count));
            }

            await stream.WriteAsync(Encoding.ASCII.GetBytes(reply));
            await stream.FlushAsync();
            // Stay open until the proxy closes its side, so a tunnel's reply is relayed before the end.
            while (await stream.ReadAsync(buffer) > 0)
            {
            }

            return received.ToString();
        }

        private static bool IsComplete(string received) =>
            received.EndsWith("\r\n\r\n", StringComparison.Ordinal) || received == "ping";
    }
}
