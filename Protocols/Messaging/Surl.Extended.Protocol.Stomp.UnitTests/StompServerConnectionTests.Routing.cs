using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>
/// Routing through the shared bus (BL-532; ADR-0118 decisions 4 to 6): SEND and MESSAGE,
/// destinations, acknowledgement, redelivery, receipts and transactions, every client frame and
/// every expected answer written by hand from the STOMP 1.2 specification's "SEND", "SUBSCRIBE",
/// "ACK", "NACK", "BEGIN", "COMMIT", "ABORT", "MESSAGE" and "RECEIPT".
/// </summary>
public sealed partial class StompServerConnectionTests
{
    [TestMethod]
    public async Task Send_ToATopic_IsDeliveredAsMessageWithTheSpecificationsHeaders()
    {
        var bus = NewBus();
        var subscriber = await StompTestSession.StartAsync(bus);
        await subscriber.SendAndWaitForReceiptAsync("SUBSCRIBE\nid:0\ndestination:/topic/a/b\nreceipt:s\n\n\0", "s");
        var publisher = await StompTestSession.StartAsync(bus);

        await publisher.SendAndWaitForReceiptAsync(
            "SEND\ndestination:/topic/a/b\ncontent-type:text/plain\nx-trace:1\nreceipt:p\ncontent-length:5\n\nhel\0o\0", "p");
        await subscriber.WaitForAsync("hel\0o\0");

        Assert.AreEqual(
            "RECEIPT\nreceipt-id:s\n\n\0"
            + "MESSAGE\ndestination:/topic/a/b\nmessage-id:1\nsubscription:0\ncontent-type:text/plain\ncontent-length:5\nx-trace:1\n\nhel\0o\0",
            subscriber.Answers);
        Assert.AreEqual("RECEIPT\nreceipt-id:p\n\n\0", publisher.Answers);
        await subscriber.CloseAsync();
        await publisher.CloseAsync();
    }

    [TestMethod]
    public async Task Send_ToATopicNobodySubscribes_IsNotDeliveredToALaterSubscriber()
    {
        var bus = NewBus();
        var session = await StompTestSession.StartAsync(bus);

        await session.SendAndWaitForReceiptAsync("SEND\ndestination:/topic/t\n\nearly\0SUBSCRIBE\nid:0\ndestination:/topic/t\nreceipt:s\n\n\0", "s");
        await session.SendAndWaitForReceiptAsync("SEND\ndestination:/topic/t\n\nlate\0DISCONNECT\nreceipt:d\n\n\0", "d");
        await session.EndAsync();

        Assert.AreEqual(
            "RECEIPT\nreceipt-id:s\n\n\0MESSAGE\ndestination:/topic/t\nmessage-id:1\nsubscription:0\ncontent-length:4\n\nlate\0RECEIPT\nreceipt-id:d\n\n\0",
            session.Answers);
    }

    [TestMethod]
    public async Task Message_PublishedOnTheBusUnderAnMqttName_IsDeliveredOnTheMappedTopicDestination()
    {
        var bus = NewBus();
        var session = await StompTestSession.StartAsync(bus);
        await session.SendAndWaitForReceiptAsync("SUBSCRIBE\nid:s\ndestination:/topic/sensors/+\nreceipt:s\n\n\0", "s");

        var topic = MqttTopicNames.ToCanonicalTopic("sensors/t1").Name!;
        bus.Publish(new BusMessage(topic, "21"u8.ToArray()) { ContentType = "text/plain" });
        await session.WaitForAsync("21\0");

        Assert.EndsWith(
            "MESSAGE\ndestination:/topic/sensors/t1\nmessage-id:1\nsubscription:s\ncontent-type:text/plain\ncontent-length:2\n\n21\0",
            session.Answers);
        await session.CloseAsync();
    }

    [TestMethod]
    public async Task Message_OnATopicTheSchemesAliasHides_IsNotDeliveredAndTheAliasedTopicIs()
    {
        var configuration = new MessageBusConfiguration { TopicMaps = [new TopicMap("stomp", "/topic/alias", "mqtt", "real")] };
        var bus = new Surl.MessageBus.MessageBus(TimeProvider.System, configuration: configuration);
        var session = await StompTestSession.StartAsync(bus);
        await session.SendAndWaitForReceiptAsync("SUBSCRIBE\nid:s\ndestination:/topic/#\nreceipt:s\n\n\0", "s");

        bus.Publish(new BusMessage("alias", "hidden"u8.ToArray()));
        bus.Publish(new BusMessage("real", "shown"u8.ToArray()));
        await session.WaitForAsync("shown\0");

        Assert.AreEqual(
            "RECEIPT\nreceipt-id:s\n\n\0MESSAGE\ndestination:/topic/alias\nmessage-id:1\nsubscription:s\ncontent-length:5\n\nshown\0",
            session.Answers);
        await session.CloseAsync();
    }

    [TestMethod]
    public async Task Message_BusHeaders_AreWrittenEscapedButNeverStompsOwnOrOnesHoldingNul()
    {
        var bus = NewBus();
        var session = await StompTestSession.StartAsync(bus);
        await session.SendAndWaitForReceiptAsync("SUBSCRIBE\nid:s\ndestination:/topic/h\nreceipt:s\n\n\0", "s");

        bus.Publish(new BusMessage("h", "x"u8.ToArray()) { Headers = HeadersOfEveryKind() });
        await session.WaitForAsync("x\0");

        Assert.EndsWith(
            "MESSAGE\ndestination:/topic/h\nmessage-id:1\nsubscription:s\ncontent-length:1\nok:1\na\\cb:2\nc:x\\ny\\r\n\nx\0",
            session.Answers);
        await session.CloseAsync();
    }

    [TestMethod]
    public async Task Message_In10_LeavesOutHeadersItCannotWriteUnescaped()
    {
        var bus = NewBus();
        var session = await StompTestSession.StartAsync(bus, connect: "CONNECT\nlogin:alice\npasscode:secret\n\n\0");
        await session.SendAndWaitForReceiptAsync("SUBSCRIBE\ndestination:/topic/h\nreceipt:s\n\n\0", "s");

        bus.Publish(new BusMessage("h", "x"u8.ToArray()) { Headers = HeadersOfEveryKind() });
        await session.WaitForAsync("x\0");

        Assert.EndsWith("MESSAGE\ndestination:/topic/h\nmessage-id:1\nsubscription:/topic/h\ncontent-length:1\nok:1\n\nx\0", session.Answers);
        await session.CloseAsync();
    }

    [TestMethod]
    public async Task Send_ToAQueue_IsKeptUntilConsumedAndGoesToOneSubscriptionRoundRobin()
    {
        var bus = NewBus();
        var producer = await StompTestSession.StartAsync(bus);
        await producer.SendAndWaitForReceiptAsync(Sends("/queue/q", "m1", "m2") + "SEND\ndestination:/queue/q\nreceipt:p\n\nm3\0", "p");
        var consumer = await StompTestSession.StartAsync(bus);

        await consumer.SendAndWaitForReceiptAsync("SUBSCRIBE\nid:a\ndestination:/queue/q\nreceipt:a\n\n\0", "a");
        await consumer.WaitForAsync("m3\0");

        Assert.AreEqual(
            "RECEIPT\nreceipt-id:a\n\n\0"
            + "MESSAGE\ndestination:/queue/q\nmessage-id:1\nsubscription:a\ncontent-length:2\n\nm1\0"
            + "MESSAGE\ndestination:/queue/q\nmessage-id:2\nsubscription:a\ncontent-length:2\n\nm2\0"
            + "MESSAGE\ndestination:/queue/q\nmessage-id:3\nsubscription:a\ncontent-length:2\n\nm3\0",
            consumer.Answers);
        Assert.AreEqual(0, bus.Connect().InspectQueue("q").ReadyMessageCount);
        await consumer.CloseAsync();
        await producer.CloseAsync();
    }

    [TestMethod]
    public async Task Ack_UnderClient_AcknowledgesTheNamedMessageAndEveryEarlierOne()
    {
        var bus = NewBus();
        var consumer = await QueueConsumerAsync(bus, "client", "m1", "m2", "m3");

        await consumer.SendAndWaitForReceiptAsync("ACK\nid:2\nreceipt:k\n\n\0", "k");
        await consumer.CloseAsync();

        Assert.Contains("MESSAGE\ndestination:/queue/q\nmessage-id:1\nsubscription:s\nack:1\ncontent-length:2\n\nm1\0", consumer.Answers);
        CollectionAssert.AreEqual(new[] { "m3" }, await DrainQueueAsync(bus));
    }

    [TestMethod]
    public async Task Ack_UnderClientIndividual_AcknowledgesTheNamedMessageOnly()
    {
        var bus = NewBus();
        var consumer = await QueueConsumerAsync(bus, "client-individual", "m1", "m2", "m3");

        await consumer.SendAndWaitForReceiptAsync("ACK\nid:2\nreceipt:k\n\n\0", "k");
        await consumer.CloseAsync();

        CollectionAssert.AreEqual(new[] { "m1", "m3" }, await DrainQueueAsync(bus));
    }

    [TestMethod]
    public async Task Unacknowledged_AfterTheSubscriberDisconnects_IsRedeliveredWithANewMessageId()
    {
        var bus = NewBus();
        var consumer = await QueueConsumerAsync(bus, "client", "m1");
        await consumer.SendAndWaitForReceiptAsync("DISCONNECT\nreceipt:d\n\n\0", "d");
        await consumer.EndAsync();
        var next = await StompTestSession.StartAsync(bus);

        await next.SendAndWaitForReceiptAsync("SUBSCRIBE\nid:t\ndestination:/queue/q\nack:client\nreceipt:t\n\n\0", "t");
        await next.WaitForAsync("m1\0");

        Assert.EndsWith("MESSAGE\ndestination:/queue/q\nmessage-id:1\nsubscription:t\nack:1\ncontent-length:2\n\nm1\0", next.Answers);
        await next.CloseAsync();
    }

    [TestMethod]
    public async Task Unsubscribe_PutsUnacknowledgedQueueMessagesBackAndEndsDelivery()
    {
        var bus = NewBus();
        var consumer = await QueueConsumerAsync(bus, "client", "m1");

        await consumer.SendAndWaitForReceiptAsync("UNSUBSCRIBE\nid:s\nreceipt:u\n\n\0UNSUBSCRIBE\nid:unknown\nreceipt:v\n\n\0", "v");

        Assert.AreEqual(1, bus.Connect().InspectQueue("q").ReadyMessageCount);
        Assert.EndsWith("m1\0RECEIPT\nreceipt-id:u\n\n\0RECEIPT\nreceipt-id:v\n\n\0", consumer.Answers);
        await consumer.CloseAsync();
    }

    [TestMethod]
    public async Task Nack_OfAQueueMessage_PutsItBackForRedelivery()
    {
        var bus = NewBus();
        var consumer = await QueueConsumerAsync(bus, "client-individual", "m1");

        consumer.Send("NACK\nid:1\n\n\0");
        await consumer.WaitForAsync("message-id:2\nsubscription:s\nack:2\ncontent-length:2\n\nm1\0");
        await consumer.SendAndWaitForReceiptAsync("ACK\nid:2\nreceipt:k\n\n\0", "k");
        await consumer.CloseAsync();

        CollectionAssert.AreEqual(Array.Empty<string>(), await DrainQueueAsync(bus));
    }

    [TestMethod]
    public async Task Nack_UnderClient_IsCumulativeAndATopicMessageIsDropped()
    {
        var bus = NewBus();
        var session = await StompTestSession.StartAsync(bus);
        await session.SendAndWaitForReceiptAsync(
            "SUBSCRIBE\nid:u\ndestination:/topic/u\nack:client\n\n\0SUBSCRIBE\nid:s\ndestination:/topic/t\nack:client\nreceipt:s\n\n\0", "s");
        bus.Publish(new BusMessage("u", "u1"u8.ToArray()));
        bus.Publish(new BusMessage("t", "a"u8.ToArray()));
        bus.Publish(new BusMessage("t", "b"u8.ToArray()));
        await session.WaitForAsync("b\0");

        await session.SendAndWaitForReceiptAsync("NACK\nid:3\nreceipt:n\n\n\0ACK\nid:1\nreceipt:k\n\n\0", "k");
        await session.CloseAsync();

        Assert.EndsWith("b\0RECEIPT\nreceipt-id:n\n\n\0RECEIPT\nreceipt-id:k\n\n\0", session.Answers);
    }

    [TestMethod]
    [DataRow("99")]
    [DataRow("not-a-number")]
    public async Task Ack_NamingNoOutstandingMessage_IsIgnored(string id)
    {
        var bus = NewBus();
        var session = await StompTestSession.StartAsync(bus);

        await session.SendAndWaitForReceiptAsync($"ACK\nid:{id}\nreceipt:k\n\n\0", "k");
        await session.CloseAsync();

        Assert.AreEqual("RECEIPT\nreceipt-id:k\n\n\0", session.Answers);
    }

    [TestMethod]
    public async Task AckAndNack_In11_NameTheMessageByMessageId()
    {
        var bus = NewBus();
        var consumer = await QueueConsumerAsync(bus, "client-individual", ["m1", "m2"], "CONNECT\naccept-version:1.1\nlogin:alice\npasscode:secret\n\n\0");

        await consumer.SendAndWaitForReceiptAsync("ACK\nmessage-id:1\nsubscription:s\n\n\0NACK\nmessage-id:2\nsubscription:s\nreceipt:k\n\n\0", "k");
        await consumer.WaitForAsync("message-id:3\nsubscription:s\ncontent-length:2\n\nm2\0");
        await consumer.CloseAsync();

        Assert.DoesNotContain("ack:", consumer.Answers);
        CollectionAssert.AreEqual(new[] { "m2" }, await DrainQueueAsync(bus));
    }

    [TestMethod]
    public async Task Session10_SubscribesByDestinationAcksByMessageIdAndHasNoNack()
    {
        var bus = NewBus();
        var consumer = await QueueConsumerAsync(bus, "client", ["m1", "m2"], "CONNECT\nlogin:alice\npasscode:secret\n\n\0", withId: false);

        await consumer.SendAndWaitForReceiptAsync("ACK\nmessage-id:1\nreceipt:k\n\n\0UNSUBSCRIBE\ndestination:/queue/q\nreceipt:u\n\n\0", "u");
        consumer.Send("NACK\nmessage-id:2\n\n\0");
        await consumer.WaitForAsync("ERROR\nmessage:Unknown command\n\n\0");
        await consumer.CloseAsync();

        Assert.Contains("MESSAGE\ndestination:/queue/q\nmessage-id:1\nsubscription:/queue/q\ncontent-length:2\n\nm1\0", consumer.Answers);
        CollectionAssert.AreEqual(new[] { "m2" }, await DrainQueueAsync(bus));
    }

    [TestMethod]
    public async Task Transaction_SendsAreDeliveredOnlyOnCommitAndDroppedOnAbort()
    {
        var bus = NewBus();
        var subscriber = await StompTestSession.StartAsync(bus);
        await subscriber.SendAndWaitForReceiptAsync("SUBSCRIBE\nid:s\ndestination:/topic/t\nreceipt:s\n\n\0", "s");
        var publisher = await StompTestSession.StartAsync(bus);

        await publisher.SendAndWaitForReceiptAsync("BEGIN\ntransaction:x\n\n\0SEND\ndestination:/topic/t\ntransaction:x\nreceipt:h\n\nheld\0", "h");
        var beforeCommit = subscriber.Answers;
        await publisher.SendAndWaitForReceiptAsync("COMMIT\ntransaction:x\nreceipt:c\n\n\0", "c");
        await subscriber.WaitForAsync("held\0");
        await publisher.SendAndWaitForReceiptAsync(
            "BEGIN\ntransaction:y\n\n\0SEND\ndestination:/topic/t\ntransaction:y\n\ndropped\0ABORT\ntransaction:y\n\n\0SEND\ndestination:/topic/t\nreceipt:z\n\nafter\0", "z");
        await subscriber.WaitForAsync("after\0");

        Assert.AreEqual("RECEIPT\nreceipt-id:s\n\n\0", beforeCommit);
        Assert.DoesNotContain("dropped", subscriber.Answers);
        Assert.EndsWith("content-length:4\n\nheld\0MESSAGE\ndestination:/topic/t\nmessage-id:2\nsubscription:s\ncontent-length:5\n\nafter\0", subscriber.Answers);
        await subscriber.CloseAsync();
        await publisher.CloseAsync();
    }

    [TestMethod]
    public async Task Transaction_AcksTakeEffectOnCommitAndAreDiscardedOnAbort()
    {
        var bus = NewBus();
        var consumer = await QueueConsumerAsync(bus, "client-individual", "m1", "m2");

        await consumer.SendAndWaitForReceiptAsync(
            "BEGIN\ntransaction:x\n\n\0ACK\nid:1\ntransaction:x\n\n\0NACK\nid:2\ntransaction:x\n\n\0ABORT\ntransaction:x\n\n\0"
            + "BEGIN\ntransaction:y\n\n\0ACK\nid:1\ntransaction:y\n\n\0COMMIT\ntransaction:y\nreceipt:c\n\n\0",
            "c");
        await consumer.CloseAsync();

        CollectionAssert.AreEqual(new[] { "m2" }, await DrainQueueAsync(bus));
    }

    [TestMethod]
    public async Task Transaction_StillOpenWhenTheConnectionEnds_IsAborted()
    {
        var bus = NewBus();
        var session = await StompTestSession.StartAsync(bus);

        await session.SendAndWaitForReceiptAsync("BEGIN\ntransaction:x\n\n\0SEND\ndestination:/queue/q\ntransaction:x\nreceipt:h\n\nheld\0", "h");
        await session.CloseAsync();

        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.Connect().InspectQueue("q").Outcome);
    }

    [TestMethod]
    public async Task Transaction_WithoutAnUploadLimit_HoldsAnyBody()
    {
        var bus = NewBus();
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, 1048576, maxUploadBytes: 0);
        var session = await StompTestSession.StartAsync(bus, limits: limits);

        await session.SendAndWaitForReceiptAsync("BEGIN\ntransaction:x\n\n\0SEND\ndestination:/queue/q\ntransaction:x\n\nheld\0COMMIT\ntransaction:x\nreceipt:c\n\n\0", "c");
        await session.CloseAsync();

        Assert.AreEqual(1, bus.Connect().InspectQueue("q").ReadyMessageCount);
    }

    private static List<BusMessageHeader> HeadersOfEveryKind() =>
    [
        new("ok", "1"),
        new("message-id", "forged"),
        new("content-length", "9"),
        new("a:b", "2"),
        new("c", "x\ny\r"),
        new("n\0", "z"),
        new("v", "\0"),
    ];

    private static string Sends(string destination, params string[] bodies) =>
        string.Concat(bodies.Select(body => $"SEND\ndestination:{destination}\n\n{body}\0"));

    private static Task<StompTestSession> QueueConsumerAsync(Surl.MessageBus.MessageBus bus, string ackMode, params string[] bodies) =>
        QueueConsumerAsync(bus, ackMode, bodies, StompTestSession.AliceConnect12);

    // Sends the bodies to /queue/q, then subscribes a new session to it as s and waits for the last one.
    private static async Task<StompTestSession> QueueConsumerAsync(
        Surl.MessageBus.MessageBus bus, string ackMode, string[] bodies, string connect, bool withId = true)
    {
        var producer = await StompTestSession.StartAsync(bus);
        await producer.SendAndWaitForReceiptAsync(Sends("/queue/q", bodies) + "DISCONNECT\nreceipt:p\n\n\0", "p");
        await producer.EndAsync();
        var consumer = await StompTestSession.StartAsync(bus, connect: connect);
        var id = withId ? "id:s\n" : string.Empty;
        await consumer.SendAndWaitForReceiptAsync($"SUBSCRIBE\n{id}destination:/queue/q\nack:{ackMode}\nreceipt:s\n\n\0", "s");
        await consumer.WaitForAsync($"{bodies[^1]}\0");
        return consumer;
    }

    // Takes what /queue/q still holds, in order, through a STOMP subscriber of its own.
    private static async Task<string[]> DrainQueueAsync(Surl.MessageBus.MessageBus bus)
    {
        var ready = bus.Connect().InspectQueue("q").ReadyMessageCount;
        var session = await StompTestSession.StartAsync(bus);
        await session.SendAndWaitForReceiptAsync("SUBSCRIBE\nid:drain\ndestination:/queue/q\nreceipt:drain\n\n\0", "drain");
        await session.WaitUntilAsync(() => session.Answers.Split("MESSAGE\n").Length - 1 == ready);
        await session.CloseAsync();
        return [.. session.Answers.Split("MESSAGE\n").Skip(1).Select(message => message[(message.IndexOf("\n\n", StringComparison.Ordinal) + 2)..^1])];
    }
}
