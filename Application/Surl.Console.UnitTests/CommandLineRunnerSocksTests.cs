using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Composition;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> serves SOCKS4 for <c>socks4://</c> and SOCKS5 for <c>socks5://</c>
/// (ADR-0121 decision 5, BL-560): each relays one request to an origin the test starts on loopback. The
/// client's connection is a fake; the origin is a real loopback listener, which the default origin
/// connector reaches.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerSocksTests
{
    private static readonly string NewLine = Environment.NewLine;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_Socks4_RelaysARequestToTheOrigin()
    {
        using var origin = new LoopbackOrigin("pong");
        byte[] request = [.. new byte[] { 4, 1, (byte)(origin.Port >> 8), (byte)origin.Port, 127, 0, 0, 1, 0 }, .. "ping"u8.ToArray()];

        var run = await ServeAsync(request, false, "socks4://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("socks4", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.AreEqual($"Listening on socks4://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        CollectionAssert.AreEqual(new byte[] { 0, 0x5A, 0, 0, 0, 0, 0, 0 }, run.Written[..8]);
        Assert.AreEqual("pong", Encoding.ASCII.GetString(run.Written[8..]));
        Assert.AreEqual("ping", await origin.Received);
    }

    [TestMethod]
    public async Task RunAsync_Socks5_RelaysARequestToTheOrigin()
    {
        using var origin = new LoopbackOrigin("pong");
        byte[] request =
        [
            5, 1, 0,
            5, 1, 0, 1, 127, 0, 0, 1, (byte)(origin.Port >> 8), (byte)origin.Port,
            .. "ping"u8.ToArray(),
        ];

        var run = await ServeAsync(request, false, "socks5://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("socks5", "127.0.0.1", 0) }, run.Factory.StartedListenUrls);
        Assert.AreEqual($"Listening on socks5://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine, run.Output);
        CollectionAssert.AreEqual(new byte[] { 5, 0 }, run.Written[..2]);
        Assert.AreEqual(5, run.Written[2]);
        Assert.AreEqual(0, run.Written[3]);
        Assert.AreEqual("pong", Encoding.ASCII.GetString(run.Written[^4..]));
        Assert.AreEqual("ping", await origin.Received);
    }

    [TestMethod]
    public async Task RunAsync_ProxyOptionWithASocks5Url_ServesSocks5()
    {
        using var origin = new LoopbackOrigin("pong");
        byte[] request =
        [
            5, 1, 0,
            5, 1, 0, 1, 127, 0, 0, 1, (byte)(origin.Port >> 8), (byte)origin.Port,
            .. "ping"u8.ToArray(),
        ];

        var run = await ServeAsync(request, false, "-x", "socks5://127.0.0.1:0/");

        CollectionAssert.AreEqual(new[] { new ListenUrl("socks5", "127.0.0.1", 0) { IsProxy = true } }, run.Factory.StartedListenUrls);
        Assert.AreEqual("pong", Encoding.ASCII.GetString(run.Written[^4..]));
    }

    [TestMethod]
    public async Task RunAsync_Socks5ToAnOriginOutsideLoopback_RefusesWithNotAllowed()
    {
        byte[] request = [5, 1, 0, 5, 1, 0, 1, 10, 0, 0, 1, 0, 80];

        var run = await ServeAsync(request, true, "socks5://127.0.0.1:0/");

        CollectionAssert.AreEqual(new byte[] { 5, 0, 5, 2, 0, 1, 0, 0, 0, 0, 0, 0 }, run.Written);
    }

    // Serves one client connection that sends request, waits until it is answered, then stops.


    private async Task<Run> ServeAsync(byte[] request, bool waitsForClose, params string[] args)
    {
        var connection = new FakeConnection(request, stallsWhenExhausted: !waitsForClose);
        var run = new Run(new FakeListenerFactory { Connection = connection });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        CommandLineRunner runner = new(run.Create, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System, null);

        var running = runner.RunAsync(args, output, error, stop.Token);
        await run.Factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        if (waitsForClose)
        {
            await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        }
        else
        {
            while (!Encoding.ASCII.GetString(connection.WrittenBytes).EndsWith("pong", StringComparison.Ordinal))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), TestContext.CancellationToken);
            }
        }

        await stop.CancelAsync();
        var exitCode = await running;
        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        run.Output = output.ToString();
        run.Written = connection.WrittenBytes;
        return run;
    }

    private sealed class Run(FakeListenerFactory factory)
    {
        public FakeListenerFactory Factory { get; } = factory;

        public string Output { get; set; } = string.Empty;

        public byte[] Written { get; set; } = [];

        public IListenerFactory Create(ServerTlsSettings? tlsSettings) => Factory;
    }

    // An origin on loopback: accepts one connection, reads four bytes, answers with its fixed reply and
    // closes once the proxy closes its side.
    private sealed class LoopbackOrigin : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);

        public LoopbackOrigin(string reply)
        {
            listener.Start();
            Received = AnswerOneConnectionAsync(reply);
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        /// <summary>What the origin read before it answered.</summary>
        public Task<string> Received { get; }

        public void Dispose() => listener.Stop();

        private async Task<string> AnswerOneConnectionAsync(string reply)
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var buffer = new byte[4];
            await stream.ReadExactlyAsync(buffer);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(reply));
            await stream.FlushAsync();
            while (await stream.ReadAsync(buffer) > 0)
            {
            }

            return Encoding.ASCII.GetString(buffer);
        }
    }
}
