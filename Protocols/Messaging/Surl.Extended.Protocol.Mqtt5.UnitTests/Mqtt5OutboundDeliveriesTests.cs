using System.Threading.Channels;
using Surl.MessageBus;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The broker as the sender of sections 4.3.2 and 4.3.3 toward one client (ADR-0093 decision 4).
/// </summary>
[TestClass]
public sealed class Mqtt5OutboundDeliveriesTests
{
    private readonly Channel<byte[]> written = Channel.CreateUnbounded<byte[]>();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Acknowledge_ForNoPublishInFlight_IsIgnoredButAPubrecGetsPacketIdentifierNotFound()
    {
        var deliveries = NewDeliveries(receiveMaximum: 2);

        Assert.IsNull(deliveries.Acknowledge(Response(Mqtt5PacketType.PublishAcknowledgement, 1)));
        Assert.IsNull(deliveries.Acknowledge(Response(Mqtt5PacketType.PublishComplete, 1)));
        Assert.AreEqual(
            new Mqtt5PublishResponsePacket(Mqtt5PacketType.PublishRelease, 1, Mqtt5ReasonCode.PacketIdentifierNotFound, []),
            deliveries.Acknowledge(Response(Mqtt5PacketType.PublishReceived, 1))! with { Properties = [] });
    }

    [TestMethod]
    public async Task Acknowledge_PubrecWithAnErrorCode_EndsTheMessageAndFreesItsSlot()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var deliveries = NewDeliveries(receiveMaximum: 1);
        var running = deliveries.RunAsync(cancellation.Token);

        deliveries.Offer(Message(BusQualityOfService.ExactlyOnce), 2, retain: false);
        CollectionAssert.AreEqual(new byte[] { 0x34, 0x06, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00 }, await ReadWrittenAsync());
        deliveries.Offer(Message(BusQualityOfService.ExactlyOnce), 2, retain: false);
        Assert.IsNull(deliveries.Acknowledge(Response(Mqtt5PacketType.PublishReceived, 1, Mqtt5ReasonCode.UnspecifiedError)));

        CollectionAssert.AreEqual(new byte[] { 0x34, 0x06, 0x00, 0x01, 0x74, 0x00, 0x02, 0x00 }, await ReadWrittenAsync());
        await cancellation.CancelAsync();
        await running;
    }

    [TestMethod]
    public async Task Offer_NextPacketIdentifierStillInFlight_SkipsIt()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var deliveries = NewDeliveries(receiveMaximum: 2);
        var running = deliveries.RunAsync(cancellation.Token);
        deliveries.LastPacketIdentifier = ushort.MaxValue;

        deliveries.Offer(Message(BusQualityOfService.AtLeastOnce), 1, retain: false);
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x06, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00 }, await ReadWrittenAsync());
        deliveries.LastPacketIdentifier = 0;
        deliveries.Offer(Message(BusQualityOfService.AtLeastOnce), 1, retain: false);

        CollectionAssert.AreEqual(new byte[] { 0x32, 0x06, 0x00, 0x01, 0x74, 0x00, 0x02, 0x00 }, await ReadWrittenAsync());
        await cancellation.CancelAsync();
        await running;
    }

    private static BusMessage Message(BusQualityOfService qualityOfService) => new("t", Array.Empty<byte>()) { QualityOfService = qualityOfService };

    private static Mqtt5PublishResponsePacket Response(Mqtt5PacketType type, ushort identifier, Mqtt5ReasonCode reasonCode = Mqtt5ReasonCode.Success) =>
        new(type, identifier, reasonCode, []);

    private Mqtt5OutboundDeliveries NewDeliveries(int receiveMaximum)
    {
        var client = new Mqtt5ConnectedClient("c", new object(), null, TimeSpan.FromSeconds(30), TimeSpan.Zero, null, TimeSpan.Zero) { ReceiveMaximum = receiveMaximum };
        return new Mqtt5OutboundDeliveries(
            client,
            new TopicNameLookup([]),
            Context(new ManualTimeProvider(), TestContext.CancellationToken),
            (bytes, _) => written.Writer.WriteAsync(bytes));
    }

    private async Task<byte[]> ReadWrittenAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        return await written.Reader.ReadAsync(timeout.Token);
    }
}
