using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// <see cref="MqttProtocolServer"/> given a hold count (ADR-0014 Amendment 1): it keeps a
/// <c>SUBSCRIBE</c> open, sends later publishes and ends it at the count. Without one, every
/// other test class shows it answers as before.
/// </summary>
[TestClass]
public sealed class MqttHeldSubscriptionServingTests
{
    private static readonly byte[] ConnackAccepted = [0x20, 0x02, 0x00, 0x00];
    private static readonly byte[] SubackGranted = [0x90, 0x03, 0x00, 0x01, 0x00];
    private static readonly byte[] SubackRefused = [0x90, 0x03, 0x00, 0x01, 0x80];
    private static readonly byte[] PingResponse = [0xD0, 0x00];
    private static readonly byte[] Disconnect = [0xE0, 0x00];
    private const string HeldNote = "SUBSCRIBE: 0 retained messages delivered; the subscription is held open.";
    private const string CountNote = "The held subscription reached its publish count; sent DISCONNECT and closed.";
    private const string OverflowNote = "More than 1000 publishes or 16777216 payload bytes waited for the held subscription; closed with no reply.";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NegativeHoldCount_Throws()
    {
        var thrown = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new MqttProtocolServer(TestMessageBus.New(), new AnonymousAuthenticationPolicy(), -1));

        Assert.AreEqual("holdSubscriptionCount", thrown.ParamName);
    }

    [TestMethod]
    public async Task ServeAsync_APublishWhileTheSubAckIsBeingWritten_ReachesTheSubscriberAfterIt()
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 1);
        var deliveredSubscriptions = -1;
        exchange.Connection.Writing = packet =>
        {
            if (packet[0] == 0x90)
            {
                deliveredSubscriptions = Publish(bus, "t", "hi").DeliveredSubscriptionCount;
            }
        };

        exchange.Connection.Send(ClientPackets.Subscribe(1, ("t", 0)));
        await FinishAsync(exchange);

        Assert.AreEqual(1, deliveredSubscriptions);
        AssertPackets(exchange.Connection.UnreadServerPackets(), ConnackAccepted, SubackGranted, Live("t", "hi"), Disconnect);
        CollectionAssert.AreEqual(new[] { HeldNote, CountNote }, exchange.Log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ALivePublishMatchingOverlappingFilters_IsSentOnceWithFixedHeader30()
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 0);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("a/b", 0), ("a/+", 0), ("#", 0)), [0x90, 0x05, 0x00, 0x01, 0x00, 0x00, 0x00]);

        bus.Publish(new BusMessage("a/b", "hi"u8.ToArray()) { Retain = true, QualityOfService = BusQualityOfService.ExactlyOnce });
        Publish(bus, "a/c", "yo");

        await AssertNextPacketAsync(exchange, [0x30, 0x07, 0x00, 0x03, (byte)'a', (byte)'/', (byte)'b', (byte)'h', (byte)'i']);
        await AssertNextPacketAsync(exchange, Live("a/c", "yo"));
        exchange.Connection.CloseFromClient();
        await FinishAsync(exchange);
        Assert.IsEmpty(exchange.Connection.UnreadServerPackets());
    }

    [TestMethod]
    public async Task ServeAsync_ARetainedPublishBetweenTwoFiltersSubscriptions_IsSentAndCountedOnce()
    {
        // The bus reads its clock once for each filter it subscribes: the second read is between
        // the first filter's subscription and the second's retained messages.
        var busClock = new HookedTimeProvider();
        var bus = new MessageBus.MessageBus(busClock);
        using var publisher = new PublishFromAnotherThread(
            () => bus.Publish(new BusMessage("a/b", "hi"u8.ToArray()) { Retain = true }));
        busClock.RunAtRead(2, publisher.StartAndWaitUntilItEndsOrIsBlocked);
        var exchange = Start(bus, holdCount: 2);

        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("a/#", 0), ("a/b", 0)), [0x90, 0x04, 0x00, 0x01, 0x00, 0x00]);
        publisher.WaitUntilItEnds();
        Publish(bus, "a/c", "last");

        await FinishAsync(exchange);
        AssertPackets(exchange.Connection.UnreadServerPackets(), Live("a/b", "hi"), Live("a/c", "last"), Disconnect);
    }

    [TestMethod]
    public async Task ServeAsync_AFilterTwiceInOneSubscribe_StaysOneSubscriptionAndIsGrantedTwice()
    {
        var bus = TestMessageBus.New();
        bus.Retain("t", "kept"u8);
        var exchange = Start(bus, holdCount: 0);

        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0), ("t", 0)), [0x90, 0x04, 0x00, 0x01, 0x00, 0x00]);
        await AssertNextPacketAsync(exchange, Retained("t", "kept"));
        var published = Publish(bus, "t", "hi");

        Assert.AreEqual(1, published.DeliveredSubscriptionCount);
        await AssertNextPacketAsync(exchange, Live("t", "hi"));
        exchange.Connection.CloseFromClient();
        await FinishAsync(exchange);
        Assert.IsEmpty(exchange.Connection.UnreadServerPackets());
    }

    [TestMethod]
    public async Task ServeAsync_ALivePublishOnAMappedTopic_IsSentUnderTheSchemesNameAndAHiddenOneIsNeitherSentNorCounted()
    {
        var bus = new MessageBus.MessageBus(TimeProvider.System, configuration: new MessageBusConfiguration
        {
            TopicMaps = [new TopicMap("mqtt", "orders/new", "nats", "shop.orders")],
        });
        var exchange = Start(bus, holdCount: 1);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("#", 0)), SubackGranted);

        Publish(bus, "orders/new", "hidden");
        Publish(bus, "shop/orders", "hi");

        await FinishAsync(exchange);
        AssertPackets(exchange.Connection.UnreadServerPackets(), Live("orders/new", "hi"), Disconnect);
    }

    [TestMethod]
    public async Task ServeAsync_ALivePublishThatExpiresWhileItWaits_IsNeitherSentNorCounted()
    {
        var clock = new ManualTimeProvider();
        var bus = new MessageBus.MessageBus(clock);
        var exchange = Start(bus, holdCount: 1, clock: clock);
        exchange.Connection.Writing = packet =>
        {
            if (packet[0] == 0x90)
            {
                bus.Publish(new BusMessage("t", "old"u8.ToArray()) { ExpiresAt = clock.GetUtcNow().AddSeconds(1) });
                bus.Publish(new BusMessage("t", "new"u8.ToArray()) { ExpiresAt = clock.GetUtcNow().AddSeconds(2) });
                clock.Advance(TimeSpan.FromSeconds(1));
            }
        };

        exchange.Connection.Send(ClientPackets.Subscribe(1, ("t", 0)));
        await FinishAsync(exchange);

        AssertPackets(exchange.Connection.UnreadServerPackets(), ConnackAccepted, SubackGranted, Live("t", "new"), Disconnect);
    }

    [TestMethod]
    public async Task ServeAsync_RetainedAndLivePublishesReachingTheCount_AreFollowedByDisconnect()
    {
        var bus = TestMessageBus.New();
        bus.Retain("t", "kept"u8);
        var exchange = Start(bus, holdCount: 3);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0)), SubackGranted);
        await AssertNextPacketAsync(exchange, Retained("t", "kept"));

        Publish(bus, "t", "1");
        Publish(bus, "t", "2");
        Publish(bus, "t", "3");

        await FinishAsync(exchange);
        AssertPackets(exchange.Connection.UnreadServerPackets(), Live("t", "1"), Live("t", "2"), Disconnect);
        Assert.AreEqual(0, Publish(bus, "t", "4").DeliveredSubscriptionCount);
    }

    [TestMethod]
    public async Task ServeAsync_MoreRetainedMessagesThanTheCount_SendsTheCountThenDisconnect()
    {
        var bus = TestMessageBus.New();
        bus.Retain("a", "1"u8);
        bus.Retain("b", "2"u8);
        var exchange = Start(bus, holdCount: 1);

        exchange.Connection.Send(ClientPackets.Subscribe(1, ("#", 0)));
        await FinishAsync(exchange);

        AssertPackets(exchange.Connection.UnreadServerPackets(), ConnackAccepted, SubackGranted, Retained("a", "1"), Disconnect);
        CollectionAssert.AreEqual(new[] { CountNote }, exchange.Log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_CountZero_NeverSendsDisconnect()
    {
        var bus = TestMessageBus.New();
        bus.Retain("t", "kept"u8);
        var exchange = Start(bus, holdCount: 0);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0)), SubackGranted);
        await AssertNextPacketAsync(exchange, Retained("t", "kept"));

        foreach (var payload in new[] { "1", "2", "3" })
        {
            Publish(bus, "t", payload);
            await AssertNextPacketAsync(exchange, Live("t", payload));
        }

        exchange.Connection.CloseFromClient();
        await FinishAsync(exchange);
        Assert.IsEmpty(exchange.Connection.UnreadServerPackets());
        Assert.AreEqual(0, Publish(bus, "t", "4").DeliveredSubscriptionCount);
    }

    [TestMethod]
    public async Task ServeAsync_PingRequestWhileHeld_IsAnsweredAndTheSubscriptionGoesOn()
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 1);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0)), SubackGranted);

        exchange.Connection.Send(ClientPackets.PingRequest);
        await AssertNextPacketAsync(exchange, PingResponse);
        Publish(bus, "t", "hi");

        await FinishAsync(exchange);
        AssertPackets(exchange.Connection.UnreadServerPackets(), Live("t", "hi"), Disconnect);
    }

    [TestMethod]
    public async Task ServeAsync_ASecondSubscribe_AddsItsFiltersAndCountsItsRetainedMessagesAgainstTheSameCount()
    {
        var bus = TestMessageBus.New();
        bus.Retain("a", "1"u8);
        bus.Retain("b", "2"u8);
        var exchange = Start(bus, holdCount: 3);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("a", 0)), SubackGranted);
        await AssertNextPacketAsync(exchange, Retained("a", "1"));

        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("b", 0)), SubackGranted);
        await AssertNextPacketAsync(exchange, Retained("b", "2"));
        Publish(bus, "a", "live");

        await FinishAsync(exchange);
        AssertPackets(exchange.Connection.UnreadServerPackets(), Live("a", "live"), Disconnect);
    }

    [TestMethod]
    public async Task ServeAsync_TheSameFilterAgain_StaysOneSubscriptionAndSendsItsRetainedMessageAgain()
    {
        var bus = TestMessageBus.New();
        bus.Retain("t", "kept"u8);
        var exchange = Start(bus, holdCount: 0);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0)), SubackGranted);
        await AssertNextPacketAsync(exchange, Retained("t", "kept"));

        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0)), SubackGranted);
        await AssertNextPacketAsync(exchange, Retained("t", "kept"));
        var published = Publish(bus, "t", "hi");

        Assert.AreEqual(1, published.DeliveredSubscriptionCount);
        await AssertNextPacketAsync(exchange, Live("t", "hi"));
        exchange.Connection.CloseFromClient();
        await FinishAsync(exchange);
        Assert.IsEmpty(exchange.Connection.UnreadServerPackets());
    }

    [TestMethod]
    public async Task ServeAsync_Unsubscribe_RemovesTheFilterAndLeavesTheConnectionOpen()
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 0);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0), ("u", 0)), [0x90, 0x04, 0x00, 0x01, 0x00, 0x00]);

        exchange.Connection.Send(ClientPackets.Unsubscribe(2, "t", "never-subscribed"));
        await AssertNextPacketAsync(exchange, [0xB0, 0x02, 0x00, 0x02]);

        Assert.AreEqual(0, Publish(bus, "t", "no").DeliveredSubscriptionCount);
        Publish(bus, "u", "yes");
        await AssertNextPacketAsync(exchange, Live("u", "yes"));
        exchange.Connection.Send(ClientPackets.Unsubscribe(3, "u"));
        await AssertNextPacketAsync(exchange, [0xB0, 0x02, 0x00, 0x03]);
        exchange.Connection.Send(ClientPackets.PingRequest);
        await AssertNextPacketAsync(exchange, PingResponse);
        exchange.Connection.CloseFromClient();
        await FinishAsync(exchange);
    }

    [TestMethod]
    public async Task ServeAsync_ClientDisconnectWhileHeld_EndsTheSubscriptionWithNoReply()
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 0);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0)), SubackGranted);

        exchange.Connection.Send(ClientPackets.Disconnect);
        await FinishAsync(exchange);

        Assert.IsEmpty(exchange.Connection.UnreadServerPackets());
        Assert.AreEqual(0, Publish(bus, "t", "hi").DeliveredSubscriptionCount);
        CollectionAssert.AreEqual(new[] { HeldNote }, exchange.Log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_TheSubscribersOwnPublish_IsAcknowledgedThenSentBack()
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 1);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0)), SubackGranted);

        exchange.Connection.Send(ClientPackets.Publish(0x32, "t", 7, "me"));

        await FinishAsync(exchange);
        AssertPackets(exchange.Connection.UnreadServerPackets(), [0x40, 0x02, 0x00, 0x07], Live("t", "me"), Disconnect);
    }

    [TestMethod]
    public async Task ServeAsync_LongAfterTheKeepAlive_TheHeldSubscriptionStillDelivers()
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 1);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0)), SubackGranted);

        // curl's CONNECT announces a Keep Alive of 60 seconds.
        exchange.Clock.Advance(TimeSpan.FromHours(1));
        var timersWhileHeld = exchange.Clock.PendingTimerCount;
        Publish(bus, "t", "hi");

        await FinishAsync(exchange);
        Assert.AreEqual(0, timersWhileHeld);
        AssertPackets(exchange.Connection.UnreadServerPackets(), Live("t", "hi"), Disconnect);
    }

    [TestMethod]
    public async Task ServeAsync_EveryFilterOfTheFirstSubscribeRefused_SendsSubAckThenDisconnect()
    {
        var exchange = Start(TestMessageBus.New(), holdCount: 0);

        exchange.Connection.Send(ClientPackets.Subscribe(1, ("a/#/b", 0)));
        await FinishAsync(exchange);

        AssertPackets(exchange.Connection.UnreadServerPackets(), ConnackAccepted, SubackRefused, Disconnect);
        CollectionAssert.AreEqual(new[] { "SUBSCRIBE: every topic filter was refused; sent DISCONNECT and closed." }, exchange.Log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_EveryFilterOfALaterSubscribeRefused_StaysOpen()
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 1);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0)), SubackGranted);

        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("a/#/b", 0)), SubackRefused);
        Publish(bus, "t", "hi");

        await FinishAsync(exchange);
        AssertPackets(exchange.Connection.UnreadServerPackets(), Live("t", "hi"), Disconnect);
    }

    [TestMethod]
    public async Task ServeAsync_AFilterTheAccountMayNotSubscribeTo_IsRefusedAndNoted()
    {
        var bus = new MessageBus.MessageBus(TimeProvider.System, configuration: new MessageBusConfiguration
        {
            AccessGrants = [new TopicAccessGrant("t", "ann", TopicAccessRights.Subscribe)],
        });
        var exchange = Start(bus, holdCount: 0);

        exchange.Connection.Send(ClientPackets.Subscribe(1, ("t", 0)));
        await FinishAsync(exchange);

        AssertPackets(exchange.Connection.UnreadServerPackets(), ConnackAccepted, SubackRefused, Disconnect);
        Assert.AreEqual("SUBSCRIBE: 1 topic filters refused by --topic-access for an anonymous caller; answered SUBACK 0x80 for each.", exchange.Log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_AFilterPastTheBusSubscriptionsAConnection_IsRefusedAndNoted()
    {
        var bus = new MessageBus.MessageBus(TimeProvider.System, maxSubscriptionsPerConnection: 1);
        var exchange = Start(bus, holdCount: 0);

        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("a", 0), ("b", 0)), [0x90, 0x04, 0x00, 0x01, 0x00, 0x80]);
        exchange.Connection.CloseFromClient();
        await FinishAsync(exchange);

        CollectionAssert.AreEqual(
            new[] { "SUBSCRIBE: 1 topic filters past the bus's subscriptions a connection; answered SUBACK 0x80 for each.", HeldNote },
            exchange.Log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_AThousandPublishesWaiting_AreAllSent()
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 1000);
        PublishWhileTheSubAckIsWritten(exchange, bus, count: 1000, payloadBytes: 1);

        exchange.Connection.Send(ClientPackets.Subscribe(1, ("t", 0)));
        await FinishAsync(exchange);

        var packets = exchange.Connection.UnreadServerPackets();
        Assert.HasCount(1003, packets);
        CollectionAssert.AreEqual(Disconnect, packets[^1]);
    }

    [TestMethod]
    [DataRow(1001, 1, DisplayName = "1001 publishes")]
    [DataRow(5, 4 * 1024 * 1024, DisplayName = "20 MiB of payload")]
    public async Task ServeAsync_ABacklogPastItsBounds_ClosesWithNoBytesAndALogNote(int count, int payloadBytes)
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 0);
        PublishWhileTheSubAckIsWritten(exchange, bus, count, payloadBytes);

        exchange.Connection.Send(ClientPackets.Subscribe(1, ("t", 0)));
        await FinishAsync(exchange);

        AssertPackets(exchange.Connection.UnreadServerPackets(), ConnackAccepted, SubackGranted);
        CollectionAssert.AreEqual(new[] { HeldNote, OverflowNote }, exchange.Log.Notes.ToArray());
        Assert.AreEqual(0, Publish(bus, "t", "after").DeliveredSubscriptionCount);
    }

    [TestMethod]
    public async Task ServeAsync_ABacklogPassingItsBoundsDuringAWrite_CancelsTheNextWrite()
    {
        var bus = TestMessageBus.New();
        bus.Retain("a", "1"u8);
        bus.Retain("b", "2"u8);
        var exchange = Start(bus, holdCount: 0);
        PublishWhileTheSubAckIsWritten(exchange, bus, count: 1001, payloadBytes: 1);

        exchange.Connection.Send(ClientPackets.Subscribe(1, ("#", 0)));
        await FinishAsync(exchange);

        AssertPackets(exchange.Connection.UnreadServerPackets(), ConnackAccepted, SubackGranted);
        CollectionAssert.AreEqual(new[] { OverflowNote }, exchange.Log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow(false, DisplayName = "a live publish is the count-th")]
    [DataRow(true, DisplayName = "a retained publish is the count-th")]
    public async Task ServeAsync_ABacklogArrivingWhileTheCountThPublishIsWritten_StillSendsDisconnect(bool retained)
    {
        var bus = TestMessageBus.New();
        if (retained)
        {
            bus.Retain("t", "hi"u8);
        }

        var exchange = Start(bus, holdCount: 1);
        exchange.Connection.Writing = packet =>
        {
            if (packet[0] == 0x90 && !retained)
            {
                Publish(bus, "t", "hi");
            }

            if ((packet[0] & 0xF0) == 0x30)
            {
                for (var index = 0; index <= MqttHeldSubscription.MaxWaitingPublishes; index++)
                {
                    Publish(bus, "t", "more");
                }
            }
        };

        exchange.Connection.Send(ClientPackets.Subscribe(1, ("t", 0)));
        await FinishAsync(exchange);

        var publish = retained ? Retained("t", "hi") : Live("t", "hi");
        AssertPackets(exchange.Connection.UnreadServerPackets(), ConnackAccepted, SubackGranted, publish, Disconnect);
        CollectionAssert.DoesNotContain(exchange.Log.Notes.ToArray(), OverflowNote);
    }

    [TestMethod]
    public async Task ServeAsync_ABacklogPassingItsBoundsWhileAWriteIsBlocked_CancelsThatWrite()
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 0);
        exchange.Connection.BlocksWriteOf = packet => packet[0] == 0x90;
        exchange.Connection.Send(ClientPackets.Subscribe(1, ("t", 0)));
        await exchange.Connection.WriteBlocked.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        for (var index = 0; index <= MqttHeldSubscription.MaxWaitingPublishes; index++)
        {
            Publish(bus, "t", "hi");
        }

        await FinishAsync(exchange);
        AssertPackets(exchange.Connection.UnreadServerPackets(), ConnackAccepted);
        CollectionAssert.AreEqual(new[] { OverflowNote }, exchange.Log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_TheExchangeCancelledWhileHeld_EndsTheReadAndThrows()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var exchange = Start(TestMessageBus.New(), holdCount: 0, cancellation.Token);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0)), SubackGranted);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => FinishAsync(exchange));
        Assert.AreEqual(0, exchange.Connection.ReadsWaiting);
        Assert.IsEmpty(exchange.Connection.UnreadServerPackets());
    }

    [TestMethod]
    public async Task ServeAsync_TheCountReached_LeavesNoReadWaiting()
    {
        var bus = TestMessageBus.New();
        var exchange = Start(bus, holdCount: 1);
        await SubscribeAsync(exchange, ClientPackets.Subscribe(1, ("t", 0)), SubackGranted);

        Publish(bus, "t", "hi");
        await FinishAsync(exchange);

        Assert.AreEqual(0, exchange.Connection.ReadsWaiting);
    }

    private static BusPublishResult Publish(MessageBus.MessageBus bus, string topic, string payload) =>
        bus.Publish(new BusMessage(topic, Encoding.UTF8.GetBytes(payload)));

    private static void PublishWhileTheSubAckIsWritten(HeldExchange exchange, MessageBus.MessageBus bus, int count, int payloadBytes) =>
        exchange.Connection.Writing = packet =>
        {
            if (packet[0] != 0x90)
            {
                return;
            }

            var payload = new byte[payloadBytes];
            for (var index = 0; index < count; index++)
            {
                bus.Publish(new BusMessage("t", payload));
            }
        };

    private static byte[] Live(string topic, string payload) => ClientPackets.Publish(0x30, topic, payload);

    private static byte[] Retained(string topic, string payload) => ClientPackets.Publish(0x31, topic, payload);

    private static void AssertPackets(IReadOnlyList<byte[]> actual, params byte[][] expected) =>
        CollectionAssert.AreEqual(
            expected.Select(Convert.ToHexString).ToArray(),
            actual.Select(Convert.ToHexString).ToArray());

    // Starts serving a connection that has sent curl's CONNECT; nothing is read until asked for.
    private HeldExchange Start(MessageBus.MessageBus bus, int holdCount, CancellationToken? cancellationToken = null, ManualTimeProvider? clock = null)
    {
        var server = new MqttProtocolServer(bus, new AnonymousAuthenticationPolicy(), holdCount);
        var connection = new MqttTestPipeConnection();
        var log = new RecordingExchangeLog();
        clock ??= new ManualTimeProvider();
        connection.Send(ClientPackets.CurlConnect());
        var serving = server.ServeAsync(connection, MqttTestExchange.Context(clock, cancellationToken ?? TestContext.CancellationToken, log: log));

        return new HeldExchange(connection, log, clock, serving);
    }

    // Sends a SUBSCRIBE and takes the server's packets up to and including its SUBACK.
    private async Task SubscribeAsync(HeldExchange exchange, byte[] subscribe, byte[] expectedSubAck)
    {
        exchange.Connection.Send(subscribe);
        var packet = await exchange.Connection.ReadPacketAsync(TestContext.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        if (packet[0] == ConnackAccepted[0])
        {
            packet = await exchange.Connection.ReadPacketAsync(TestContext.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        }

        AssertPackets([packet], expectedSubAck);
    }

    private async Task AssertNextPacketAsync(HeldExchange exchange, byte[] expected)
    {
        var packet = await exchange.Connection.ReadPacketAsync(TestContext.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        AssertPackets([packet], expected);
    }

    private Task FinishAsync(HeldExchange exchange) => exchange.Serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

    private sealed record HeldExchange(MqttTestPipeConnection Connection, RecordingExchangeLog Log, ManualTimeProvider Clock, Task Serving);
}
