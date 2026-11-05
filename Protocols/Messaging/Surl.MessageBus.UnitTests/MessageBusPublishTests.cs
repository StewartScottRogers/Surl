namespace Surl.MessageBus;

[TestClass]
public sealed class MessageBusPublishTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void Publish_MatchingSubscriber_ReceivesPayloadBytesExactAndProperties()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("sensors/+/temperature", received.Add);
        byte[] payload = [0x00, 0xFF, 0x80, 0x0A, 0x0D];
        byte[] correlation = [1, 2, 3];
        byte[] key = [9];
        var expiresAt = clock.UtcNow.AddMinutes(5);
        var message = new BusMessage("sensors/kitchen/temperature", payload)
        {
            QualityOfService = BusQualityOfService.ExactlyOnce,
            ExpiresAt = expiresAt,
            ContentType = "application/octet-stream",
            ResponseTopic = "replies/kitchen",
            CorrelationData = correlation,
            Key = key,
            Headers = [new("unit", "C"), new("unit", "K")],
            Utf8Payload = true,
            PublishedAt = DateTimeOffset.UnixEpoch,
        };

        var result = bus.Publish(message);
        payload[0] = 0x42;
        correlation[0] = 0x42;
        key[0] = 0x42;

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, 1), result);
        Assert.HasCount(1, received);
        var delivered = received[0];
        Assert.AreEqual("sensors/kitchen/temperature", delivered.Topic);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0xFF, 0x80, 0x0A, 0x0D }, delivered.Payload.ToArray());
        Assert.AreEqual(BusQualityOfService.ExactlyOnce, delivered.QualityOfService);
        Assert.AreEqual(expiresAt, delivered.ExpiresAt);
        Assert.AreEqual("application/octet-stream", delivered.ContentType);
        Assert.AreEqual("replies/kitchen", delivered.ResponseTopic);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, delivered.CorrelationData!.Value.ToArray());
        CollectionAssert.AreEqual(new byte[] { 9 }, delivered.Key!.Value.ToArray());
        CollectionAssert.AreEqual(new BusMessageHeader[] { new("unit", "C"), new("unit", "K") }, delivered.Headers.ToArray());
        Assert.IsTrue(delivered.Utf8Payload);
        Assert.IsFalse(delivered.Retain);
        Assert.AreEqual(clock.UtcNow, delivered.PublishedAt);
    }

    [TestMethod]
    public void Publish_MessageWithoutOptionalProperties_DeliversThemAbsent()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("t", received.Add);

        bus.Publish(new BusMessage("t", "hi"u8.ToArray()));

        var delivered = received.Single();
        Assert.IsNull(delivered.CorrelationData);
        Assert.IsNull(delivered.Key);
        Assert.IsNull(delivered.ExpiresAt);
        Assert.IsNull(delivered.ContentType);
        Assert.IsEmpty(delivered.Headers);
        Assert.AreEqual(BusQualityOfService.AtMostOnce, delivered.QualityOfService);
    }

    [TestMethod]
    public void Publish_NonMatchingSubscriber_ReceivesNothing()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("a/b", received.Add);

        var result = bus.Publish(new BusMessage("a/c", "x"u8.ToArray()));

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, 0), result);
        Assert.IsEmpty(received);
    }

    [TestMethod]
    public void Publish_SeveralMatchingSubscriptions_DeliversOncePerSubscription()
    {
        var bus = new MessageBus(clock);
        using var first = bus.Connect();
        using var second = bus.Connect();
        var firstReceived = new List<BusMessage>();
        var secondReceived = new List<BusMessage>();
        first.Subscribe("a/+", firstReceived.Add);
        first.Subscribe("a/#", firstReceived.Add);
        second.Subscribe("#", secondReceived.Add);

        var result = bus.Publish(new BusMessage("a/b", "x"u8.ToArray()));

        Assert.AreEqual(3, result.DeliveredSubscriptionCount);
        Assert.HasCount(2, firstReceived);
        Assert.HasCount(1, secondReceived);
    }

    [TestMethod]
    public void Publish_ManyMessages_ReachOneSubscriberInPublishOrder()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<string>();
        connection.Subscribe("#", message => received.Add(message.Topic));
        var topics = Enumerable.Range(0, 50).Select(index => $"t/{index}").ToList();

        foreach (var topic in topics)
        {
            bus.Publish(new BusMessage(topic, "x"u8.ToArray()));
        }

        CollectionAssert.AreEqual(topics, received);
    }

    [TestMethod]
    public void Publish_FromInsideADelivery_IsDeliveredAfterTheMessageBeingDelivered()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<string>();
        connection.Subscribe("#", message =>
        {
            received.Add("first:" + message.Topic);
            if (message.Topic == "ping")
            {
                bus.Publish(new BusMessage("pong", "x"u8.ToArray()));
            }
        });
        connection.Subscribe("#", message => received.Add("second:" + message.Topic));

        var result = bus.Publish(new BusMessage("ping", "x"u8.ToArray()));

        Assert.AreEqual(2, result.DeliveredSubscriptionCount);
        CollectionAssert.AreEqual(new[] { "first:ping", "second:ping", "first:pong", "second:pong" }, received);
    }

    [TestMethod]
    public void Publish_ConcurrentPublishers_EachSubscriberSeesEveryMessageOnceInEachPublishersOrder()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<string>();
        connection.Subscribe("#", message => received.Add(message.Topic));

        Parallel.For(0, 4, publisher =>
        {
            for (var index = 0; index < 250; index++)
            {
                bus.Publish(new BusMessage($"p{publisher}/{index}", "x"u8.ToArray()));
            }
        });

        Assert.HasCount(1000, received);
        for (var publisher = 0; publisher < 4; publisher++)
        {
            var prefix = $"p{publisher}/";
            var own = received.Where(topic => topic.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(0, 250).Select(index => prefix + index).ToList(), own);
        }
    }

    [TestMethod]
    public void Publish_DeliveryThrows_ExceptionReachesThePublisherAndTheBusDeliversLaterMessages()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<string>();
        connection.Subscribe("#", message =>
        {
            if (message.Topic == "bad")
            {
                throw new InvalidOperationException("subscriber failed");
            }

            received.Add(message.Topic);
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => bus.Publish(new BusMessage("bad", "x"u8.ToArray())));
        bus.Publish(new BusMessage("good", "x"u8.ToArray()));

        CollectionAssert.AreEqual(new[] { "good" }, received);
    }

    [TestMethod]
    public void Publish_ExpiredMessage_IsNeitherDeliveredNorRetained()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("t", received.Add);

        var result = bus.Publish(new BusMessage("t", "x"u8.ToArray()) { Retain = true, ExpiresAt = clock.UtcNow });

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, 0), result);
        Assert.IsEmpty(received);
        Assert.IsEmpty(connection.Subscribe("t", _ => { }, includeRetained: true).RetainedMessages);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("a/+")]
    [DataRow("a/#")]
    public void Publish_TopicNotCanonical_ReturnsInvalidTopicAndDeliversNothing(string topic)
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("#", received.Add);

        var result = bus.Publish(new BusMessage(topic, "x"u8.ToArray()));

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.InvalidTopic, 0), result);
        Assert.IsEmpty(received);
    }

    [TestMethod]
    public void Publish_NullMessage_Throws()
    {
        var bus = new MessageBus(clock);

        Assert.ThrowsExactly<ArgumentNullException>(() => bus.Publish(null!));
    }

    [TestMethod]
    public void Constructor_Defaults_AreTheAdrsBounds()
    {
        var bus = new MessageBus(clock);

        Assert.AreEqual(10_000, bus.MaxTopics);
        Assert.AreEqual(104_857_600, bus.MaxRetainedPayloadBytes);
        Assert.AreEqual(1_000, bus.MaxSubscriptionsPerConnection);
        Assert.AreEqual(104_857_600, bus.MaxMessageBytes);
    }

    [TestMethod]
    public void Constructor_NullTimeProvider_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MessageBus(null!));
    }

    [TestMethod]
    [DataRow(0, 1L, 1, 0L)]
    [DataRow(1, 0L, 1, 0L)]
    [DataRow(1, 1L, 0, 0L)]
    [DataRow(1, 1L, 1, -1L)]
    public void Constructor_BoundBelowItsLeastValue_Throws(int maxTopics, long maxRetainedPayloadBytes, int maxSubscriptions, long maxMessageBytes)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new MessageBus(clock, maxTopics, maxRetainedPayloadBytes, maxSubscriptions, maxMessageBytes));
    }
}
