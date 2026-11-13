using System.Net.Sockets;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>stomp</c> client against a live <c>surl stomp://</c> and <c>stomps://</c> (BL-534,
/// ADR-0083 decisions 1 to 3): send to a destination, subscribe and receive, and a refused bad login. Each expected
/// value comes from STOMP 1.2 as <c>Documentation/Conformance/Extended/Stomp.md</c> lists it - a SEND is delivered
/// as MESSAGE to every subscriber of the destination, a queue keeps a message until it is consumed, a refused login
/// is an ERROR frame - never from what the port printed beyond that. The other side of each exchange is a raw STOMP
/// connection written here from the specification, on surl's plain <c>stomp</c> listener, which shares one bus with
/// <c>stomps</c>. Inconclusive when the pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientExchangesMessagesWithSurlOverStompTests
{
    private const string Login = "alice:secret";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Stomp:STOMP12-Send-2, Stomp:Surl-Destination-1, Stomp:STOMP12-Message-1, Stomp:STOMP12-Disconnect-1, Stomp:Surl-Tls-1: a body the client sends to a topic reaches a subscriber as MESSAGE, body exact")]
    [DataRow("stomp")]
    [DataRow("stomps")]
    public async Task Send_ToATopic_IsDeliveredToASubscriber(string scheme)
    {
        await using var surl = await StartAsync();
        await using var subscriber = await RawClient.ConnectAsync(PlainPort(surl), TestContext.CancellationToken);
        await subscriber.SubscribeAsync("/topic/events", TestContext.CancellationToken);

        var send = await ClientAsync(surl, scheme, Login, "/topic/events", "-d", "sent by the port");

        AssertOutput(string.Empty, send, surl);
        var message = await subscriber.ReadMessageAsync(TestContext.CancellationToken);
        Assert.AreEqual("/topic/events", message.Destination, surl.Log);
        Assert.AreEqual("sent by the port", message.Body, surl.Log);
    }

    [TestMethod]
    [Description("Stomp:Surl-Destination-3, Stomp:STOMP12-Message-1, Stomp:STOMP12-Frames-4, Stomp:Surl-Tls-1: a queue keeps a message until a subscriber comes, and the client prints its body exactly")]
    [DataRow("stomp")]
    [DataRow("stomps")]
    public async Task Subscribe_ToAQueueHoldingAMessage_PrintsTheBody(string scheme)
    {
        await using var surl = await StartAsync();
        await using var sender = await RawClient.ConnectAsync(PlainPort(surl), TestContext.CancellationToken);
        await sender.SendAsync("/queue/jobs", "hello stomp", TestContext.CancellationToken);

        // The client reads until surl closes, which surl does not do, so its own time limit ends it.
        var result = await ClientAsync(surl, scheme, Login, "/queue/jobs", "-m", "5");

        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual("hello stomp", Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }

    [TestMethod]
    [Description("Stomp:Surl-Login-2: a wrong password is refused with an ERROR frame, which the client reports and fails on")]
    public async Task Login_WithAWrongPassword_IsRefused()
    {
        await using var surl = await StartAsync();

        var result = await ClientAsync(surl, "stomp", "alice:wrong", "/queue/refused", "-m", "30", "-d", "never");

        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreNotEqual(0, result.ExitCode, diagnostics);
        StringAssert.Contains(result.StandardError, "Authentication failed", diagnostics);
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }

    private Task<SurlOnLoopback> StartAsync() =>
        SurlOnLoopback.StartInMemoryAsync(
            ["stomp", "stomps"],
            ["--user", Login, "--allow-plaintext-auth", "--self-signed"],
            TestContext.CancellationToken);

    private static int PlainPort(SurlOnLoopback surl) => surl.BaseUrlOf("stomp").Port;

    private Task<CurlPortClientRunResult> ClientAsync(SurlOnLoopback surl, string scheme, string login, string path, params string[] options) =>
        PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, ["-sS", "-k", .. options, $"{scheme}://{login}@127.0.0.1:{surl.BaseUrlOf(scheme).Port}{path}"]);

    private static void AssertOutput(string expected, CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }

    private readonly record struct StompMessage(string Destination, string Body);

    /// <summary>A raw STOMP 1.2 client over plain text, written from the specification: CONNECT, SUBSCRIBE, SEND and MESSAGE.</summary>
    private sealed class RawClient : IAsyncDisposable
    {
        private readonly TcpClient tcp;
        private readonly StreamReader reader;
        private readonly StreamWriter writer;
        private int receipts;

        private RawClient(TcpClient tcp)
        {
            this.tcp = tcp;
            var stream = tcp.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        }

        public static async Task<RawClient> ConnectAsync(int port, CancellationToken cancellationToken)
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port, cancellationToken);
            var client = new RawClient(tcp);
            await client.writer.WriteAsync("CONNECT\naccept-version:1.2\nhost:localhost\nlogin:alice\npasscode:secret\n\n\0".AsMemory(), cancellationToken);
            var connected = await client.ReadFrameAsync(cancellationToken);
            Assert.StartsWith("CONNECTED\n", connected, "surl answers the raw login with CONNECTED");
            return client;
        }

        public async Task SubscribeAsync(string destination, CancellationToken cancellationToken)
        {
            await writer.WriteAsync($"SUBSCRIBE\nid:0\ndestination:{destination}\nack:auto\n{NextReceipt()}\n\0".AsMemory(), cancellationToken);
            await ReadReceiptAsync(cancellationToken);
        }

        public async Task SendAsync(string destination, string body, CancellationToken cancellationToken)
        {
            var length = Encoding.UTF8.GetByteCount(body);
            await writer.WriteAsync($"SEND\ndestination:{destination}\ncontent-length:{length}\n{NextReceipt()}\n{body}\0".AsMemory(), cancellationToken);
            await ReadReceiptAsync(cancellationToken);
        }

        public async Task<StompMessage> ReadMessageAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var frame = await ReadFrameAsync(cancellationToken);
                if (!frame.StartsWith("MESSAGE\n", StringComparison.Ordinal))
                {
                    continue; // a RECEIPT or heart-beat
                }

                var split = frame.IndexOf("\n\n", StringComparison.Ordinal);
                var headers = frame[..split].Split('\n').Skip(1).Select(line => line.Split(':', 2)).ToDictionary(pair => pair[0], pair => pair[1]);
                return new StompMessage(headers["destination"], frame[(split + 2)..]);
            }
        }

        public ValueTask DisposeAsync()
        {
            reader.Dispose();
            writer.Dispose();
            tcp.Dispose();
            return ValueTask.CompletedTask;
        }

        private string NextReceipt() => $"receipt:r{++receipts}\n";

        // A RECEIPT shows the server took everything before it.
        private async Task ReadReceiptAsync(CancellationToken cancellationToken)
        {
            var frame = await ReadFrameAsync(cancellationToken);
            Assert.StartsWith("RECEIPT\n", frame, $"surl answered with {frame}");
        }

        private async Task<string> ReadFrameAsync(CancellationToken cancellationToken)
        {
            var frame = new StringBuilder();
            var one = new char[1];
            while (true)
            {
                if (await reader.ReadAsync(one.AsMemory(), cancellationToken) == 0)
                {
                    throw new EndOfStreamException("surl closed the STOMP connection");
                }

                if (one[0] == '\0')
                {
                    return frame.ToString().TrimStart('\n', '\r');
                }

                _ = frame.Append(one[0]);
            }
        }
    }
}
