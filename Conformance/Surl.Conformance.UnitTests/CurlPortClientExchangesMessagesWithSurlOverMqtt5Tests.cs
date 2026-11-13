using System.Net.Sockets;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>mqtt5</c> client against a live <c>surl mqtt5://</c>, <c>mqtt5s://</c> and
/// <c>mqtt5+ws://</c> (BL-502, ADR-0083 decisions 1 to 3): a publish, a subscribe that receives a retained message,
/// and a refused bad password. Each expected value comes from OASIS MQTT Version 5.0 as
/// <c>Documentation/Conformance/Extended/Mqtt5.md</c> lists it - a PUBLISH is delivered to a subscriber, a QoS 1
/// PUBLISH is answered PUBACK and a QoS 2 one PUBREC, PUBREL and PUBCOMP, a retained message is sent to a new
/// subscription, a refused login is a CONNACK with a reason code of <c>0x80</c> or above - never from what the port
/// printed beyond the payloads. The port publishes and subscribes at QoS 0 only (it grants itself nothing higher), so
/// the QoS 1 and QoS 2 flows are driven by a raw MQTT 5 connection written here from the specification, whose
/// retained message the port then reads. The raw connection is on surl's plain <c>mqtt5</c> listener, which shares one
/// bus with <c>mqtt5s</c> and <c>mqtt5+ws</c>. Inconclusive when the pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientExchangesMessagesWithSurlOverMqtt5Tests
{
    private const string Login = "alice:secret";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Mqtt5:MQTT-4.2.0-1, Mqtt5:MQTT-6.0.0-4, Mqtt5:MQTT-3.1.4-5: a payload the client publishes is delivered to a subscriber, payload exact")]
    [DataRow("mqtt5")]
    [DataRow("mqtt5s")]
    [DataRow("mqtt5+ws")]
    public async Task Publish_ToATopic_IsDeliveredToASubscriber(string scheme)
    {
        await using var surl = await StartAsync();
        await using var subscriber = await RawClient.ConnectAsync(PlainPort(surl), TestContext.CancellationToken);
        await subscriber.SubscribeAsync("events/one", TestContext.CancellationToken);

        var publish = await ClientAsync(surl, scheme, Login, "/events/one", "-d", "sent by the port");

        AssertOutput(string.Empty, publish, surl);
        var message = await subscriber.ReadPublishAsync(TestContext.CancellationToken);
        Assert.AreEqual("events/one", message.Topic, surl.Log);
        Assert.AreEqual("sent by the port", message.Payload, surl.Log);
    }

    [TestMethod]
    [Description("Mqtt5:MQTT-4.3.2-4, Mqtt5:MQTT-2.2.1-5, Mqtt5:MQTT-3.3.1-5, Mqtt5:MQTT-3.3.1-9, Mqtt5:MQTT-4.2.0-1: a QoS 1 retained PUBLISH is answered PUBACK with its Packet Identifier, and a new subscription receives it")]
    [DataRow("mqtt5")]
    [DataRow("mqtt5s")]
    [DataRow("mqtt5+ws")]
    public async Task Subscribe_ToARetainedQosOneMessage_PrintsThePayloadThenSurlDisconnects(string scheme)
    {
        await using var surl = await StartAsync("--mqtt5-disconnect-after-retained");
        await using var publisher = await RawClient.ConnectAsync(PlainPort(surl), TestContext.CancellationToken);
        await publisher.PublishQosOneAsync("state/door", "open", retain: true, TestContext.CancellationToken);

        var subscribe = await ClientAsync(surl, scheme, Login, "/state/door");

        AssertOutput("open", subscribe, surl);
    }

    [TestMethod]
    [Description("Mqtt5:MQTT-4.3.3-8, Mqtt5:MQTT-4.3.3-11, Mqtt5:MQTT-3.3.1-5, Mqtt5:MQTT-3.3.1-9, Mqtt5:MQTT-4.2.0-1: a QoS 2 retained PUBLISH is answered PUBREC, its PUBREL is answered PUBCOMP, and a new subscription receives it")]
    [DataRow("mqtt5")]
    [DataRow("mqtt5s")]
    [DataRow("mqtt5+ws")]
    public async Task Subscribe_ToARetainedQosTwoMessage_PrintsThePayloadAfterTheRoundTrip(string scheme)
    {
        await using var surl = await StartAsync("--mqtt5-disconnect-after-retained");
        await using var publisher = await RawClient.ConnectAsync(PlainPort(surl), TestContext.CancellationToken);
        await publisher.PublishQosTwoAsync("state/lamp", "on", retain: true, TestContext.CancellationToken);

        var subscribe = await ClientAsync(surl, scheme, Login, "/state/lamp");

        AssertOutput("on", subscribe, surl);
    }

    [TestMethod]
    [Description("Mqtt5:MQTT-4.12.0-4, Mqtt5:MQTT-3.2.2-6, Mqtt5:MQTT-4.13.2-1: a wrong password is refused with CONNACK 0x86 Bad User Name or Password, which the client reports as a failed transfer")]
    [DataRow("mqtt5")]
    [DataRow("mqtt5s")]
    [DataRow("mqtt5+ws")]
    public async Task Login_WithAWrongPassword_IsRefused(string scheme)
    {
        await using var surl = await StartAsync();

        var result = await ClientAsync(surl, scheme, "alice:wrong", "/never", "-d", "never");

        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        // MQTT 5 section 3.2.2.2: 0x86 Bad User Name or Password. The pinned port exits 8 for it where its own newer
        // ADR maps it to 67; the exit code is the port's, so only the reason code surl sent is asserted.
        Assert.AreNotEqual(0, result.ExitCode, diagnostics);
        StringAssert.Contains(result.StandardError, "CONNACK reason code 0x86", diagnostics);
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }

    private Task<SurlOnLoopback> StartAsync(params string[] extraOptions) =>
        SurlOnLoopback.StartInMemoryAsync(
            ["mqtt5", "mqtt5s", "mqtt5+ws"],
            ["--user", Login, "--allow-plaintext-auth", "--self-signed", .. extraOptions],
            TestContext.CancellationToken);

    private static int PlainPort(SurlOnLoopback surl) => surl.BaseUrlOf("mqtt5").Port;

    private Task<CurlPortClientRunResult> ClientAsync(SurlOnLoopback surl, string scheme, string login, string path, params string[] options) =>
        PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, ["-sS", "-k", "-m", "30", .. options, $"{scheme}://{login}@127.0.0.1:{surl.BaseUrlOf(scheme).Port}{path}"]);

    private static void AssertOutput(string expected, CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }

    private readonly record struct ReceivedPublish(string Topic, string Payload);

    private readonly record struct Packet(int Type, byte[] Body);

    /// <summary>A raw MQTT 5.0 client over plain TCP, written from the specification: CONNECT, SUBSCRIBE, PUBLISH at QoS 1 and 2.</summary>
    private sealed class RawClient : IAsyncDisposable
    {
        private const int ConnectType = 1;
        private const int ConnackType = 2;
        private const int PublishType = 3;
        private const int PubackType = 4;
        private const int PubrecType = 5;
        private const int PubrelType = 6;
        private const int PubcompType = 7;
        private const int SubscribeType = 8;
        private const int SubackType = 9;

        private readonly TcpClient tcp;
        private readonly NetworkStream stream;

        private RawClient(TcpClient tcp)
        {
            this.tcp = tcp;
            stream = tcp.GetStream();
        }

        public static async Task<RawClient> ConnectAsync(int port, CancellationToken cancellationToken)
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port, cancellationToken);
            var client = new RawClient(tcp);

            // Protocol name, level 5, flags: User Name, Password and Clean Start, Keep Alive 60 s, no properties.
            var body = new List<byte>();
            body.AddRange(Utf8String("MQTT"));
            body.AddRange([5, 0xC2, 0, 60, 0]);
            body.AddRange(Utf8String("raw-" + Guid.NewGuid().ToString("N")[..12]));
            body.AddRange(Utf8String("alice"));
            body.AddRange(Utf8String("secret"));
            await client.SendAsync(ConnectType << 4, body.ToArray(), cancellationToken);

            var connack = await client.ReadPacketAsync(cancellationToken);
            Assert.AreEqual(ConnackType, connack.Type, "surl answers the raw login with CONNACK");
            Assert.AreEqual(0, connack.Body[1], "surl accepts the raw login (reason code 0x00)");
            return client;
        }

        public async Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken)
        {
            var body = new List<byte>([0, 1, 0]);
            body.AddRange(Utf8String(topicFilter));
            body.Add(0); // Maximum QoS 0, Retain Handling 0.
            await SendAsync((SubscribeType << 4) | 0b0010, body.ToArray(), cancellationToken);

            var suback = await ReadPacketAsync(cancellationToken);
            Assert.AreEqual(SubackType, suback.Type, "surl answers SUBSCRIBE with SUBACK");
        }

        public async Task PublishQosOneAsync(string topic, string payload, bool retain, CancellationToken cancellationToken)
        {
            await SendAsync((PublishType << 4) | (1 << 1) | (retain ? 1 : 0), PublishBody(topic, packetIdentifier: 7, payload), cancellationToken);

            var puback = await ReadPacketAsync(cancellationToken);
            Assert.AreEqual(PubackType, puback.Type, "surl answers a QoS 1 PUBLISH with PUBACK");
            Assert.AreEqual(7, (puback.Body[0] << 8) | puback.Body[1], "the PUBACK carries the PUBLISH's Packet Identifier");
        }

        public async Task PublishQosTwoAsync(string topic, string payload, bool retain, CancellationToken cancellationToken)
        {
            await SendAsync((PublishType << 4) | (2 << 1) | (retain ? 1 : 0), PublishBody(topic, packetIdentifier: 9, payload), cancellationToken);

            var pubrec = await ReadPacketAsync(cancellationToken);
            Assert.AreEqual(PubrecType, pubrec.Type, "surl answers a QoS 2 PUBLISH with PUBREC");
            Assert.AreEqual(9, (pubrec.Body[0] << 8) | pubrec.Body[1], "the PUBREC carries the PUBLISH's Packet Identifier");

            await SendAsync((PubrelType << 4) | 0b0010, [0, 9], cancellationToken);
            var pubcomp = await ReadPacketAsync(cancellationToken);
            Assert.AreEqual(PubcompType, pubcomp.Type, "surl answers PUBREL with PUBCOMP");
            Assert.AreEqual(9, (pubcomp.Body[0] << 8) | pubcomp.Body[1], "the PUBCOMP carries the PUBREL's Packet Identifier");
        }

        public async Task<ReceivedPublish> ReadPublishAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var packet = await ReadPacketAsync(cancellationToken);
                if (packet.Type != PublishType)
                {
                    continue;
                }

                var topicLength = (packet.Body[0] << 8) | packet.Body[1];
                var topic = Encoding.UTF8.GetString(packet.Body, 2, topicLength);
                var propertiesAt = 2 + topicLength; // a QoS 0 delivery has no Packet Identifier
                var payloadAt = propertiesAt + 1 + packet.Body[propertiesAt]; // properties are shorter than 128 bytes
                return new ReceivedPublish(topic, Encoding.UTF8.GetString(packet.Body, payloadAt, packet.Body.Length - payloadAt));
            }
        }

        public ValueTask DisposeAsync()
        {
            stream.Dispose();
            tcp.Dispose();
            return ValueTask.CompletedTask;
        }

        private static byte[] PublishBody(string topic, int packetIdentifier, string payload)
        {
            var body = new List<byte>(Utf8String(topic));
            body.AddRange([(byte)(packetIdentifier >> 8), (byte)packetIdentifier, 0]);
            body.AddRange(Encoding.UTF8.GetBytes(payload));
            return body.ToArray();
        }

        private static byte[] Utf8String(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return [(byte)(bytes.Length >> 8), (byte)bytes.Length, .. bytes];
        }

        private async Task SendAsync(int fixedHeaderByte, byte[] body, CancellationToken cancellationToken)
        {
            var frame = new List<byte> { (byte)fixedHeaderByte };
            var remaining = body.Length;
            do
            {
                var digit = remaining % 128;
                remaining /= 128;
                frame.Add((byte)(remaining > 0 ? digit | 0x80 : digit));
            }
            while (remaining > 0);

            frame.AddRange(body);
            await stream.WriteAsync(frame.ToArray(), cancellationToken);
        }

        private async Task<Packet> ReadPacketAsync(CancellationToken cancellationToken)
        {
            var first = new byte[1];
            await stream.ReadExactlyAsync(first, cancellationToken);
            var length = 0;
            var multiplier = 1;
            var one = new byte[1];
            do
            {
                await stream.ReadExactlyAsync(one, cancellationToken);
                length += (one[0] & 0x7F) * multiplier;
                multiplier *= 128;
            }
            while ((one[0] & 0x80) != 0);

            var body = new byte[length];
            await stream.ReadExactlyAsync(body, cancellationToken);
            return new Packet(first[0] >> 4, body);
        }
    }
}
