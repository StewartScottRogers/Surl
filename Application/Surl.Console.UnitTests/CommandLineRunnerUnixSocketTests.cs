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
        var factory = new ListenerStartSignallingFactory(Program.CreateListenerFactory(null));
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
