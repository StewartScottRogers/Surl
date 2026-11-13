using System.Net.Sockets;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>nats</c> client against a live <c>surl nats://</c> and <c>natss://</c> (BL-529,
/// ADR-0083 decisions 1 to 3): subscribe and receive a publish, publish, request and reply, and a refused bad login.
/// Each expected value comes from the NATS client protocol as <c>Documentation/Conformance/Extended/Nats.md</c>
/// lists it - a PUB is delivered as MSG to every matching subscription, a reply-to is carried to the replier, a
/// refused login is <c>-ERR 'Authorization Violation'</c> - never from what the port printed beyond that. The
/// other side of each exchange is a raw NATS connection written here from the protocol page, on surl's plain
/// <c>nats</c> listener, which shares one bus with <c>natss</c>. Inconclusive when the pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientExchangesMessagesWithSurlOverNatsTests
{
    private const string Login = "alice:secret";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Nats:NATS-PUB-1, Nats:NATS-MSG-1, Nats:NATS-TLS-1: a message published on a subject reaches the client subscribed to it, payload exact")]
    [DataRow("nats")]
    [DataRow("natss")]
    public async Task Subscribe_ThenAMessageIsPublished_PrintsThePayload(string scheme)
    {
        await using var surl = await StartAsync();
        var subscriber = ClientAsync(surl, scheme, Login, "/news?count=1", "-m", "30");

        // The bus keeps nothing for a subscriber that is not there yet, so publish until the client has one.
        await using var publisher = await RawClient.ConnectAsync(PlainPort(surl), TestContext.CancellationToken);
        while (!subscriber.IsCompleted)
        {
            await publisher.PublishAsync("news", "hello nats", TestContext.CancellationToken);
            await Task.WhenAny(subscriber, Task.Delay(300, TestContext.CancellationToken));
        }

        var result = await subscriber;
        AssertOutput("hello nats", result, surl);
    }

    [TestMethod]
    [Description("Nats:NATS-PUB-1, Nats:NATS-SUB-1, Nats:NATS-TLS-2: data the client publishes is delivered as MSG to a subscriber, payload exact")]
    [DataRow("nats")]
    [DataRow("natss")]
    public async Task Publish_WithData_IsDeliveredToASubscriber(string scheme)
    {
        await using var surl = await StartAsync();
        await using var subscriber = await RawClient.ConnectAsync(PlainPort(surl), TestContext.CancellationToken);
        await subscriber.SubscribeAsync("events", TestContext.CancellationToken);

        var publish = await ClientAsync(surl, scheme, Login, "/events", "-m", "30", "-d", "sent by the port");

        AssertOutput(string.Empty, publish, surl);
        var message = await subscriber.ReadMessageAsync(TestContext.CancellationToken);
        Assert.AreEqual("events", message.Subject, surl.Log);
        Assert.AreEqual("sent by the port", message.Payload, surl.Log);
    }

    [TestMethod]
    [Description("Nats:NATS-PUB-1, Nats:NATS-MSG-1: a request is delivered with its reply-to, and the replier's PUB on it comes back to the client")]
    [DataRow("nats")]
    [DataRow("natss")]
    public async Task Request_ToAReplier_PrintsTheReply(string scheme)
    {
        await using var surl = await StartAsync();
        await using var replier = await RawClient.ConnectAsync(PlainPort(surl), TestContext.CancellationToken);
        await replier.SubscribeAsync("service", TestContext.CancellationToken);
        var replying = Task.Run(async () =>
        {
            var request = await replier.ReadMessageAsync(TestContext.CancellationToken);
            Assert.IsFalse(string.IsNullOrEmpty(request.ReplyTo), "the request carries a reply-to");
            await replier.PublishAsync(request.ReplyTo!, "re: " + request.Payload, TestContext.CancellationToken);
        }, TestContext.CancellationToken);

        var result = await ClientAsync(surl, scheme, Login, "/service?request", "-m", "30", "-d", "ping");

        await replying;
        AssertOutput("re: ping", result, surl);
    }

    [TestMethod]
    [Description("Nats:NATS-CONNECT-2: a wrong password is refused with Authorization Violation, which the client reports as a login failure (exit 67)")]
    public async Task Login_WithAWrongPassword_IsRefused()
    {
        await using var surl = await StartAsync();

        var result = await ClientAsync(surl, "nats", "alice:wrong", "/refused", "-m", "30", "-d", "never");

        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(67, result.ExitCode, diagnostics);
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }

    private Task<SurlOnLoopback> StartAsync() =>
        SurlOnLoopback.StartInMemoryAsync(
            ["nats", "natss"],
            ["--user", Login, "--allow-plaintext-auth", "--self-signed"],
            TestContext.CancellationToken);

    private static int PlainPort(SurlOnLoopback surl) => surl.BaseUrlOf("nats").Port;

    private Task<CurlPortClientRunResult> ClientAsync(SurlOnLoopback surl, string scheme, string login, string pathAndQuery, params string[] options) =>
        PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, ["-sS", "-k", .. options, $"{scheme}://{login}@127.0.0.1:{surl.BaseUrlOf(scheme).Port}{pathAndQuery}"]);

    private static void AssertOutput(string expected, CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }

    private readonly record struct NatsMessage(string Subject, string? ReplyTo, string Payload);

    /// <summary>A raw NATS client over plain text, written from the protocol page: CONNECT, SUB, PUB, MSG, PING and PONG.</summary>
    private sealed class RawClient : IAsyncDisposable
    {
        private readonly TcpClient tcp;
        private readonly StreamReader reader;
        private readonly StreamWriter writer;

        private RawClient(TcpClient tcp)
        {
            this.tcp = tcp;
            var stream = tcp.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
        }

        public static async Task<RawClient> ConnectAsync(int port, CancellationToken cancellationToken)
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port, cancellationToken);
            var client = new RawClient(tcp);
            _ = await client.reader.ReadLineAsync(cancellationToken); // INFO
            await client.writer.WriteLineAsync("CONNECT {\"user\":\"alice\",\"pass\":\"secret\",\"verbose\":false,\"pedantic\":false,\"protocol\":1}".AsMemory(), cancellationToken);
            await client.PingAsync(cancellationToken);
            return client;
        }

        public async Task SubscribeAsync(string subject, CancellationToken cancellationToken)
        {
            await writer.WriteLineAsync($"SUB {subject} 1".AsMemory(), cancellationToken);
            await PingAsync(cancellationToken);
        }

        public async Task PublishAsync(string subject, string payload, CancellationToken cancellationToken)
        {
            await writer.WriteAsync($"PUB {subject} {Encoding.UTF8.GetByteCount(payload)}\r\n{payload}\r\n".AsMemory(), cancellationToken);
            await PingAsync(cancellationToken);
        }

        public async Task<NatsMessage> ReadMessageAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken) ?? throw new EndOfStreamException("surl closed the NATS connection");
                var fields = line.Split(' ');
                if (fields[0] != "MSG")
                {
                    continue; // PING from the server, or a stray PONG
                }

                var size = int.Parse(fields[^1], System.Globalization.CultureInfo.InvariantCulture);
                var buffer = new char[size];
                await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
                _ = await reader.ReadLineAsync(cancellationToken);
                return new NatsMessage(fields[1], fields.Length == 5 ? fields[3] : null, new string(buffer));
            }
        }

        public ValueTask DisposeAsync()
        {
            reader.Dispose();
            writer.Dispose();
            tcp.Dispose();
            return ValueTask.CompletedTask;
        }

        // PING after an operation: the PONG shows the server has processed everything before it.
        private async Task PingAsync(CancellationToken cancellationToken)
        {
            await writer.WriteLineAsync("PING".AsMemory(), cancellationToken);
            string? line;
            do
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }
            while (line is not null && line != "PONG");
        }
    }
}
