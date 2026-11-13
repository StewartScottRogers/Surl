using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The requirements of <c>Documentation/Conformance/Extended/Mqtt5.md</c> that BL-1096 gave a
/// test of their own, every expected byte written from MQTT 5.0.
/// </summary>
public sealed partial class Mqtt5BrokerTests
{
    private static readonly byte[] PubAck1 = [0x40, 0x02, 0x00, 0x01];

    private static readonly byte[] PingResponse = [0xD0, 0x00];

    [TestMethod]
    public async Task ServeAsync_WillRetain0_IsPublishedNonRetained_MQTT_3_1_2_14()
    {
        var state = NewState();
        var broker = Broker(state);
        var subscriber = await ConnectClientAsync(broker, "s");
        await SubscribeAsync(subscriber, "w", 0);

        await ServeAsync(broker, Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect("p", will: new Mqtt5Will(0, false, [], "w", "gone"u8.ToArray())));

        CollectionAssert.AreEqual(new byte[] { 0x30, 0x08, 0x00, 0x01, 0x77, 0x00, 0x67, 0x6F, 0x6E, 0x65 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(state.Bus.Connect().Subscribe("w", _ => { }, includeRetained: true).RetainedMessages);
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_PublishWithRetain0_IsNotStoredAndLeavesTheRetainedMessage_MQTT_3_3_1_8()
    {
        var state = NewState();
        var publisher = await ConnectClientAsync(Broker(state), "p");
        publisher.Send(Publish("t", "kept", 0, 0, retain: true));
        publisher.Send(Publish("t", "live", 0, 0, retain: false));
        publisher.Send(PingRequest);
        CollectionAssert.AreEqual(PingResponse, await publisher.ReadPacketAsync());

        var retained = state.Bus.Connect().Subscribe("t", _ => { }, includeRetained: true).RetainedMessages.Single();

        CollectionAssert.AreEqual("kept"u8.ToArray(), retained.Payload.ToArray());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_TopicAliasSetOnOneConnection_IsNotKnownToAnother_MQTT_3_3_2_7()
    {
        var broker = Broker();
        var first = await ConnectClientAsync(broker, "p1");
        var second = await ConnectClientAsync(broker, "p2");
        first.Send(Publish("t", "a", 0, 0, properties: [TopicAlias(1)]));
        first.Send(PingRequest);
        CollectionAssert.AreEqual(PingResponse, await first.ReadPacketAsync());

        second.Send(Publish(string.Empty, "b", 0, 0, properties: [TopicAlias(1)]));

        CollectionAssert.AreEqual(Disconnect(0x82), await second.ReadPacketAsync());
        Assert.IsEmpty(await first.DisconnectAsync());
    }

    [TestMethod]
    [DataRow("r/#", DisplayName = "Multi-level wildcard")]
    [DataRow("r/+/x", DisplayName = "Single-level wildcard")]
    public async Task ServeAsync_ResponseTopicWithAWildcard_DisconnectsWithProtocolError_MQTT_3_3_2_14(string responseTopic)
    {
        var publisher = await ConnectClientAsync(Broker(), "p");

        publisher.Send(Publish("t", "hi", 0, 0, properties: [Mqtt5Property.FromText(Mqtt5PropertyIdentifier.ResponseTopic, responseTopic)]));

        CollectionAssert.AreEqual(Disconnect(0x82), await publisher.ReadPacketAsync());
        Assert.IsEmpty(await publisher.ServingEndedAsync());
    }

    [TestMethod]
    public async Task ServeAsync_ReceiveMaximumReached_StillAnswersPingRequest_MQTT_3_3_4_10()
    {
        var (broker, subscriber, publisher) = await SubscriberAtItsReceiveMaximumAsync();

        subscriber.Send(PingRequest);

        CollectionAssert.AreEqual(PingResponse, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsNotNull(broker);
    }

    [TestMethod]
    public async Task ServeAsync_SendQuotaZero_AnswersSubscribeUnsubscribeAndPing_MQTT_4_9_0_3()
    {
        var (_, subscriber, publisher) = await SubscriberAtItsReceiveMaximumAsync();

        subscriber.Send(Subscribe(2, ("u", 0)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x02, 0x00, 0x00 }, await subscriber.ReadPacketAsync());
        subscriber.Send(Unsubscribe(3, "u"));
        CollectionAssert.AreEqual(new byte[] { 0xB0, 0x04, 0x00, 0x03, 0x00, 0x00 }, await subscriber.ReadPacketAsync());
        subscriber.Send(PingRequest);
        CollectionAssert.AreEqual(PingResponse, await subscriber.ReadPacketAsync());
        subscriber.Send(PubAck1);
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x07, 0x00, 0x01, 0x74, 0x00, 0x02, 0x00, 0x62 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_QualityOfService1PacketIdentifier_IsANewMessageAfterItsPuback_MQTT_4_3_2_5()
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s");
        await SubscribeAsync(subscriber, "t", 1);
        var publisher = await ConnectClientAsync(broker, "p");

        publisher.Send(Publish("t", "a", 1, 5));
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x02, 0x00, 0x05 }, await publisher.ReadPacketAsync());
        publisher.Send(Publish("t", "b", 1, 5));
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x02, 0x00, 0x05 }, await publisher.ReadPacketAsync());

        CollectionAssert.AreEqual(new byte[] { 0x32, 0x07, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x61 }, await subscriber.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x07, 0x00, 0x01, 0x74, 0x00, 0x02, 0x00, 0x62 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_QualityOfService2PublishAlreadySent_IsNotExpired_MQTT_4_3_3_7()
    {
        var clock = new ManualTimeProvider();
        var broker = Broker(NewState(clock));
        var subscriber = await ConnectClientAsync(broker, "s", clock);
        await SubscribeAsync(subscriber, "t", 2);
        var publisher = await ConnectClientAsync(broker, "p", clock);
        publisher.Send(Publish("t", "hi", 2, 9, properties: [MessageExpiry(10)]));
        _ = await publisher.ReadPacketAsync();
        CollectionAssert.AreEqual(new byte[] { 0x34, 0x0D, 0x00, 0x01, 0x74, 0x00, 0x01, 0x05, 0x02, 0x00, 0x00, 0x00, 0x0A, 0x68, 0x69 }, await subscriber.ReadPacketAsync());

        clock.Advance(TimeSpan.FromSeconds(20));
        subscriber.Send([0x50, 0x02, 0x00, 0x01]);

        CollectionAssert.AreEqual(new byte[] { 0x62, 0x02, 0x00, 0x01 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_QualityOfService2SequenceAfterExpiry_RunsToPubcomp_MQTT_4_3_3_13()
    {
        var clock = new ManualTimeProvider();
        var broker = Broker(NewState(clock));
        var subscriber = await ConnectClientAsync(broker, "s", clock, [ReceiveMaximumProperty(1)]);
        await SubscribeAsync(subscriber, "t", 2);
        var publisher = await ConnectClientAsync(broker, "p", clock);
        publisher.Send(Publish("t", "a", 2, 1, properties: [MessageExpiry(10)]));
        publisher.Send(Publish("t", "b", 2, 2));
        _ = await publisher.ReadPacketAsync();
        _ = await publisher.ReadPacketAsync();
        _ = await subscriber.ReadPacketAsync();

        clock.Advance(TimeSpan.FromSeconds(20));
        subscriber.Send([0x50, 0x02, 0x00, 0x01]);
        CollectionAssert.AreEqual(new byte[] { 0x62, 0x02, 0x00, 0x01 }, await subscriber.ReadPacketAsync());
        subscriber.Send([0x70, 0x02, 0x00, 0x01]);

        // The freed quota lets the second message out.
        CollectionAssert.AreEqual(new byte[] { 0x34, 0x07, 0x00, 0x01, 0x74, 0x00, 0x02, 0x00, 0x62 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_DeliveriesStartedBeforeUnsubscribe_CompleteAfterIt_MQTT_3_10_4_3()
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s");
        await SubscribeAsync(subscriber, "q1", 1);
        subscriber.Send(Subscribe(2, ("q2", 2)));
        _ = await subscriber.ReadPacketAsync();
        var publisher = await ConnectClientAsync(broker, "p");
        publisher.Send(Publish("q1", "a", 1, 1));
        publisher.Send(Publish("q2", "b", 2, 2));
        _ = await publisher.ReadPacketAsync();
        _ = await publisher.ReadPacketAsync();
        var delivered = new[] { await subscriber.ReadPacketAsync(), await subscriber.ReadPacketAsync() };
        Assert.ContainsSingle(delivered.Where(packet => packet[0] == 0x32));
        Assert.ContainsSingle(delivered.Where(packet => packet[0] == 0x34));

        subscriber.Send(Unsubscribe(3, "q1", "q2"));
        CollectionAssert.AreEqual(new byte[] { 0xB0, 0x05, 0x00, 0x03, 0x00, 0x00, 0x00 }, await subscriber.ReadPacketAsync());
        var ids = delivered.ToDictionary(packet => packet[0], packet => packet[7]);
        subscriber.Send([0x40, 0x02, 0x00, ids[0x32]]);
        subscriber.Send([0x50, 0x02, 0x00, ids[0x34]]);

        CollectionAssert.AreEqual(new byte[] { 0x62, 0x02, 0x00, ids[0x34] }, await subscriber.ReadPacketAsync());
        subscriber.Send([0x70, 0x02, 0x00, ids[0x34]]);
        subscriber.Send(PingRequest);
        CollectionAssert.AreEqual(PingResponse, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_TopicOf65535Bytes_IsTakenAndDelivered_MQTT_4_7_3_3()
    {
        var topic = new string('a', ushort.MaxValue);
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s");
        subscriber.Send(new Mqtt5SubscribePacket(1, [], [new Mqtt5Subscription(topic, 0, false, false, 0)]));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await subscriber.ReadPacketAsync());
        var publisher = await ConnectClientAsync(broker, "p");

        publisher.Send(Publish(topic, "hi", 1, 1));

        CollectionAssert.AreEqual(PubAck1, await publisher.ReadPacketAsync());
        var delivered = await subscriber.ReadDecodedPacketAsync();
        Assert.AreEqual(topic, ((Mqtt5PublishPacket)delivered).TopicName);
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Mqtt5BodyWriter().WriteUtf8String(new string('a', ushort.MaxValue + 1)));
    }

    [TestMethod]
    public async Task ServeAsync_TopicMatching_DoesNoNormalisationOrSubstitution_MQTT_4_7_3_4()
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s");
        await SubscribeAsync(subscriber, "café", 0);
        var publisher = await ConnectClientAsync(broker, "p");

        publisher.Send(Publish("café", "decomposed", 0, 0));
        publisher.Send(Publish("CAFÉ", "upper", 0, 0));
        publisher.Send(Publish("café", "exact", 0, 0));

        var delivered = (Mqtt5PublishPacket)await subscriber.ReadDecodedPacketAsync();
        CollectionAssert.AreEqual("exact"u8.ToArray(), delivered.Payload);
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_ServerStopping_SendsDisconnectServerShuttingDown_MQTT5_3_14_2_1_2()
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new Mqtt5TestPipeConnection();
        var serving = Broker().ServeAsync(connection, Context(new ManualTimeProvider(), stopping.Token));
        connection.Send(Connect("c"));
        CollectionAssert.AreEqual(ConnackSuccess, await connection.ReadPacketAsync(TestContext.CancellationToken));

        await stopping.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        CollectionAssert.AreEqual(Disconnect(0x8B), connection.UnreadServerBytes());
    }

    [TestMethod]
    public async Task ServeAsync_ServerStoppingWithAClientThatReadsNothing_GivesUpOnTheNotice_MQTT5_3_14_2_1_2()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new Mqtt5StalledAfterConnackConnection(Connect("c"));
        var serving = Broker().ServeAsync(connection, Context(clock, stopping.Token, log: log));
        await connection.ConnectAcknowledged.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        await stopping.CancelAsync();
        while (!serving.IsCompleted)
        {
            clock.Advance(TimeSpan.FromSeconds(3));
            await Task.Yield();
        }

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.Contains("The DISCONNECT 0x8B could not be written within the shutdown notice timeout.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_SharedSubscriptionGrantedQos0_DeliversAtQos0_MQTT_4_8_2_3()
    {
        var broker = Broker();
        var member = await ConnectClientAsync(broker, "m");
        await SubscribeAsync(member, "$share/g/t", 0);
        var publisher = await ConnectClientAsync(broker, "p");

        publisher.Send(Publish("t", "hi", 2, 9));

        CollectionAssert.AreEqual(new byte[] { 0x50, 0x02, 0x00, 0x09 }, await publisher.ReadPacketAsync());
        CollectionAssert.AreEqual(PublishHiAtQos0, await member.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
        Assert.IsEmpty(await member.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_SharedQos2DeliveryCutByALostConnection_CompletesOnTheMembersReconnect_MQTT_4_8_2_4()
    {
        var broker = Broker();
        var member = await Mqtt5TestClient.ConnectAsync(broker, Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect("m", properties: [SessionExpiry(60)]));
        await SubscribeAsync(member, "$share/g/t", 2);
        var publisher = await ConnectClientAsync(broker, "p");
        publisher.Send(Publish("t", "hi", 2, 9));
        _ = await publisher.ReadPacketAsync();
        CollectionAssert.AreEqual(new byte[] { 0x34, 0x08, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x68, 0x69 }, await member.ReadPacketAsync());
        member.Abort();
        _ = await member.ServingEndedAsync();

        var returned = await Mqtt5TestClient.ConnectAsync(broker, Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect("m", cleanStart: false, properties: [SessionExpiry(60)]));

        CollectionAssert.AreEqual(new byte[] { 0x3C, 0x08, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x68, 0x69 }, await returned.ReadPacketAsync());
        returned.Send([0x50, 0x02, 0x00, 0x01]);
        CollectionAssert.AreEqual(new byte[] { 0x62, 0x02, 0x00, 0x01 }, await returned.ReadPacketAsync());
        returned.Send([0x70, 0x02, 0x00, 0x01]);
        Assert.IsEmpty(await returned.DisconnectAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_SharedDeliveryWhoseMemberSessionEnds_GoesToNoOtherMember_MQTT_4_8_2_5()
    {
        var broker = Broker();
        var first = await ConnectClientAsync(broker, "m1");
        await SubscribeAsync(first, "$share/g/t", 1);
        var publisher = await ConnectClientAsync(broker, "p");
        publisher.Send(Publish("t", "hi", 1, 1));
        _ = await publisher.ReadPacketAsync();
        _ = await first.ReadPacketAsync();
        var second = await ConnectClientAsync(broker, "m2");
        await SubscribeAsync(second, "$share/g/t", 1);

        first.Abort();
        _ = await first.ServingEndedAsync();

        second.Send(PingRequest);
        CollectionAssert.AreEqual(PingResponse, await second.ReadPacketAsync());
        Assert.IsEmpty(await second.DisconnectAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    [DataRow((byte)0x40, DisplayName = "PUBACK 0x80")]
    [DataRow((byte)0x50, DisplayName = "PUBREC 0x80")]
    public async Task ServeAsync_SharedDeliveryRefusedWith0x80OrAbove_IsDiscardedAndGoesToNoOtherMember_MQTT_4_8_2_6(byte responseType)
    {
        var broker = Broker();
        var first = await ConnectClientAsync(broker, "m1");
        await SubscribeAsync(first, "$share/g/t", (byte)(responseType == 0x40 ? 1 : 2));
        var publisher = await ConnectClientAsync(broker, "p");
        publisher.Send(Publish("t", "hi", responseType == 0x40 ? 1 : 2, 1));
        _ = await publisher.ReadPacketAsync();
        _ = await first.ReadPacketAsync();
        var second = await ConnectClientAsync(broker, "m2");
        await SubscribeAsync(second, "$share/g/t", 1);

        first.Send([responseType, 0x03, 0x00, 0x01, 0x80]);
        first.Abort();
        _ = await first.ServingEndedAsync();

        second.Send(PingRequest);
        CollectionAssert.AreEqual(PingResponse, await second.ReadPacketAsync());
        Assert.IsEmpty(await second.DisconnectAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    // A subscriber with Receive Maximum 1 at QoS 1 holding one unacknowledged delivery, with a
    // second message waiting behind it.
    private async Task<(Mqtt5Broker Broker, Mqtt5TestClient Subscriber, Mqtt5TestClient Publisher)> SubscriberAtItsReceiveMaximumAsync()
    {
        var broker = Broker();
        var subscriber = await ConnectClientAsync(broker, "s", properties: [ReceiveMaximumProperty(1)]);
        await SubscribeAsync(subscriber, "t", 1);
        var publisher = await ConnectClientAsync(broker, "p");
        publisher.Send(Publish("t", "a", 1, 1));
        publisher.Send(Publish("t", "b", 1, 2));
        _ = await publisher.ReadPacketAsync();
        _ = await publisher.ReadPacketAsync();
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x07, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x61 }, await subscriber.ReadPacketAsync());
        return (broker, subscriber, publisher);
    }
}
