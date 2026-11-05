using System.Threading.Channels;
using Surl.MessageBus;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The broker as the sender of sections 4.3.2 and 4.3.3 toward one client (ADR-0093 decisions 3 and 4).
/// </summary>
[TestClass]
public sealed class Mqtt5OutboundDeliveriesTests
{
    private readonly Channel<byte[]> written = Channel.CreateUnbounded<byte[]>();
    private readonly ManualTimeProvider clock = new();
    private Mqtt5Session session = null!;

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

        session.Offer(Message(BusQualityOfService.ExactlyOnce), 2, retain: false, []);
        CollectionAssert.AreEqual(new byte[] { 0x34, 0x06, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00 }, await ReadWrittenAsync());
        session.Offer(Message(BusQualityOfService.ExactlyOnce), 2, retain: false, []);
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
        session.LastPacketIdentifier = ushort.MaxValue;

        session.Offer(Message(BusQualityOfService.AtLeastOnce), 1, retain: false, []);
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x06, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00 }, await ReadWrittenAsync());
        session.LastPacketIdentifier = 0;
        session.Offer(Message(BusQualityOfService.AtLeastOnce), 1, retain: false, []);

        CollectionAssert.AreEqual(new byte[] { 0x32, 0x06, 0x00, 0x01, 0x74, 0x00, 0x02, 0x00 }, await ReadWrittenAsync());
        await cancellation.CancelAsync();
        await running;
    }

    [TestMethod]
    public async Task RunAsync_SessionWithMessagesInFlight_ResendsThePublishesWithDupAndThePubrelsInTheirOrder()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var deliveries = NewDeliveries(receiveMaximum: 10, inFlight:
        [
            (7, new(2, Mqtt5PacketType.PublishComplete, Delivery(2))),
            (3, new(1, Mqtt5PacketType.PublishAcknowledgement, Delivery(1))),
            (9, new(3, Mqtt5PacketType.PublishReceived, Delivery(2))),
        ]);

        var running = deliveries.RunAsync(cancellation.Token);

        CollectionAssert.AreEqual(new byte[] { 0x3A, 0x06, 0x00, 0x01, 0x74, 0x00, 0x03, 0x00 }, await ReadWrittenAsync());
        CollectionAssert.AreEqual(new byte[] { 0x62, 0x02, 0x00, 0x07 }, await ReadWrittenAsync());
        CollectionAssert.AreEqual(new byte[] { 0x3C, 0x06, 0x00, 0x01, 0x74, 0x00, 0x09, 0x00 }, await ReadWrittenAsync());
        await cancellation.CancelAsync();
        await running;
    }

    [TestMethod]
    public async Task RunAsync_MessageInFlightExpired_DropsItAndFreesItsSlot()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var expired = Delivery(1) with { Message = Message(BusQualityOfService.AtLeastOnce) with { ExpiresAt = clock.GetUtcNow() } };
        var deliveries = NewDeliveries(receiveMaximum: 1, inFlight: [(4, new(1, Mqtt5PacketType.PublishAcknowledgement, expired))]);
        var running = deliveries.RunAsync(cancellation.Token);

        session.Offer(Message(BusQualityOfService.AtLeastOnce), 1, retain: false, []);

        CollectionAssert.AreEqual(new byte[] { 0x32, 0x06, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00 }, await ReadWrittenAsync());
        await cancellation.CancelAsync();
        await running;
    }

    [TestMethod]
    public async Task Acknowledge_MoreInFlightThanTheReceiveMaximum_FreesASlotOnlyOnceTheExcessIsAcknowledged()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var deliveries = NewDeliveries(receiveMaximum: 1, inFlight:
        [
            (1, new(1, Mqtt5PacketType.PublishAcknowledgement, Delivery(1))),
            (2, new(2, Mqtt5PacketType.PublishAcknowledgement, Delivery(1))),
        ]);
        session.LastPacketIdentifier = 2;
        var running = deliveries.RunAsync(cancellation.Token);
        await ReadWrittenAsync();
        await ReadWrittenAsync();

        Assert.IsNull(deliveries.Acknowledge(Response(Mqtt5PacketType.PublishAcknowledgement, 1)));
        Assert.IsNull(deliveries.Acknowledge(Response(Mqtt5PacketType.PublishAcknowledgement, 2)));
        session.Offer(Message(BusQualityOfService.AtLeastOnce), 1, retain: false, []);

        CollectionAssert.AreEqual(new byte[] { 0x32, 0x06, 0x00, 0x01, 0x74, 0x00, 0x03, 0x00 }, await ReadWrittenAsync());
        await cancellation.CancelAsync();
        await running;
    }

    private static BusMessage Message(BusQualityOfService qualityOfService) => new("t", Array.Empty<byte>()) { QualityOfService = qualityOfService };

    private static Mqtt5Delivery Delivery(int qualityOfService) =>
        new(Message((BusQualityOfService)qualityOfService), qualityOfService, false, []);

    private static Mqtt5PublishResponsePacket Response(Mqtt5PacketType type, ushort identifier, Mqtt5ReasonCode reasonCode = Mqtt5ReasonCode.Success) =>
        new(type, identifier, reasonCode, []);

    private Mqtt5OutboundDeliveries NewDeliveries(int receiveMaximum, (ushort Identifier, Mqtt5InFlightPublish Publish)[]? inFlight = null)
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        session = new Mqtt5Session("c", bus.Connect(), bus.Names, "mqtt5", Mqtt5Session.MaxQueuedMessages, Mqtt5Session.MaxQueuedPayloadBytes);
        foreach (var (identifier, publish) in inFlight ?? [])
        {
            session.InFlight[identifier] = publish;
        }

        var client = new Mqtt5ConnectedClient("c", new object(), null, TimeSpan.FromSeconds(30), null, TimeSpan.Zero, null) { ReceiveMaximum = receiveMaximum };
        return new Mqtt5OutboundDeliveries(
            session,
            client,
            bus.Names,
            Context(clock, TestContext.CancellationToken),
            (bytes, _) => written.Writer.WriteAsync(bytes));
    }

    private async Task<byte[]> ReadWrittenAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        return await written.Reader.ReadAsync(timeout.Token);
    }
}
