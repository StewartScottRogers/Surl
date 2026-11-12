using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Composition;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// <c>--unix-socket</c> served end to end (ADR-0094, decisions 1 and 6): <c>surl</c>'s own listener
/// factory binds a socket file in a temporary directory, and a client <see cref="Socket"/> in the
/// test sends the request bytes upstream curl 8.21.0 sends over one (ADR-0094's measurement). No
/// network is involved, so the test is not an integration test, as <c>Surl.Networking</c>'s own
/// Unix socket tests are not.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerUnixSocketTests
{
    // What curl --unix-socket <path> http://localhost/ sends: TCP's request bytes (ADR-0094).
    private static readonly byte[] CurlRequest =
        "GET / HTTP/1.1\r\nHost: localhost\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_UnixSocket_ServesOneHttpRequestOverTheSocketFileAndRemovesIt()
    {
        // Short enough for macOS's 103-byte sun_path under its temporary directory.
        var socketPath = Path.Join(Path.GetTempPath(), "surl-" + Guid.NewGuid().ToString("N")[..8] + ".sock");
        var factory = new ListenerStartSignallingFactory(Program.CreateListenerFactory(null, TimeProvider.System));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var running = new CommandLineRunner(
                _ => factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System,
                environmentVariables: FakeEnvironmentVariableReader.Empty)
            .RunAsync(["--unix-socket", socketPath, "http://localhost/"], output, error, stop.Token);
        await factory.ConnectionListenerStarted.Task.WaitAsync(TestContext.CancellationToken);

        var responseHead = await ExchangeAsync(socketPath, TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        StringAssert.StartsWith(responseHead, "HTTP/1.1 ");
        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        StringAssert.Contains(output.ToString(), $"Listening on http://localhost:80/ over unix://{socketPath}");
        Assert.IsFalse(File.Exists(socketPath), "the socket file is removed when surl stops");
    }

    [TestMethod]
    public async Task RunAsync_UnixSocketWithSocks5h_RelaysOneRequestToTheOriginOverTheSocketFile()
    {
        // Short enough for macOS's 103-byte sun_path under its temporary directory.
        var socketPath = Path.Join(Path.GetTempPath(), "surl-" + Guid.NewGuid().ToString("N")[..8] + ".sock");
        using var origin = new TcpListener(IPAddress.Loopback, 0);
        origin.Start();
        var originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
        var originAnswered = AnswerOneConnectionAsync(origin, TestContext.CancellationToken);
        var factory = new ListenerStartSignallingFactory(Program.CreateListenerFactory(null, TimeProvider.System));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var running = new CommandLineRunner(
                _ => factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System,
                environmentVariables: FakeEnvironmentVariableReader.Empty)
            .RunAsync(["--unix-socket", socketPath, "socks5h://localhost"], output, error, stop.Token);
        await factory.ConnectionListenerStarted.Task.WaitAsync(TestContext.CancellationToken);

        var relayed = await Socks5ExchangeAsync(socketPath, originPort, TestContext.CancellationToken);
        var originReceived = await originAnswered;
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        CollectionAssert.AreEqual(new byte[] { 5, 0 }, relayed.Greeting);
        Assert.AreEqual(0, relayed.ReplyCode);
        Assert.AreEqual("pong", relayed.Body);
        Assert.AreEqual("ping", originReceived);
        StringAssert.Contains(output.ToString(), $"over unix://{socketPath}");
        Assert.IsFalse(File.Exists(socketPath), "the socket file is removed when surl stops");
    }

    // Greets with no authentication, asks for a CONNECT to 127.0.0.1:originPort as curl's socks5h does for an
    // address, sends "ping" and reads the greeting answer, the reply code and the origin's four-byte answer.
    private static async Task<(byte[] Greeting, byte ReplyCode, string Body)> Socks5ExchangeAsync(
        string socketPath, int originPort, CancellationToken cancellationToken)
    {
        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);
        await using var stream = new NetworkStream(client);
        await stream.WriteAsync(new byte[] { 5, 1, 0 }, cancellationToken);
        var greeting = new byte[2];
        await stream.ReadExactlyAsync(greeting, cancellationToken);
        await stream.WriteAsync(new byte[] { 5, 1, 0, 1, 127, 0, 0, 1, (byte)(originPort >> 8), (byte)originPort }, cancellationToken);
        var reply = new byte[10];
        await stream.ReadExactlyAsync(reply, cancellationToken);
        await stream.WriteAsync("ping"u8.ToArray(), cancellationToken);
        var body = new byte[4];
        await stream.ReadExactlyAsync(body, cancellationToken);
        return (greeting, reply[1], Encoding.ASCII.GetString(body));
    }

    // Accepts one connection on the origin, reads four bytes, answers "pong" and returns what it read.
    private static async Task<string> AnswerOneConnectionAsync(TcpListener origin, CancellationToken cancellationToken)
    {
        using var accepted = await origin.AcceptTcpClientAsync(cancellationToken);
        var stream = accepted.GetStream();
        var received = new byte[4];
        await stream.ReadExactlyAsync(received, cancellationToken);
        await stream.WriteAsync("pong"u8.ToArray(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
        return Encoding.ASCII.GetString(received);
    }

    // Sends curl's request over the socket file and reads the response up to the end of its head.
    private static async Task<string> ExchangeAsync(string socketPath, CancellationToken cancellationToken)
    {
        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);
        await client.SendAsync(CurlRequest, SocketFlags.None, cancellationToken);

        var received = new StringBuilder();
        var buffer = new byte[4096];
        while (!received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var count = await client.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
            if (count == 0)
            {
                break;
            }

            received.Append(Encoding.ASCII.GetString(buffer, 0, count));
        }

        return received.ToString();
    }

    // surl's own listener factory, which also says when the connection listener has started.
    private sealed class ListenerStartSignallingFactory(IListenerFactory inner) : IListenerFactory
    {
        public TaskCompletionSource ConnectionListenerStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
        {
            var listener = await inner.StartConnectionListenerAsync(listenUrl, cancellationToken);
            ConnectionListenerStarted.TrySetResult();
            return listener;
        }

        public ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
            inner.StartDatagramListenerAsync(listenUrl, cancellationToken);

        public ValueTask<SharedPortListeners> StartSharedPortListenersAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
            inner.StartSharedPortListenersAsync(listenUrl, cancellationToken);
    }
}
