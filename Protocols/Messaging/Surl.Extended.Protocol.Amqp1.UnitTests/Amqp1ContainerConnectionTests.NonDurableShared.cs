using System.Security.Cryptography;
using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Non-durable shared topic subscriptions (AMQP 1.0 Part 3 sections 3.5.3 and 3.5.6; Jakarta Messaging 3.1
/// section 8.3.2; ADR-0146 Amendment 4; matrix row <c>Amqp1:Surl-Subscriptions-2</c>): the links of one
/// compete on a bus queue that goes with its last link. The connections here open with the
/// <c>container-id</c> <c>client</c>.
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    // ADR-0146 Amendment 4 item 3: the prefix and the hex SHA-256 of the container-id, a zero byte and
    // the link name up to its first "|".
    private static readonly string NonDurableSharedQueue =
        "surl-subscription-nondurable-" + Convert.ToHexStringLower(SHA256.HashData("client\0group"u8.ToArray()));

    [TestMethod]
    [DataRow(0u, null, false, DisplayName = "durable none")]
    [DataRow(0u, "never", false, DisplayName = "durable none, expiry-policy never")]
    [DataRow(2u, "link-detach", false, DisplayName = "link-detach")]
    [DataRow(0u, null, true, DisplayName = "global")]
    public async Task ServeAsync_TwoLinksOnANonDurableSharedSource_ReceiveEachMessageOnceBetweenThem(uint durable, string? policy, bool global)
    {
        string[] capabilities = global ? ["shared", "global"] : ["shared"];

        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(SharedAttach(0, "group|1", durable, policy, NoLocal(), capabilities)),
            BusFrame(SharedAttach(1, "group|2", durable, policy, NoLocal(), capabilities)),
            BusFrame(SenderAttach(2, "/topic/news")),
            Publication(2, 0, "one"),
            Publication(2, 1, "two"),
            Publication(2, 2, "three"),
            Publication(2, 3, "four"),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(Credit(1, deliveryCount: 0, credit: 10)));

        CollectionAssert.AreEquivalent(new[] { "one", "two", "three", "four" }, Bodies(written));
        CollectionAssert.AreEquivalent(new[] { "one", "three" }, Bodies(written, handle: 0));
        CollectionAssert.AreEquivalent(new[] { "two", "four" }, Bodies(written, handle: 1));
        var answeredReceivers = written.Select(frame => frame.Performative).OfType<Amqp1Attach>().Where(attach => !attach.IsReceiver).ToList();
        Assert.HasCount(2, answeredReceivers);
        foreach (var answered in answeredReceivers)
        {
            var source = Fields(answered.Source);
            CollectionAssert.AreEqual(
                new object?[] { "/topic/news", null, new Amqp1Symbol("link-detach"), null, null, null, new Amqp1Symbol("copy"), null, null, null },
                source.Take(10).ToArray());
            CollectionAssert.AreEqual(
                capabilities.Select(capability => (object?)new Amqp1Symbol(capability)).ToArray(),
                source[10] is Amqp1Array array ? array.Elements.ToArray() : [source[10]]);
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "a detach that does not close")]
    [DataRow(true, DisplayName = "a closed detach")]
    public async Task ServeAsync_OneOfTwoNonDurableSharedLinksDetaches_TheOtherReceivesWhatItLeftUnsettledAndEveryLaterMessage(bool closed)
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(SharedAttach(0, "group|1")),
            BusFrame(SharedAttach(1, "group|2")),
            BusFrame(SenderAttach(2, "/topic/news")),
            Publication(2, 0, "one"),
            Publication(2, 1, "two"),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(Credit(1, deliveryCount: 0, credit: 10)),
            BusFrame(new Amqp1Detach(0) { Closed = closed }),
            Publication(2, 2, "three"),
            Publication(2, 3, "four"),
            BusFrame(Credit(1, deliveryCount: 0, credit: 20)));

        CollectionAssert.AreEqual(new[] { "one" }, Bodies(written, handle: 0));
        CollectionAssert.AreEqual(new[] { "two", "one", "three", "four" }, Bodies(written, handle: 1));
        var redelivered = Transfers(written).Last(transfer => DataOf(transfer.Payload) == "one").Payload;
        Assert.AreEqual(1u, Amqp1Header.FromDescribed(Amqp1MessageSection.ReadAll(redelivered)[0].Described).DeliveryCount);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "detaches that do not close")]
    [DataRow(true, DisplayName = "closed detaches")]
    public async Task ServeAsync_BothNonDurableSharedLinksDetach_NothingOfTheSubscriptionRemainsOnTheBus(bool closed)
    {
        var bus = Bus();
        var observer = bus.Connect("alice");
        var connection = new InMemoryConnection(
            [Open(), Begin(), BusFrame(SharedAttach(0, "group|1")), BusFrame(SharedAttach(1, "group|2")), BusFrame(new Amqp1Detach(0) { Closed = closed })],
            peerHalfClosesWhenExhausted: false);
        var both = new InMemoryConnection(
            [Open(), Begin(), BusFrame(SharedAttach(0, "group|1")), BusFrame(SharedAttach(1, "group|2")), BusFrame(new Amqp1Detach(0) { Closed = closed }), BusFrame(new Amqp1Detach(1) { Closed = closed })],
            peerHalfClosesWhenExhausted: false);

        var afterOne = await InspectOnceDetachedAsync(bus, connection, detaches: 1, () => observer.InspectQueue(NonDurableSharedQueue));
        var afterBoth = await InspectOnceDetachedAsync(bus, both, detaches: 2, () => observer.InspectQueue(NonDurableSharedQueue));

        Assert.AreEqual(MessageBusOutcome.Succeeded, afterOne.Outcome);
        Assert.AreEqual(1, afterOne.ConsumerCount);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, afterBoth.Outcome);
    }

    [TestMethod]
    public async Task ServeAsync_SessionEndOfTheNonDurableSharedLinks_RemovesTheSubscription()
    {
        var (bus, declares) = BusActingOnEachDeclareOfTheNonDurableSharedQueue((_, _) => { });
        var connection = new InMemoryConnection(
            [Open(), Begin(), BusFrame(SharedAttach(0, "group|1")), BusFrame(new Amqp1End())],
            peerHalfClosesWhenExhausted: false);

        var afterSessionEnd = await InspectOnceWrittenAsync(
            bus, connection, frame => frame is Amqp1End, 1, () => bus.Connect("alice").InspectQueue(NonDurableSharedQueue));

        Assert.AreEqual(1, declares());
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, afterSessionEnd.Outcome);
    }

    [TestMethod]
    public async Task ServeAsync_NonDurableSharedAttachRefusedAfterItsQueueWasDeclared_LeavesNothingOnTheBus()
    {
        var (bus, declares) = BusActingOnEachDeclareOfTheNonDurableSharedQueue((host, _) => host.BindQueue(NonDurableSharedQueue, "secrets"));
        var connection = new InMemoryConnection([Open(), Begin(), BusFrame(SharedAttach(0, "group|1"))], peerHalfClosesWhenExhausted: false);

        var afterRefusal = await InspectOnceDetachedAsync(bus, connection, detaches: 1, () => bus.Connect("alice").InspectQueue(NonDurableSharedQueue));

        var written = await ReadWrittenWithPayloadsAsync(connection.WrittenBytes);
        Assert.AreEqual(Amqp1ErrorCondition.UnauthorizedAccess, written.Select(frame => frame.Performative).OfType<Amqp1Detach>().Single().Error!.Condition);
        Assert.AreEqual(1, declares());
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, afterRefusal.Outcome);
    }

    [TestMethod]
    public async Task ServeAsync_NonDurableSharedQueueDeletedBetweenTheDeclareAndTheConsume_IsMadeAgain()
    {
        var (bus, declares) = BusActingOnEachDeclareOfTheNonDurableSharedQueue((host, declare) =>
        {
            if (declare == 1)
            {
                host.DeleteQueue(NonDurableSharedQueue);
            }
        });

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(SharedAttach(0, "group|1")),
            BusFrame(SenderAttach(1, "/topic/news")),
            Publication(1, 0, "after the second declare"),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)));

        Assert.AreEqual(2, declares());
        Assert.IsEmpty(written.Select(frame => frame.Performative).OfType<Amqp1Detach>());
        CollectionAssert.AreEqual(new[] { "after the second declare" }, Bodies(written));
    }

    [TestMethod]
    public async Task ServeAsync_NonDurableSharedQueueDeletedAfterEachOfTwoDeclares_IsRefusedNotFound()
    {
        var (bus, declares) = BusActingOnEachDeclareOfTheNonDurableSharedQueue((host, _) => host.DeleteQueue(NonDurableSharedQueue));

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(SharedAttach(0, "group|1")));

        Assert.AreEqual(2, declares());
        Assert.AreEqual(Amqp1ErrorCondition.NotFound, ((Amqp1Detach)written[3].Performative!).Error!.Condition);
    }

    [TestMethod]
    public async Task ServeAsync_KeptQueueDeletedBetweenTheDeclareAndTheBind_IsRefusedNotFoundWithoutASecondDeclare()
    {
        var (bus, declares) = BusActingOnEachDeclare(DurableQueue, (host, _) => host.DeleteQueue(DurableQueue));

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")));

        Assert.AreEqual(1, declares());
        Assert.AreEqual(Amqp1ErrorCondition.NotFound, ((Amqp1Detach)written[3].Performative!).Error!.Condition);
    }

    [TestMethod]
    [DataRow("/topic/news", true, DisplayName = "another selector")]
    [DataRow("/topic/weather", false, DisplayName = "another address")]
    public async Task ServeAsync_AttachWithOtherFiltersWhileALinkConsumesTheNonDurableSharedSubscription_IsRefusedResourceLocked(string address, bool filtered)
    {
        var other = SharedAttach(1, "group|2", filters: filtered ? SelectorFilter("1 = 1") : null);
        var fields = Fields(other.Source).ToList();
        fields[0] = address;

        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(SharedAttach(0, "group|1")),
            BusFrame(other with { Source = new Amqp1Described(Amqp1Descriptor.Source, fields) }),
            BusFrame(SenderAttach(2, "/topic/news")),
            Publication(2, 0, "still shared"),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)));

        Assert.IsNull(((Amqp1Attach)written[3].Performative!).Source);
        Assert.AreEqual(
            new Amqp1Detach(1) { Closed = true, Error = new(Amqp1ErrorCondition.ResourceLocked) { Description = "subscription in use" } },
            written[4].Performative);
        CollectionAssert.AreEqual(new[] { "still shared" }, Bodies(written));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "unshared durable")]
    [DataRow(true, DisplayName = "shared durable")]
    public async Task ServeAsync_NonDurableSharedAttachUnderADurableSubscriptionsLinkName_LeavesTheDurableOneAndItsMessages(bool shared)
    {
        var bus = Bus();
        var name = shared ? "group|1" : "group";
        string[] capabilities = shared ? ["shared"] : [];
        var durableQueue = Amqp1SubscriptionQueue.QueueNameFor("client", name, shared, isGlobal: false);
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never", name, null, capabilities)), BusFrame(new Amqp1Detach(0)));
        var observer = bus.Connect("alice");
        var description = observer.InspectQueue(durableQueue).SubscriptionDescription;
        bus.Publish(new BusMessage("news", "kept"u8.ToArray()));

        var (written, _) = await ServeOnBusAsync(
            bus, Open(), Begin(), BusFrame(SharedAttach(0, name)), BusFrame(Credit(0, deliveryCount: 0, credit: 10)), BusFrame(new Amqp1Detach(0) { Closed = true }));

        Assert.IsNotNull(((Amqp1Attach)written[2].Performative!).Source);
        Assert.IsEmpty(Transfers(written));
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 1, 0, description), observer.InspectQueue(durableQueue));
    }

    [TestMethod]
    public async Task ServeAsync_TwoLinksOnAnUnsharedNonDurableSource_EachReceiveEveryMessage()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "/topic/news")),
            BusFrame(ReceiverAttach(1, "/topic/news")),
            BusFrame(SenderAttach(2, "/topic/news")),
            Publication(2, 0, "one"),
            Publication(2, 1, "two"),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(Credit(1, deliveryCount: 0, credit: 10)));

        CollectionAssert.AreEqual(new[] { "one", "two" }, Bodies(written, handle: 0));
        CollectionAssert.AreEqual(new[] { "one", "two" }, Bodies(written, handle: 1));
    }

    [TestMethod]
    public async Task ServeAsync_LinksAskingDifferentDurabilityThatKeepsNothing_ShareOneSubscription()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(SharedAttach(0, "group|1")),
            BusFrame(SharedAttach(1, "group|2", durable: 2, policy: "link-detach")),
            BusFrame(SenderAttach(2, "/topic/news")),
            Publication(2, 0, "one"),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(Credit(1, deliveryCount: 0, credit: 10)));

        Assert.IsEmpty(written.Select(frame => frame.Performative).OfType<Amqp1Detach>());
        CollectionAssert.AreEqual(new[] { "one" }, Bodies(written));
    }

    [TestMethod]
    public async Task ServeAsync_AttachWithNoSourceUnderANonDurableSharedSubscriptionsName_IsRefusedNotFound()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(), Open(), Begin(), BusFrame(SharedAttach(0, "group")), BusFrame(SharedAttach(1, "group") with { Source = null }));

        Assert.AreEqual(Amqp1ErrorCondition.NotFound, ((Amqp1Detach)written[4].Performative!).Error!.Condition);
    }

    private static (MessageBus.MessageBus Bus, Func<int> Declares) BusActingOnEachDeclareOfTheNonDurableSharedQueue(Action<BusConnection, int> act) =>
        BusActingOnEachDeclare(NonDurableSharedQueue, act);

    // A bus whose test host acts, on the declaring thread, each time the queue is made: between the
    // container's declare and its bind. It is told which declare it is, from 1.
    private static (MessageBus.MessageBus Bus, Func<int> Declares) BusActingOnEachDeclare(string queueName, Action<BusConnection, int> act)
    {
        var events = new UnitTestQueueDeclaredEventPublisher(queueName);
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), events: events);
        var host = bus.Connect("alice");
        events.Declared = declare => act(host, declare);
        return (bus, () => events.DeclareCount);
    }

    private Task<BusQueueInspectResult> InspectOnceDetachedAsync(
        MessageBus.MessageBus bus, InMemoryConnection connection, int detaches, Func<BusQueueInspectResult> inspect) =>
        InspectOnceWrittenAsync(bus, connection, frame => frame is Amqp1Detach, detaches, inspect);

    // Inspects while the connection is still open, once it has written that many such frames, then ends it.
    private async Task<BusQueueInspectResult> InspectOnceWrittenAsync(
        MessageBus.MessageBus bus, InMemoryConnection connection, Func<Amqp1Performative?, bool> awaited, int count, Func<BusQueueInspectResult> inspect)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new Amqp1ContainerConnection(connection, Context(new RecordingExchangeLog(), cancellation.Token), Options(), bus, "alice").ServeAsync();
        await WaitUntilWrittenAsync(async () => (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Count(frame => awaited(frame.Performative)) >= count);

        var inspected = inspect();
        await cancellation.CancelAsync();
        await serving;
        return inspected;
    }

    private static Amqp1Attach SharedAttach(uint handle, string name, uint durable = 0, string? policy = null, Amqp1Map? filters = null, params string[] capabilities) =>
        new(name, handle, IsReceiver: true)
        {
            Source = new Amqp1Described(
                Amqp1Descriptor.Source,
                new List<object?>
                {
                    "/topic/news",
                    durable,
                    policy is null ? null : new Amqp1Symbol(policy),
                    null,
                    null,
                    null,
                    null,
                    filters,
                    null,
                    null,
                    Amqp1Fields.SymbolsField([.. (capabilities.Length == 0 ? ["shared"] : capabilities).Select(capability => new Amqp1Symbol(capability))]),
                }),
        };

    // The bodies of the messages written, on one of the container's handles when given. A delivery's
    // handle is its first transfer's.
    private static string[] Bodies(List<(Amqp1Performative? Performative, byte[] Payload)> written, uint? handle = null) =>
        [.. Transfers(written).Where(transfer => handle is null || transfer.Transfer.Handle == handle).Select(transfer => DataOf(transfer.Payload))];

    /// <summary>Calls <see cref="Declared"/> on the declaring thread each time the bus makes the queue.</summary>
    private sealed class UnitTestQueueDeclaredEventPublisher(string queueName) : ISurlEventPublisher
    {
        public Action<int> Declared { get; set; } = _ => { };

        public int DeclareCount { get; private set; }

        public bool IsPublishingBytes => false;

        public void Publish(SurlEvent surlEvent)
        {
            if (surlEvent is StoreChangedEvent { Change: StoreChange.Written } changed && changed.Name == MessageBus.MessageBus.QueueChangeName(queueName))
            {
                Declared(++DeclareCount);
            }
        }
    }
}
