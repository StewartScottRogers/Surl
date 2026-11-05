using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The broker's answers to <c>PUBLISH</c>, its QoS flows both ways, and the plain
/// <c>SUBSCRIBE</c> deliveries need (BL-497; ADR-0093 decision 4, ADR-0100 decision 3.9,
/// ADR-0102 decision 3.4), every expected byte written from MQTT 5.0.
/// </summary>
public sealed partial class Mqtt5BrokerTests
{
    // PUBLISH QoS 0, topic "t", no properties, payload "hi".
    private static readonly byte[] PublishHiAtQos0 = [0x30, 0x06, 0x00, 0x01, 0x74, 0x00, 0x68, 0x69];

    [TestMethod]
    [DataRow((byte)0, (byte)0, new byte[] { 0x30, 0x06, 0x00, 0x01, 0x74, 0x00, 0x68, 0x69 }, DisplayName = "QoS 0 to a QoS 0 subscription")]
    [DataRow((byte)2, (byte)0, new byte[] { 0x30, 0x06, 0x00, 0x01, 0x74, 0x00, 0x68, 0x69 }, DisplayName = "QoS 2 to a QoS 0 subscription goes at QoS 0")]
    [DataRow((byte)0, (byte)2, new byte[] { 0x30, 0x06, 0x00, 0x01, 0x74, 0x00, 0x68, 0x69 }, DisplayName = "QoS 0 to a QoS 2 subscription goes at QoS 0")]
    public async Task ServeAsync_Publish_DeliversAtTheLowerOfTheTwoQualitiesOfService(byte publishQos, byte grantedQos, byte[] expected)
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s");
        await SubscribeAsync(subscriber, "t", grantedQos);
        var publisher = await ConnectClientAsync(broker, "p");

        publisher.Send(Publish("t", "hi", publishQos, packetIdentifier: 5));
        if (publishQos == 2)
        {
            CollectionAssert.AreEqual(new byte[] { 0x50, 0x02, 0x00, 0x05 }, await publisher.ReadPacketAsync());
        }

        CollectionAssert.AreEqual(expected, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_QualityOfService1_IsAcknowledgedInboundAndOutbound()
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s");
        await SubscribeAsync(subscriber, "t", 1);
        var publisher = await ConnectClientAsync(broker, "p");

        publisher.Send(Publish("t", "hi", 1, packetIdentifier: 7));
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x02, 0x00, 0x07 }, await publisher.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x08, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
        subscriber.Send([0x40, 0x02, 0x00, 0x01]);
        publisher.Send(Publish("t", "hi", 1, packetIdentifier: 8));

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x02, 0x00, 0x08 }, await publisher.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x08, 0x00, 0x01, 0x74, 0x00, 0x02, 0x00, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_QualityOfService2_RunsPubrecPubrelPubcompBothWaysAndDeliversARepeatOnce()
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s");
        await SubscribeAsync(subscriber, "t", 2);
        var publisher = await ConnectClientAsync(broker, "p");

        publisher.Send(Publish("t", "hi", 2, packetIdentifier: 9));
        CollectionAssert.AreEqual(new byte[] { 0x50, 0x02, 0x00, 0x09 }, await publisher.ReadPacketAsync());
        publisher.Send(Publish("t", "hi", 2, packetIdentifier: 9, duplicate: true));
        CollectionAssert.AreEqual(new byte[] { 0x50, 0x02, 0x00, 0x09 }, await publisher.ReadPacketAsync());
        publisher.Send([0x62, 0x02, 0x00, 0x09]);
        CollectionAssert.AreEqual(new byte[] { 0x70, 0x02, 0x00, 0x09 }, await publisher.ReadPacketAsync());
        publisher.Send([0x62, 0x02, 0x00, 0x09]);
        CollectionAssert.AreEqual(new byte[] { 0x70, 0x03, 0x00, 0x09, 0x92 }, await publisher.ReadPacketAsync());

        CollectionAssert.AreEqual(new byte[] { 0x34, 0x08, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
        subscriber.Send([0x50, 0x02, 0x00, 0x01]);
        CollectionAssert.AreEqual(new byte[] { 0x62, 0x02, 0x00, 0x01 }, await subscriber.ReadPacketAsync());
        subscriber.Send([0x70, 0x02, 0x00, 0x01]);
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_Publish_NoSubscriber_AcknowledgesNoMatchingSubscribers()
    {
        var publisher = await ConnectClientAsync(Broker(), "p");

        publisher.Send(Publish("t", "hi", 1, packetIdentifier: 3));

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x03, 0x00, 0x03, 0x10 }, await publisher.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_QualityOfService2PastTheReceiveMaximum_DisconnectsWithReceiveMaximumExceeded()
    {
        var publisher = await ConnectClientAsync(Broker(), "p");
        for (ushort identifier = 1; identifier <= Mqtt5Broker.ReceiveMaximum; identifier++)
        {
            publisher.Send(Publish("t", "hi", 2, identifier));
            _ = await publisher.ReadPacketAsync();
        }

        publisher.Send(Publish("t", "hi", 2, Mqtt5Broker.ReceiveMaximum + 1));

        CollectionAssert.AreEqual(Disconnect(0x93), await publisher.ReadPacketAsync());
        Assert.IsEmpty(await publisher.ServingEndedAsync());
    }

    [TestMethod]
    public async Task ServeAsync_TopicAliasSetThenUsed_DeliversBothOnTheAliasedTopic()
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s");
        await SubscribeAsync(subscriber, "t", 0);
        var publisher = await ConnectClientAsync(broker, "p");

        publisher.Send(Publish("t", "a", 0, 0, properties: [TopicAlias(1)]));
        publisher.Send(Publish(string.Empty, "b", 0, 0, properties: [TopicAlias(1)]));

        CollectionAssert.AreEqual(new byte[] { 0x30, 0x05, 0x00, 0x01, 0x74, 0x00, 0x61 }, await subscriber.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x05, 0x00, 0x01, 0x74, 0x00, 0x62 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    [DataRow("t", 11u, (byte)0x94, DisplayName = "An alias over the Topic Alias Maximum")]
    [DataRow("t", 0u, (byte)0x94, DisplayName = "Alias 0")]
    [DataRow("", 2u, (byte)0x82, DisplayName = "An alias never set")]
    public async Task ServeAsync_TopicAliasRefused_DisconnectsWithItsReasonCode(string topic, uint alias, byte reasonCode)
    {
        var publisher = await ConnectClientAsync(Broker(), "p");

        publisher.Send(Publish(topic, "hi", 0, 0, properties: [TopicAlias(alias)]));

        CollectionAssert.AreEqual(Disconnect(reasonCode), await publisher.ReadPacketAsync());
        Assert.IsEmpty(await publisher.ServingEndedAsync());
    }

    [TestMethod]
    public async Task ServeAsync_EmptyTopicNameWithNoAlias_DisconnectsWithProtocolError()
    {
        var publisher = await ConnectClientAsync(Broker(), "p");

        publisher.Send([0x30, 0x03, 0x00, 0x00, 0x00]);

        CollectionAssert.AreEqual(Disconnect(0x82), await publisher.ReadPacketAsync());
    }

    [TestMethod]
    public async Task ServeAsync_MessageExpiry_DropsAnExpiredMessageAndSendsTheIntervalLeftOnADelayedOne()
    {
        var clock = new ManualTimeProvider();
        var broker = Broker(NewState(clock));
        var subscriber = await ConnectClientAsync(broker, "s", clock, [ReceiveMaximumProperty(1)]);
        await SubscribeAsync(subscriber, "t", 1);
        var publisher = await ConnectClientAsync(broker, "p", clock);

        publisher.Send(Publish("t", "a", 0, 0));
        publisher.Send(Publish("t", "b", 1, 1));
        publisher.Send(Publish("t", "c", 1, 2, properties: [MessageExpiry(10)]));
        publisher.Send(Publish("t", "d", 1, 3, properties: [MessageExpiry(2)]));
        for (var acknowledgements = 0; acknowledgements < 3; acknowledgements++)
        {
            _ = await publisher.ReadPacketAsync();
        }

        CollectionAssert.AreEqual(new byte[] { 0x30, 0x05, 0x00, 0x01, 0x74, 0x00, 0x61 }, await subscriber.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x07, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x62 }, await subscriber.ReadPacketAsync());
        clock.Advance(TimeSpan.FromSeconds(3));
        subscriber.Send([0x40, 0x02, 0x00, 0x01]);
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x0C, 0x00, 0x01, 0x74, 0x00, 0x02, 0x05, 0x02, 0x00, 0x00, 0x00, 0x07, 0x63 }, await subscriber.ReadPacketAsync());
        subscriber.Send([0x40, 0x02, 0x00, 0x02]);
        publisher.Send(Publish("t", "e", 1, 4));
        _ = await publisher.ReadPacketAsync();

        CollectionAssert.AreEqual(new byte[] { 0x32, 0x07, 0x00, 0x01, 0x74, 0x00, 0x03, 0x00, 0x65 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_Utf8IndicatedPayloadThatIsNot_AcknowledgesPayloadFormatInvalidAndDeliversNothing()
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s");
        await SubscribeAsync(subscriber, "t", 0);
        var publisher = await ConnectClientAsync(broker, "p");

        publisher.Send(Mqtt5PacketEncoder.Encode(new Mqtt5PublishPacket(false, 1, false, "t", 4, [PayloadFormat(1)], [0xFF])));
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x03, 0x00, 0x04, 0x99 }, await publisher.ReadPacketAsync());

        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    [DataRow(0, 1u, new byte[] { 0xFF }, (byte)0x99, DisplayName = "QoS 0, Payload Format Indicator 1, not UTF-8")]
    [DataRow(1, 2u, new byte[] { 0x61 }, (byte)0x82, DisplayName = "Payload Format Indicator 2")]
    public async Task ServeAsync_PayloadFormatRefused_DisconnectsWithItsReasonCode(int qualityOfService, uint indicator, byte[] payload, byte reasonCode)
    {
        var publisher = await ConnectClientAsync(Broker(), "p");

        publisher.Send(Mqtt5PacketEncoder.Encode(new Mqtt5PublishPacket(false, qualityOfService, false, "t", (ushort)qualityOfService, [PayloadFormat(indicator)], payload)));

        CollectionAssert.AreEqual(Disconnect(reasonCode), await publisher.ReadPacketAsync());
    }

    [TestMethod]
    public async Task ServeAsync_PublishWithASubscriptionIdentifier_DisconnectsWithProtocolError()
    {
        var publisher = await ConnectClientAsync(Broker(), "p");

        publisher.Send(Publish("t", "hi", 0, 0, properties: [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.SubscriptionIdentifier, 1)]));

        CollectionAssert.AreEqual(Disconnect(0x82), await publisher.ReadPacketAsync());
    }

    [TestMethod]
    public async Task ServeAsync_RetainedMessage_GoesToALaterSubscriberWithRetainSetAndAnEmptyOneClearsIt()
    {
        var broker = Broker();
        var publisher = await ConnectClientAsync(broker, "p");
        publisher.Send(Publish("t", "hi", 1, 1, retain: true));
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x03, 0x00, 0x01, 0x10 }, await publisher.ReadPacketAsync());

        var later = await ConnectClientAsync(broker, "s1");
        await SubscribeAsync(later, "t", 1);
        CollectionAssert.AreEqual(new byte[] { 0x33, 0x08, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x68, 0x69 }, await later.ReadPacketAsync());
        later.Send([0x40, 0x02, 0x00, 0x01]);
        publisher.Send(Publish("t", string.Empty, 0, 0, retain: true));
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x04, 0x00, 0x01, 0x74, 0x00 }, await later.ReadPacketAsync());
        var last = await ConnectClientAsync(broker, "s2");
        await SubscribeAsync(last, "t", 1);

        Assert.IsEmpty(await last.DisconnectAsync());
        Assert.IsEmpty(await later.DisconnectAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_ForwardedProperties_ArriveUnalteredWithUserPropertiesInOrder()
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s");
        await SubscribeAsync(subscriber, "t", 0);
        var publisher = await ConnectClientAsync(broker, "p");
        Mqtt5Property[] forwarded =
        [
            PayloadFormat(1),
            Mqtt5Property.FromText(Mqtt5PropertyIdentifier.ResponseTopic, "r"),
            Mqtt5Property.FromBinary(Mqtt5PropertyIdentifier.CorrelationData, [1, 2]),
            Mqtt5Property.FromUserProperty("a", "1"),
            Mqtt5Property.FromUserProperty("b", "2"),
            Mqtt5Property.FromUserProperty("a", "3"),
            Mqtt5Property.FromText(Mqtt5PropertyIdentifier.ContentType, "text/plain"),
        ];

        publisher.Send(Publish("t", "hi", 0, 0, properties: forwarded));

        CollectionAssert.AreEqual(Publish("t", "hi", 0, 0, properties: forwarded), await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_MessageAnotherServerPublishesOnTheBus_IsDeliveredToAnMqtt5Subscriber()
    {
        var state = NewState();
        var subscriber = await ConnectClientAsync(Broker(state), "s");
        await SubscribeAsync(subscriber, "t", 1);

        state.Bus.Publish(new BusMessage("t", "hi"u8.ToArray()) { QualityOfService = BusQualityOfService.AtLeastOnce });

        CollectionAssert.AreEqual(new byte[] { 0x32, 0x08, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
        subscriber.Send([0x40, 0x02, 0x00, 0x01]);
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_RetainedPublish_IsTheBussRetainedMessage()
    {
        var state = NewState();
        var publisher = await ConnectClientAsync(Broker(state), "p");

        publisher.Send(Publish("t", "hi", 0, 0, retain: true));
        Assert.IsEmpty(await publisher.DisconnectAsync());

        var retained = state.Bus.Connect().Subscribe("t", _ => { }, includeRetained: true).RetainedMessages.Single();
        Assert.AreEqual("t", retained.Topic);
        CollectionAssert.AreEqual("hi"u8.ToArray(), retained.Payload.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_TopicAccessRefuses_AnswersNotAuthorizedAndDropsAQualityOfService0Publish()
    {
        var bus = new Surl.MessageBus.MessageBus(
            TimeProvider.System,
            configuration: MessageBusConfiguration.None with { AccessGrants = [new TopicAccessGrant("open/#", "alice", TopicAccessRights.PublishAndSubscribe)] });
        var log = new RecordingExchangeLog();
        var broker = Broker(new Mqtt5BrokerState(bus), new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted));
        var alice = await Mqtt5TestClient.ConnectAsync(broker, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log), ConnectAsAlice);

        alice.Send(Publish("t", "hi", 1, 1));
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x03, 0x00, 0x01, 0x87 }, await alice.ReadPacketAsync());
        alice.Send(Publish("t", "hi", 2, 2));
        CollectionAssert.AreEqual(new byte[] { 0x50, 0x03, 0x00, 0x02, 0x87 }, await alice.ReadPacketAsync());
        alice.Send([0x62, 0x02, 0x00, 0x02]);
        CollectionAssert.AreEqual(new byte[] { 0x70, 0x03, 0x00, 0x02, 0x92 }, await alice.ReadPacketAsync());
        alice.Send(Publish("t", "hi", 0, 0));
        alice.Send(Subscribe(1, ("t", 0), ("open/x", 1)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x05, 0x00, 0x01, 0x00, 0x87, 0x01 }, await alice.ReadPacketAsync());
        alice.Send(Publish("open/x", "hi", 1, 3));

        // Her own PUBACK and the delivery to her own subscription may go out in either order.
        var answers = new[] { await alice.ReadPacketAsync(), await alice.ReadPacketAsync() };
        Assert.ContainsSingle(answers.Where(answer => answer.SequenceEqual(new byte[] { 0x40, 0x02, 0x00, 0x03 })));
        Assert.ContainsSingle(answers.Where(answer => answer[0] == 0x32));
        Assert.IsEmpty(await alice.DisconnectAsync());
        Assert.Contains("A QoS 0 PUBLISH was refused 0x87; dropped.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_LoginAcceptedUnchecked_PublishesAsAnAnonymousCaller()
    {
        var bus = new Surl.MessageBus.MessageBus(
            TimeProvider.System,
            configuration: MessageBusConfiguration.None with { AccessGrants = [new TopicAccessGrant("#", "alice", TopicAccessRights.Publish)] });
        var anonymous = await ConnectClientAsync(Broker(new Mqtt5BrokerState(bus)), "p");

        anonymous.Send(Publish("t", "hi", 1, 1));

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x03, 0x00, 0x01, 0x87 }, await anonymous.ReadPacketAsync());
        Assert.IsEmpty(await anonymous.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_Subscribe_AnswersInvalidFilterAndQuotaExceededPerFilterAndSharedOnesGetNoRetainedMessages()
    {
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System, maxSubscriptionsPerConnection: 1);
        bus.Publish(new BusMessage("t", "hi"u8.ToArray()) { Retain = true });
        var subscriber = await ConnectClientAsync(Broker(new Mqtt5BrokerState(bus)), "s");

        subscriber.Send(Subscribe(1, ("a/#/b", 0), ("$share/g/t", 2), ("u", 1)));

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x06, 0x00, 0x01, 0x00, 0x8F, 0x02, 0x97 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    [DataRow(1, 2L, 1L, (byte)0x97, DisplayName = "A retained message past the retained bytes")]
    [DataRow(0, 1L, 100L, (byte)0x95, DisplayName = "A payload past the bus's message size")]
    public async Task ServeAsync_PublishPastABusBound_AnswersAsTheAdrSays(int qualityOfService, long maxMessageBytes, long maxRetainedPayloadBytes, byte reasonCode)
    {
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System, maxRetainedPayloadBytes: maxRetainedPayloadBytes, maxMessageBytes: maxMessageBytes);
        var publisher = await ConnectClientAsync(Broker(new Mqtt5BrokerState(bus)), "p");

        publisher.Send(Publish("t", "hi", qualityOfService, (ushort)qualityOfService, retain: true));

        var expected = qualityOfService == 1 ? new byte[] { 0x40, 0x03, 0x00, 0x01, reasonCode } : Disconnect(reasonCode);
        CollectionAssert.AreEqual(expected, await publisher.ReadPacketAsync());
    }

    [TestMethod]
    public async Task ServeAsync_BusCannotSave_NotesItAndGoesOn()
    {
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System);
        await bus.LoadAsync(new MessageBusFiles(new UnitTestWriteFailingContentFileSystem(), Path.Join(InMemoryRoot, ".surl")), TestContext.CancellationToken);
        var log = new RecordingExchangeLog();
        var publisher = await Mqtt5TestClient.ConnectAsync(Broker(new Mqtt5BrokerState(bus)), Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log), Connect("p"));

        publisher.Send(Publish("t", "hi", 1, 1));

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x03, 0x00, 0x01, 0x10 }, await publisher.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.Contains("The message bus could not save a change (The disk failed.); it stays in memory.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_SubscriberAllowsTopicAliases_GetsTheAliasThenAnEmptyTopicName()
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s", properties: [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.TopicAliasMaximum, 1)]);
        await SubscribeAsync(subscriber, "t", 0);
        var publisher = await ConnectClientAsync(broker, "p");

        publisher.Send(PublishHiAtQos0, PublishHiAtQos0);

        CollectionAssert.AreEqual(new byte[] { 0x30, 0x09, 0x00, 0x01, 0x74, 0x03, 0x23, 0x00, 0x01, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x08, 0x00, 0x00, 0x03, 0x23, 0x00, 0x01, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_MessageOverTheSubscribersMaximumPacketSize_IsNotSent()
    {
        var broker = Broker();
        var log = new RecordingExchangeLog();
        var subscriber = await Mqtt5TestClient.ConnectAsync(
            broker,
            Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log),
            Connect("s", properties: [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.MaximumPacketSize, 8)]));
        await SubscribeAsync(subscriber, "t", 0);
        var publisher = await ConnectClientAsync(broker, "p");

        publisher.Send(Publish("t", "too long", 0, 0), PublishHiAtQos0);

        CollectionAssert.AreEqual(PublishHiAtQos0, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
        Assert.Contains("A message on t is larger than the client's Maximum Packet Size; not sent.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_TopicTheSubscriberCannotName_IsNotSent()
    {
        var bus = new Surl.MessageBus.MessageBus(
            TimeProvider.System,
            configuration: MessageBusConfiguration.None with { TopicMaps = [new TopicMap("mqtt5", "x", "mqtt5", "y")] });
        var subscriber = await ConnectClientAsync(Broker(new Mqtt5BrokerState(bus)), "s");
        await SubscribeAsync(subscriber, "#", 0);

        bus.Publish(new BusMessage("x", "no"u8.ToArray()));
        bus.Publish(new BusMessage("t", "hi"u8.ToArray()));

        CollectionAssert.AreEqual(PublishHiAtQos0, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_WillWithAnEmptyTopic_AnswersMalformedPacket()
    {
        var connection = await ServeAsync(
            Broker(),
            Context(new ManualTimeProvider(), TestContext.CancellationToken),
            Connect(will: new Mqtt5Will(0, false, [], string.Empty, [])));

        CollectionAssert.AreEqual(ConnackRefused(0x81), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_WillOfAClientThatLeaves_GoesToAnMqtt5Subscriber()
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s");
        await SubscribeAsync(subscriber, "w", 1);

        await ServeAsync(broker, Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect("p", will: Will()));

        CollectionAssert.AreEqual(new byte[] { 0x32, 0x0A, 0x00, 0x01, 0x77, 0x00, 0x01, 0x00, 0x67, 0x6F, 0x6E, 0x65 }, await subscriber.ReadPacketAsync());
        subscriber.Send([0x40, 0x02, 0x00, 0x01]);
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    private static string InMemoryRoot => Surl.Content.InMemoryContentFileSystem.RootPath;

    private static Mqtt5Property TopicAlias(uint alias) => Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.TopicAlias, alias);

    private static Mqtt5Property MessageExpiry(uint seconds) => Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.MessageExpiryInterval, seconds);

    private static Mqtt5Property PayloadFormat(uint indicator) => Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.PayloadFormatIndicator, indicator);

    private static Mqtt5Property ReceiveMaximumProperty(uint maximum) => Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ReceiveMaximum, maximum);

    private static byte[] Publish(
        string topic,
        string payload,
        int qualityOfService,
        int packetIdentifier,
        bool retain = false,
        bool duplicate = false,
        IReadOnlyList<Mqtt5Property>? properties = null) =>
        Mqtt5PacketEncoder.Encode(new Mqtt5PublishPacket(duplicate, qualityOfService, retain, topic, (ushort)packetIdentifier, properties ?? [], Encoding.UTF8.GetBytes(payload)));

    // SUBSCRIBE (3.8): Packet Identifier, Property Length 0, then each filter and its options byte.
    private static byte[] Subscribe(ushort packetIdentifier, params (string Filter, byte Options)[] filters)
    {
        var body = new List<byte> { (byte)(packetIdentifier >> 8), (byte)packetIdentifier, 0x00 };
        foreach (var (filter, options) in filters)
        {
            var bytes = Encoding.UTF8.GetBytes(filter);
            body.AddRange([(byte)(bytes.Length >> 8), (byte)bytes.Length, .. bytes, options]);
        }

        return [0x82, (byte)body.Count, .. body];
    }

    private static async Task SubscribeAsync(Mqtt5TestClient client, string filter, byte qualityOfService)
    {
        client.Send(Subscribe(1, (filter, qualityOfService)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, qualityOfService }, await client.ReadPacketAsync());
    }

    private Task<Mqtt5TestClient> ConnectClientAsync(Mqtt5Broker broker, string clientIdentifier, TimeProvider? clock = null, IReadOnlyList<Mqtt5Property>? properties = null) =>
        Mqtt5TestClient.ConnectAsync(broker, Context(clock ?? new ManualTimeProvider(), TestContext.CancellationToken), Connect(clientIdentifier, properties: properties));
}
