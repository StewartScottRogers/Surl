using System.Net;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The MQTT server composed for <c>mqtt</c> (ADR-0014 Amendment 1, BL-1721):
/// <c>--mqtt-hold-subscription</c>'s count reaches it, and without the option a subscribe is one-shot.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    // MQTT 3.1.1 CONNECT (clean session, client "c"), SUBSCRIBE packet 1 to "t" at QoS 0.
    private static readonly byte[] ConnectSubscribeToT =
    [
        0x10, 0x0D, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, 0x02, 0x00, 0x3C, 0x00, 0x01, (byte)'c',
        0x82, 0x06, 0x00, 0x01, 0x00, 0x01, (byte)'t', 0x00,
    ];

    private static readonly byte[] ConnackThenSuback = [0x20, 0x02, 0x00, 0x00, 0x90, 0x03, 0x00, 0x01, 0x00];

    [TestMethod]
    public async Task ComposeAsync_MqttHoldSubscriptionOne_SendsALivePublishAndThenDisconnect()
    {
        var written = await SubscribeAndPublishAfterSubAckAsync(
            "--mqtt-hold-subscription", "1", "--allow-anonymous", "mqtt://127.0.0.1:0/");

        CollectionAssert.AreEqual(
            (byte[])[.. ConnackThenSuback, 0x30, 0x05, 0x00, 0x01, (byte)'t', (byte)'h', (byte)'i', 0xE0, 0x00], written);
    }

    [TestMethod]
    public async Task ComposeAsync_NoMqttHoldSubscription_SendsDisconnectAfterSubAck()
    {
        var written = await SubscribeAndPublishAfterSubAckAsync("--allow-anonymous", "mqtt://127.0.0.1:0/");

        CollectionAssert.AreEqual((byte[])[.. ConnackThenSuback, 0xE0, 0x00], written);
    }

    // Composes a start, serves ConnectSubscribeToT to its MQTT server, publishes "hi" to "t" on its bus as the SUBACK
    // is written, and gives every byte the server wrote once it ended the exchange.
    private async Task<byte[]> SubscribeAndPublishAfterSubAckAsync(params string[] arguments)
    {
        IConnectionProtocolServer? mqttServer = null;
        MessageBus.MessageBus? bus = null;
        var probe = new AdditionalProtocolServer(
            new ProtocolServerRegistration(["x-probe"], services =>
            {
                mqttServer = (IConnectionProtocolServer)services.ComposedServerFor("mqtt");
                bus = services.MessageBus;
                return new SchemeOnlyServer("x-probe");
            }),
            []);
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse(arguments),
            new StartTestSeams().Seams with { AdditionalProtocolServers = [probe] },
            TestContext.CancellationToken);
        await using var serving = start.Serving!;
        await using var connection = new OpenMqttSubscriberConnection(
            ConnectSubscribeToT, () => bus!.Publish(new BusMessage("t", "hi"u8.ToArray())));

        await mqttServer!.ServeAsync(connection, MqttContext()).WaitAsync(TestContext.CancellationToken);

        return connection.WrittenBytes;
    }

    // A client that sends its request and then stays connected and silent: a read after the request waits until the
    // server cancels it. The server's write of a SUBACK runs subAckWritten.
    private sealed class OpenMqttSubscriberConnection(byte[] request, Action subAckWritten) : IConnection
    {
        private readonly MemoryStream written = new();
        private int delivered;

        public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 1883);

        public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

        public TlsSession? TlsSession => null;

        public byte[] WrittenBytes => written.ToArray();

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (delivered == request.Length)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            var count = Math.Min(buffer.Length, request.Length - delivered);
            request.AsSpan(delivered, count).CopyTo(buffer.Span);
            delivered += count;
            return count;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            written.Write(bytes.Span);
            if (bytes.Span[0] == 0x90)
            {
                subAckWritten();
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public void Abort()
        {
        }

        public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("An MQTT exchange upgrades to no TLS.");

        public ValueTask DisposeAsync() => written.DisposeAsync();
    }
}
