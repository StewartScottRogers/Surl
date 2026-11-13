using System.Net.Sockets;
using System.Text;
using Surl.Api;
using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

/// <summary>
/// Recipe: a test suite uses one <see cref="SurlServer"/> as the message broker behind the code it tests (BL-820).
/// The server runs <c>mqtt</c>, <c>nats</c> and <c>stomp</c> on port 0 over one message bus; the pinned upstream
/// curl publishes over MQTT and a test-local <see cref="TcpClient"/> consumes over NATS or STOMP, writing the text
/// frames those specifications define (upstream curl speaks neither, and the Curl port is never a recipe's client;
/// ADR-0083 decision 1). Topic <c>a/b</c> is NATS subject <c>a.b</c> and STOMP destination <c>/topic/a/b</c>
/// (ADR-0100 decision 3.2). Nothing sleeps or polls: the test waits on the bus's change event with
/// <see cref="SurlServer.WaitForEventAsync"/>. Inconclusive where no pinned build is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class StandInPublishAndConsumeRecipeTests
{
    private const string Payload = "hello bus";

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PublishOverMqtt_ThenConsumeOverNats_ReceivesTheExactPayload()
    {
        // Arrange: one server, three broker protocols, one bus; a NATS consumer subscribed to a.b. The PING
        // is answered only after the SUB before it was handled, so the subscription exists once PONG is read.
        await using var server = await StartAsync();
        using var consumer = await ConnectAsync(server, "nats");
        await consumer.SendAsync("CONNECT {}\r\nSUB a.b 1\r\nPING\r\n");
        await consumer.ReadUntilAsync("PONG\r\n");

        // Act: the code under test publishes over MQTT; the test waits for the bus's publish event instead of
        // polling, then reads what the consumer was sent.
        var published = await PublishOverMqttAsync(server);
        await server.WaitForEventAsync(BusChange(), WaitLimit, TestContext.CancellationToken);
        var received = await consumer.ReadUntilAsync($"\r\n{Payload}\r\n");

        // Assert: NATS delivered MSG <subject> <sid> <bytes> and the payload, byte for byte.
        Assert.AreEqual(0, published.ExitCode, published.StandardError);
        StringAssert.Contains(received, $"MSG a.b 1 {Payload.Length}\r\n{Payload}\r\n");
    }

    [TestMethod]
    public async Task PublishOverMqtt_ThenConsumeOverStomp_ReceivesTheExactPayload()
    {
        // Arrange: a STOMP 1.2 consumer subscribed to /topic/a/b; the RECEIPT frame proves the subscription exists.
        await using var server = await StartAsync();
        using var consumer = await ConnectAsync(server, "stomp");
        await consumer.SendAsync("CONNECT\naccept-version:1.2\nhost:localhost\n\n\0");
        await consumer.ReadUntilAsync("\0");
        await consumer.SendAsync("SUBSCRIBE\nid:0\ndestination:/topic/a/b\nreceipt:r1\n\n\0");
        await consumer.ReadUntilAsync("receipt-id:r1");

        // Act: the code under test publishes over MQTT; the test waits for the bus's publish event.
        var published = await PublishOverMqttAsync(server);
        await server.WaitForEventAsync(BusChange(), WaitLimit, TestContext.CancellationToken);
        var received = await consumer.ReadUntilAsync($"\n\n{Payload}\0");

        // Assert: a MESSAGE frame for the destination carries the payload.
        Assert.AreEqual(0, published.ExitCode, published.StandardError);
        StringAssert.Contains(received, "MESSAGE\n");
        StringAssert.Contains(received, "destination:/topic/a/b");
    }

    private static SurlEventQuery BusChange() =>
        new() { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Bus };

    private Task<UpstreamCurlRunResult> PublishOverMqttAsync(SurlServer server)
    {
        var mqtt = server.BoundUrls.Single(bound => bound.Scheme == "mqtt");
        return PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-d", Payload, $"mqtt://{mqtt.Host}:{mqtt.Port}/a/b");
    }

    private async Task<Consumer> ConnectAsync(SurlServer server, string scheme)
    {
        var url = server.BoundUrls.Single(bound => bound.Scheme == scheme);
        var client = new TcpClient();
        await client.ConnectAsync(url.Host, url.Port, TestContext.CancellationToken);
        return new Consumer(client, TestContext.CancellationToken);
    }

    private Task<SurlServer> StartAsync()
    {
        var options = new SurlServerOptions { RecordExchanges = true, AllowAnonymous = true, AreExtensionsTurnedOn = true };
        foreach (var scheme in new[] { "mqtt", "nats", "stomp" })
        {
            options.ListenUrls.Add($"{scheme}://127.0.0.1:0");
        }

        return SurlServer.StartAsync(options, TestContext.CancellationToken);
    }

    /// <summary>A test-local text-protocol client: writes frames and reads until an expected marker arrives.</summary>
    private sealed class Consumer(TcpClient client, CancellationToken testCancellation) : IDisposable
    {
        private readonly StringBuilder received = new();

        public async Task SendAsync(string text) =>
            await client.GetStream().WriteAsync(Encoding.UTF8.GetBytes(text), testCancellation);

        public async Task<string> ReadUntilAsync(string marker)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(testCancellation);
            limit.CancelAfter(WaitLimit);
            var buffer = new byte[4096];
            while (!received.ToString().Contains(marker, StringComparison.Ordinal))
            {
                var count = await client.GetStream().ReadAsync(buffer, limit.Token);
                Assert.AreNotEqual(0, count, $"the server closed the connection before sending {marker}");
                received.Append(Encoding.UTF8.GetString(buffer, 0, count));
            }

            return received.ToString();
        }

        public void Dispose() => client.Dispose();
    }
}
