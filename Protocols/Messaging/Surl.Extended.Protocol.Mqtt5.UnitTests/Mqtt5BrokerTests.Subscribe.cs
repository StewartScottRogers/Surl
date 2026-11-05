using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The broker's answers to <c>SUBSCRIBE</c> with its options and identifiers, and to
/// <c>UNSUBSCRIBE</c> (BL-498; ADR-0093 decision 5), every expected byte written from MQTT 5.0.
/// </summary>
public sealed partial class Mqtt5BrokerTests
{
    private const byte NoLocal = 0x04;
    private const byte RetainAsPublished = 0x08;

    [TestMethod]
    public async Task ServeAsync_Subscribe_GrantsEachRequestedQualityOfServiceAndAnswersAnInvalidFilterTopicFilterInvalid()
    {
        var subscriber = await ConnectClientAsync(Broker(), "s");

        subscriber.Send(Subscribe(3, ("t", 0), ("u", 1), ("v", 2), ("a/#/b", 0)));

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x07, 0x00, 0x03, 0x00, 0x00, 0x01, 0x02, 0x8F }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    [DataRow("a/#/b", DisplayName = "# not last")]
    [DataRow("a/b#", DisplayName = "# not alone in its level")]
    [DataRow("a+/b", DisplayName = "+ not alone in its level")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("$share//t", DisplayName = "empty share name")]
    [DataRow("$share/g+/t", DisplayName = "+ in the share name")]
    [DataRow("$share/g", DisplayName = "share name with no filter")]
    public async Task ServeAsync_InvalidFilter_AnswersTopicFilterInvalidForThatFilterOnly(string filter)
    {
        var subscriber = await ConnectClientAsync(Broker(), "s");

        subscriber.Send(Subscribe(1, (filter, 0), ("t", 1)));

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x05, 0x00, 0x01, 0x00, 0x8F, 0x01 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_FilterRefusedByTopicAccess_AnswersNotAuthorized()
    {
        var bus = new Surl.MessageBus.MessageBus(
            TimeProvider.System,
            configuration: MessageBusConfiguration.None with { AccessGrants = [new TopicAccessGrant("open/#", "alice", TopicAccessRights.Subscribe)] });
        var broker = Broker(new Mqtt5BrokerState(bus), new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted));
        var alice = await Mqtt5TestClient.ConnectAsync(broker, Context(new ManualTimeProvider(), TestContext.CancellationToken), ConnectAsAlice);

        alice.Send(Subscribe(1, ("closed/x", 1), ("open/x", 1)));

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x05, 0x00, 0x01, 0x00, 0x87, 0x01 }, await alice.ReadPacketAsync());
        Assert.IsEmpty(await alice.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeAndUnsubscribeReusingAnIdentifierAwaitingPubrel_AnswerPacketIdentifierInUse()
    {
        var client = await ConnectClientAsync(Broker(), "c");
        client.Send(Publish("t", "hi", 2, 9));
        CollectionAssert.AreEqual(new byte[] { 0x50, 0x03, 0x00, 0x09, 0x10 }, await client.ReadPacketAsync());

        client.Send(Subscribe(9, ("t", 0), ("u", 0)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x05, 0x00, 0x09, 0x00, 0x91, 0x91 }, await client.ReadPacketAsync());
        client.Send(Unsubscribe(9, "t"));
        CollectionAssert.AreEqual(new byte[] { 0xB0, 0x04, 0x00, 0x09, 0x00, 0x91 }, await client.ReadPacketAsync());

        Assert.IsEmpty(await client.DisconnectAsync());
    }

    [TestMethod]
    [DataRow("#", new[] { "$SYS/x", "a/b" }, "a/b", DisplayName = "# matches every level but not a $ topic")]
    [DataRow("+/x", new[] { "$SYS/x", "a/b/x", "a/x" }, "a/x", DisplayName = "+ matches one level but not a $ topic")]
    [DataRow("a/#", new[] { "b", "a" }, "a", DisplayName = "a/# matches its parent a")]
    [DataRow("$SYS/#", new[] { "a", "$SYS/x" }, "$SYS/x", DisplayName = "a filter starting with $ matches a $ topic")]
    public async Task ServeAsync_WildcardFilter_ReceivesTheFirstTopicItMatches(string filter, string[] topics, string expectedTopic)
    {
        var state = new Mqtt5BrokerState(new Surl.MessageBus.MessageBus(TimeProvider.System));
        var subscriber = await ConnectClientAsync(Broker(state), "s");
        await SubscribeAsync(subscriber, filter, 0);

        foreach (var topic in topics)
        {
            state.Bus.Publish(new BusMessage(topic, "hi"u8.ToArray()));
        }

        Assert.AreEqual(expectedTopic, ((Mqtt5PublishPacket)await subscriber.ReadDecodedPacketAsync()).TopicName);
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_NoLocal_KeepsTheClientsOwnMessageFromThatSubscriptionButNotAnothersMessage()
    {
        var broker = Broker();
        var client = await ConnectClientAsync(broker, "c");
        client.Send(Subscribe(1, ("t", NoLocal), ("u", 0)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x05, 0x00, 0x01, 0x00, 0x00, 0x00 }, await client.ReadPacketAsync());
        var other = await ConnectClientAsync(broker, "o");

        client.Send(Publish("t", "hi", 0, 0), Publish("u", "hi", 0, 0));
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x06, 0x00, 0x01, 0x75, 0x00, 0x68, 0x69 }, await client.ReadPacketAsync());
        other.Send(PublishHiAtQos0);

        CollectionAssert.AreEqual(PublishHiAtQos0, await client.ReadPacketAsync());
        Assert.IsEmpty(await other.DisconnectAsync());
        Assert.IsEmpty(await client.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_NoLocalOnASharedSubscription_DisconnectsWithProtocolError()
    {
        var client = await ConnectClientAsync(Broker(), "c");

        client.Send(Subscribe(1, ("t", 0), ("$share/g/t", NoLocal)));

        CollectionAssert.AreEqual(Disconnect(0x82), await client.ServingEndedAsync());
    }

    [TestMethod]
    [DataRow(RetainAsPublished, (byte)0x31, DisplayName = "Retain As Published keeps RETAIN")]
    [DataRow((byte)0, (byte)0x30, DisplayName = "otherwise RETAIN goes as 0")]
    public async Task ServeAsync_LiveRetainedMessage_GoesWithRetainAsTheOptionSays(byte options, byte firstByte)
    {
        var state = new Mqtt5BrokerState(new Surl.MessageBus.MessageBus(TimeProvider.System));
        var subscriber = await ConnectClientAsync(Broker(state), "s");
        subscriber.Send(Subscribe(1, ("t", options)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await subscriber.ReadPacketAsync());

        state.Bus.Publish(new BusMessage("t", "hi"u8.ToArray()) { Retain = true });
        state.Bus.Publish(new BusMessage("t", "hi"u8.ToArray()));

        CollectionAssert.AreEqual(new byte[] { firstByte, 0x06, 0x00, 0x01, 0x74, 0x00, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
        CollectionAssert.AreEqual(PublishHiAtQos0, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    [DataRow((byte)0x00, 2, DisplayName = "Retain Handling 0 sends them at every subscribe")]
    [DataRow((byte)0x10, 1, DisplayName = "Retain Handling 1 sends them only for a new subscription")]
    [DataRow((byte)0x20, 0, DisplayName = "Retain Handling 2 never sends them")]
    public async Task ServeAsync_RetainHandling_SendsRetainedMessagesAsSpecified(byte options, int expectedRetainedCount)
    {
        var state = new Mqtt5BrokerState(new Surl.MessageBus.MessageBus(TimeProvider.System));
        state.Bus.Publish(new BusMessage("t", "hi"u8.ToArray()) { Retain = true });
        var subscriber = await ConnectClientAsync(Broker(state), "s");

        subscriber.Send(Subscribe(1, ("t", options)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await subscriber.ReadPacketAsync());
        var retainedCount = 0;
        if (expectedRetainedCount > 0)
        {
            CollectionAssert.AreEqual(new byte[] { 0x31, 0x06, 0x00, 0x01, 0x74, 0x00, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
            retainedCount++;
        }

        subscriber.Send(Subscribe(2, ("t", options)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x02, 0x00, 0x00 }, await subscriber.ReadPacketAsync());
        state.Bus.Publish(new BusMessage("t", "hi"u8.ToArray()));
        while ((await subscriber.ReadPacketAsync()) is [0x31, ..])
        {
            retainedCount++;
        }

        Assert.AreEqual(expectedRetainedCount, retainedCount);
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_SubscriptionIdentifier_IsCarriedOnDeliveryAndOnARetainedMessage()
    {
        var state = new Mqtt5BrokerState(new Surl.MessageBus.MessageBus(TimeProvider.System));
        state.Bus.Publish(new BusMessage("r", "hi"u8.ToArray()) { Retain = true });
        var subscriber = await ConnectClientAsync(Broker(state), "s");

        subscriber.Send(Subscribe(1, 5, ("t", 0), ("r", 0)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x05, 0x00, 0x01, 0x00, 0x00, 0x00 }, await subscriber.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0x31, 0x08, 0x00, 0x01, 0x72, 0x02, 0x0B, 0x05, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
        state.Bus.Publish(new BusMessage("t", "hi"u8.ToArray()));

        CollectionAssert.AreEqual(new byte[] { 0x30, 0x08, 0x00, 0x01, 0x74, 0x02, 0x0B, 0x05, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_MessageMatchingOverlappingSubscriptions_IsSentOnceAtTheHighestQualityOfServiceWithEveryIdentifier()
    {
        var state = new Mqtt5BrokerState(new Surl.MessageBus.MessageBus(TimeProvider.System));
        var subscriber = await ConnectClientAsync(Broker(state), "s");
        subscriber.Send(Subscribe(1, 1, ("t", 0)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await subscriber.ReadPacketAsync());
        subscriber.Send(Subscribe(2, 2, ("#", 1)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x02, 0x00, 0x01 }, await subscriber.ReadPacketAsync());
        await SubscribeAsync(subscriber, "u", 0);

        state.Bus.Publish(new BusMessage("t", "hi"u8.ToArray()) { QualityOfService = BusQualityOfService.AtLeastOnce });
        state.Bus.Publish(new BusMessage("u", "hi"u8.ToArray()));

        CollectionAssert.AreEqual(
            new byte[] { 0x32, 0x0C, 0x00, 0x01, 0x74, 0x00, 0x01, 0x04, 0x0B, 0x01, 0x0B, 0x02, 0x68, 0x69 },
            await subscriber.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x08, 0x00, 0x01, 0x75, 0x02, 0x0B, 0x02, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
        subscriber.Send([0x40, 0x02, 0x00, 0x01]);
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_SubscriptionIdentifier0_DisconnectsWithProtocolError()
    {
        var client = await ConnectClientAsync(Broker(), "c");

        client.Send(Subscribe(1, 0, ("t", 0)));

        CollectionAssert.AreEqual(Disconnect(0x82), await client.ServingEndedAsync());
    }

    [TestMethod]
    public async Task ServeAsync_SharedSubscription_GivesEachOfTwoMessagesToOneOfTwoMembers()
    {
        var state = new Mqtt5BrokerState(new Surl.MessageBus.MessageBus(TimeProvider.System));
        var broker = Broker(state);
        var first = await ConnectClientAsync(broker, "s1");
        await SubscribeAsync(first, "$share/g/t", 0);
        var second = await ConnectClientAsync(broker, "s2");
        second.Send(Subscribe(1, 7, ("$share/g/t", 0)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await second.ReadPacketAsync());

        state.Bus.Publish(new BusMessage("t", "1"u8.ToArray()));
        state.Bus.Publish(new BusMessage("t", "2"u8.ToArray()));

        CollectionAssert.AreEqual(new byte[] { 0x30, 0x05, 0x00, 0x01, 0x74, 0x00, 0x31 }, await first.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x07, 0x00, 0x01, 0x74, 0x02, 0x0B, 0x07, 0x32 }, await second.ReadPacketAsync());
        Assert.IsEmpty(await first.DisconnectAsync());
        Assert.IsEmpty(await second.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_Unsubscribe_DeletesAnExistingSubscriptionAndAnswersEachFilter()
    {
        var state = new Mqtt5BrokerState(new Surl.MessageBus.MessageBus(TimeProvider.System));
        var subscriber = await ConnectClientAsync(Broker(state), "s");
        subscriber.Send(Subscribe(1, ("t", 0), ("u", 0)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x05, 0x00, 0x01, 0x00, 0x00, 0x00 }, await subscriber.ReadPacketAsync());

        subscriber.Send(Unsubscribe(2, "t", "x", "a/#/b"));
        CollectionAssert.AreEqual(new byte[] { 0xB0, 0x06, 0x00, 0x02, 0x00, 0x00, 0x11, 0x8F }, await subscriber.ReadPacketAsync());
        state.Bus.Publish(new BusMessage("t", "no"u8.ToArray()));
        state.Bus.Publish(new BusMessage("u", "hi"u8.ToArray()));

        CollectionAssert.AreEqual(new byte[] { 0x30, 0x06, 0x00, 0x01, 0x75, 0x00, 0x68, 0x69 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_UnknownFilter_UnsubscribeAnswersNoSubscriptionExisted()
    {
        var subscriber = await ConnectClientAsync(Broker(), "s");

        subscriber.Send(Unsubscribe(4, "t"));

        CollectionAssert.AreEqual(new byte[] { 0xB0, 0x04, 0x00, 0x04, 0x00, 0x11 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_IdenticalFilter_ReplacesTheSubscriptionKeepingNoDuplicate()
    {
        var subscriber = await ConnectClientAsync(Broker(new Mqtt5BrokerState(new Surl.MessageBus.MessageBus(TimeProvider.System, maxSubscriptionsPerConnection: 1))), "s");
        await SubscribeAsync(subscriber, "t", 0);

        subscriber.Send(Subscribe(2, ("t", 1)));
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x02, 0x00, 0x01 }, await subscriber.ReadPacketAsync());
        subscriber.Send(Unsubscribe(3, "t", "t"));

        CollectionAssert.AreEqual(new byte[] { 0xB0, 0x05, 0x00, 0x03, 0x00, 0x00, 0x11 }, await subscriber.ReadPacketAsync());
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }

    // SUBSCRIBE (3.8) with a Subscription Identifier property (0x0B, one-byte Variable Byte Integer).
    private static byte[] Subscribe(ushort packetIdentifier, byte subscriptionIdentifier, params (string Filter, byte Options)[] filters)
    {
        var body = new List<byte> { (byte)(packetIdentifier >> 8), (byte)packetIdentifier, 0x02, 0x0B, subscriptionIdentifier };
        foreach (var (filter, options) in filters)
        {
            var bytes = Encoding.UTF8.GetBytes(filter);
            body.AddRange([(byte)(bytes.Length >> 8), (byte)bytes.Length, .. bytes, options]);
        }

        return [0x82, (byte)body.Count, .. body];
    }

    // UNSUBSCRIBE (3.10): Packet Identifier, Property Length 0, then each filter.
    private static byte[] Unsubscribe(ushort packetIdentifier, params string[] filters)
    {
        var body = new List<byte> { (byte)(packetIdentifier >> 8), (byte)packetIdentifier, 0x00 };
        foreach (var filter in filters)
        {
            var bytes = Encoding.UTF8.GetBytes(filter);
            body.AddRange([(byte)(bytes.Length >> 8), (byte)bytes.Length, .. bytes]);
        }

        return [0xA2, (byte)body.Count, .. body];
    }
}
