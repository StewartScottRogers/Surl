using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// One <c>surl</c> serves one message bus over several protocols (BL-670, ADR-0100 decisions 3.2 and 5). The pinned
/// upstream curl publishes over <c>mqtt</c> and the pinned Curl port's <c>amqp</c>, <c>amqp1</c>, <c>stomp</c>,
/// <c>nats</c>, <c>kafka</c> and <c>mqtt5</c> clients consume the same bytes from the topic the name mapping gives
/// each protocol; and the port's <c>mqtt5</c> client publishes a message the pinned upstream curl's <c>mqtt</c>
/// subscribe receives. <c>amqp</c>, <c>amqp1</c>, <c>stomp</c> and <c>nats</c> deliver live only (ADR-0100 decision
/// 3.3), so a subscription exists before the publish: the port's client is already running for <c>amqp1</c>,
/// <c>stomp</c> and <c>nats</c>, and for <c>amqp</c> a raw AMQP 0-9-1 connection written here from the specification
/// binds a queue to <c>amq.topic</c> first, because the port's <c>amqp</c> client only runs <c>basic.get</c>.
/// <c>kafka</c> and <c>mqtt5</c> read after the publish: the log and the retained slot hold it. Every expected value
/// is the payload the publishing end was given, or the bytes upstream curl's subscribe prints for a topic and
/// payload (ADR-0014), never what the port printed (ADR-0083 decisions 1 to 3, ADR-0003). Inconclusive when a pinned
/// build is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientConsumesMessagesSharedAcrossSurlProtocolsTests
{
    private const string Login = "alice:secret";
    private const string Topic = "events";
    private const string Payload = "shared on the bus";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("BL-670, ADR-0100 3.2: a message upstream curl publishes over mqtt reaches the port's amqp client through a queue bound to amq.topic with the routing key of the topic")]
    public async Task MqttPublish_ThenAmqpGet_FromABoundQueue_ReturnsThePublishedPayload()
    {
        await using var surl = await StartAsync("amqp");
        await BindQueueToTopicAsync(surl.BaseUrlOf("amqp").Port, "inbox", Topic);

        await PublishOverMqttAsync(surl);
        var received = await RunPortAsync(surl, "-sS", "-m", "30", $"amqp://{Login}@127.0.0.1:{surl.BaseUrlOf("amqp").Port}/%2F/inbox");

        AssertPayload(received, surl);
    }

    [TestMethod]
    [Description("BL-670, ADR-0100 3.2: a message upstream curl publishes over mqtt reaches the port's amqp1 client attached to the /topic/ address, payload exact")]
    public async Task Amqp1Receive_ThenMqttPublish_ReturnsThePublishedPayload()
    {
        await using var surl = await StartAsync("amqp1");
        var receiver = RunPortAsync(surl, "-sS", "-m", "30", $"amqp1://{Login}@127.0.0.1:{surl.BaseUrlOf("amqp1").Port}/topic/{Topic}");

        await PublishUntilConsumedAsync(surl, receiver);

        AssertPayload(await receiver, surl);
    }

    [TestMethod]
    [Description("BL-670, ADR-0100 3.2: a message upstream curl publishes over mqtt reaches the port's stomp client subscribed to /topic/, payload exact")]
    public async Task StompSubscribe_ThenMqttPublish_ReturnsThePublishedPayload()
    {
        await using var surl = await StartAsync("stomp");
        var subscriber = RunPortAsync(surl, "-sS", "-m", "8", $"stomp://{Login}@127.0.0.1:{surl.BaseUrlOf("stomp").Port}/topic/{Topic}");

        await PublishUntilConsumedAsync(surl, subscriber);

        AssertPayload(await subscriber, surl);
    }

    [TestMethod]
    [Description("BL-670, ADR-0100 3.2: a message upstream curl publishes over mqtt reaches the port's nats client subscribed to the subject, payload exact")]
    public async Task NatsSubscribe_ThenMqttPublish_ReturnsThePublishedPayload()
    {
        await using var surl = await StartAsync("nats");
        var subscriber = RunPortAsync(surl, "-sS", "-m", "30", $"nats://{Login}@127.0.0.1:{surl.BaseUrlOf("nats").Port}/{Topic}?count=1");

        await PublishUntilConsumedAsync(surl, subscriber);

        AssertPayload(await subscriber, surl);
    }

    [TestMethod]
    [Description("BL-670, ADR-0100 3.3: a message upstream curl published over mqtt is in the Kafka topic's log, and the port's kafka client reads it from offset 0")]
    public async Task MqttPublish_ThenKafkaConsume_ReturnsThePublishedPayload()
    {
        await using var surl = await StartAsync("kafka");
        await PublishOverMqttAsync(surl);

        var consumed = await RunPortAsync(
            surl, "-sS", "-m", "30", $"kafka://{Login}@127.0.0.1:{surl.BaseUrlOf("kafka").Port}/{Topic}/0?offset=0");

        AssertPayload(consumed, surl);
    }

    [TestMethod]
    [Description("BL-670, ADR-0093 decision 0 and ADR-0100 3.9: a message upstream curl published over mqtt is the retained message the port's mqtt5 client subscribes to")]
    public async Task MqttPublish_ThenMqtt5Subscribe_ReturnsThePublishedPayload()
    {
        await using var surl = await StartAsync("mqtt5");
        await PublishOverMqttAsync(surl);

        // The subscribe never ends by itself, so its own time limit ends it after the retained message arrived.
        var received = await RunPortAsync(surl, "-sS", "-m", "5", $"mqtt5://{Login}@127.0.0.1:{surl.BaseUrlOf("mqtt5").Port}/{Topic}");

        AssertPayload(received, surl);
    }

    [TestMethod]
    [Description("BL-670, ADR-0014: a message the port's mqtt5 client publishes is what upstream curl's mqtt subscribe prints as topic length, topic and payload")]
    public async Task Mqtt5Publish_ThenMqttSubscribe_PrintsTheTopicAndPayload()
    {
        // The port's publish has RETAIN clear and upstream curl's subscribe reads only retained messages, so the
        // topic is configured to retain every publish (--topic <filter>;retain).
        await using var surl = await StartAsync("mqtt5", "--topic", $"{Topic};retain");

        var published = await RunPortAsync(
            surl, "-sS", "-m", "30", "-d", Payload, $"mqtt5://{Login}@127.0.0.1:{surl.BaseUrlOf("mqtt5").Port}/{Topic}");
        var subscribed = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-m", "20", "-u", Login, MqttUrl(surl));

        Assert.AreEqual(0, published.ExitCode, Diagnostics(published, surl));
        Assert.AreEqual(0, subscribed.ExitCode, $"{subscribed.StandardError}; surl: {surl.Log}");
        byte[] expected = [.. TopicLengthPrefix(Topic), .. Encoding.UTF8.GetBytes(Topic), .. Encoding.UTF8.GetBytes(Payload)];
        CollectionAssert.AreEqual(expected, subscribed.StandardOutput, $"got {Convert.ToHexString(subscribed.StandardOutput)}; surl: {surl.Log}");
    }

    private static byte[] TopicLengthPrefix(string topic)
    {
        var prefix = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(prefix, (ushort)Encoding.UTF8.GetByteCount(topic));
        return prefix;
    }

    private static string MqttUrl(SurlOnLoopback surl) => $"mqtt://127.0.0.1:{surl.BaseUrlOf("mqtt").Port}/{Topic}";

    private Task<SurlOnLoopback> StartAsync(string consumerScheme, params string[] extraOptions) =>
        SurlOnLoopback.StartInMemoryAsync(
            ["mqtt", consumerScheme],
            ["--user", Login, "--allow-plaintext-auth", .. extraOptions],
            TestContext.CancellationToken);

    private async Task PublishOverMqttAsync(SurlOnLoopback surl)
    {
        var published = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-m", "20", "-u", Login, "-d", Payload, MqttUrl(surl));

        Assert.AreEqual(0, published.ExitCode, $"{published.StandardError}; surl: {surl.Log}");
    }

    // The bus keeps nothing for a subscriber that is not there yet, so publish until the client has one.
    private async Task PublishUntilConsumedAsync(SurlOnLoopback surl, Task<CurlPortClientRunResult> consumer)
    {
        while (!consumer.IsCompleted)
        {
            await PublishOverMqttAsync(surl);
            _ = await Task.WhenAny(consumer, Task.Delay(500, TestContext.CancellationToken));
        }
    }

    private async Task<CurlPortClientRunResult> RunPortAsync(SurlOnLoopback surl, params string[] arguments)
    {
        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, arguments);

        TestContext.WriteLine($"surl: {surl.Log}");
        return result;
    }

    private static string Diagnostics(CurlPortClientRunResult result, SurlOnLoopback surl) =>
        $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";

    private static void AssertPayload(CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var output = Encoding.UTF8.GetString(result.StandardOutput);
        StringAssert.Contains(output, Payload, Diagnostics(result, surl));
    }

    /// <summary>
    /// Declares <paramref name="queue"/> and binds it to <c>amq.topic</c> with <paramref name="routingKey"/> over a
    /// raw AMQP 0-9-1 connection written from the specification (section 4.2 framing, connection class 10, channel
    /// class 20, queue class 50), then disconnects; the queue stays, as a declared non-exclusive queue does.
    /// </summary>
    private async Task BindQueueToTopicAsync(int port, string queue, string routingKey)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync("127.0.0.1", port, TestContext.CancellationToken);
        var stream = tcp.GetStream();
        await stream.WriteAsync("AMQP\0\0\t\u0001"u8.ToArray(), TestContext.CancellationToken);

        _ = await ReadMethodAsync(stream, 10, 10); // connection.start
        await SendMethodAsync(stream, 0, 10, 11, [.. Table(), .. ShortString("PLAIN"), .. LongString("\0alice\0secret"), .. ShortString("en_US")]);
        var tune = await ReadMethodAsync(stream, 10, 30);
        await SendMethodAsync(stream, 0, 10, 31, tune);
        await SendMethodAsync(stream, 0, 10, 40, [.. ShortString("/"), .. ShortString(string.Empty), 0]);
        _ = await ReadMethodAsync(stream, 10, 41);
        await SendMethodAsync(stream, 1, 20, 10, ShortString(string.Empty));
        _ = await ReadMethodAsync(stream, 20, 11);
        await SendMethodAsync(stream, 1, 50, 10, [0, 0, .. ShortString(queue), 0, .. Table()]);
        _ = await ReadMethodAsync(stream, 50, 11);
        await SendMethodAsync(stream, 1, 50, 20, [0, 0, .. ShortString(queue), .. ShortString("amq.topic"), .. ShortString(routingKey), 0, .. Table()]);
        _ = await ReadMethodAsync(stream, 50, 21);
    }

    private static byte[] Table() => [0, 0, 0, 0];

    private static byte[] ShortString(string text) => [(byte)Encoding.UTF8.GetByteCount(text), .. Encoding.UTF8.GetBytes(text)];

    private static byte[] LongString(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var size = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(size, (uint)bytes.Length);
        return [.. size, .. bytes];
    }

    private async Task SendMethodAsync(NetworkStream stream, ushort channel, ushort classId, ushort methodId, byte[] arguments)
    {
        var frame = new byte[7 + 4 + arguments.Length + 1];
        frame[0] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(1), channel);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(3), (uint)(4 + arguments.Length));
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(7), classId);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(9), methodId);
        arguments.CopyTo(frame, 11);
        frame[^1] = 0xCE;
        await stream.WriteAsync(frame, TestContext.CancellationToken);
    }

    // Reads frames until the method frame of the class and method wanted, and returns its arguments.
    private async Task<byte[]> ReadMethodAsync(NetworkStream stream, ushort classId, ushort methodId)
    {
        while (true)
        {
            var header = new byte[7];
            await stream.ReadExactlyAsync(header, TestContext.CancellationToken);
            var body = new byte[BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(3)) + 1];
            await stream.ReadExactlyAsync(body, TestContext.CancellationToken);
            if (header[0] != 1)
            {
                continue; // a heart-beat
            }

            var gotClass = BinaryPrimitives.ReadUInt16BigEndian(body);
            var gotMethod = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(2));
            Assert.IsFalse(gotClass == 10 && gotMethod == 50, "surl closed the connection while binding the queue");
            if (gotClass == classId && gotMethod == methodId)
            {
                return body[4..^1];
            }
        }
    }
}
