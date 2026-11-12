using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Durable and shared topic subscriptions (AMQP 1.0 Part 3 sections 3.5.3, 3.5.5 and 3.5.6; Part 2 sections
/// 2.6.6 and 2.8.15; ADR-0146 decision 3; matrix rows <c>Amqp1:Messaging-3.5.5-1</c>, <c>-3.5.5-2</c>,
/// <c>Amqp1:Surl-Subscriptions-1</c> to <c>-3</c>, <c>Amqp1:Core-2.6.6-2</c> and <c>Amqp1:Messaging-3.5.6-1</c>).
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    private static readonly string DurableQueue = Amqp1KeptSubscription.QueueNameFor("client", "durable", isShared: false, isGlobal: false);

    [TestMethod]
    public async Task ServeAsync_DurableAttach_IsAnsweredConfigurationAndNeverAndKeepsWhatIsPublishedWhileDetached()
    {
        var bus = Bus();
        var (first, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never", timeout: 60)), BusFrame(new Amqp1Detach(0)));
        bus.Publish(new BusMessage("news", "kept"u8.ToArray()));

        var (second, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")), BusFrame(Credit(0, deliveryCount: 0, credit: 10)));

        CollectionAssert.AreEqual(
            new object?[] { "/topic/news", 1u, new Amqp1Symbol("never"), 60u, null, null, new Amqp1Symbol("copy") },
            Fields(((Amqp1Attach)first[2].Performative!).Source).ToArray());
        Assert.AreEqual("kept", DataOf(Transfers(second).Single().Payload));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Connect("alice").InspectQueue(DurableQueue).Outcome);
    }

    [TestMethod]
    public async Task ServeAsync_DeliveryUnsettledWhenTheDurableLinkDetaches_ReturnsToTheSubscriptionMarkedRedelivered()
    {
        var bus = Bus();
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")), BusFrame(new Amqp1Detach(0)));
        bus.Publish(new BusMessage("news", "again"u8.ToArray()));
        var (first, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")), BusFrame(Credit(0, deliveryCount: 0, credit: 10)), BusFrame(new Amqp1Detach(0)));

        var (second, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")), BusFrame(Credit(0, deliveryCount: 0, credit: 10)));

        Assert.HasCount(1, Transfers(first));
        var redelivered = Amqp1MessageSection.ReadAll(Transfers(second).Single().Payload);
        Assert.AreEqual(1u, Amqp1Header.FromDescribed(redelivered[0].Described).DeliveryCount);
    }

    [TestMethod]
    public async Task ServeAsync_DurableSubscriptionAfterASaveAndLoadOfTheBus_StillHasWhatWasKept()
    {
        var bus = Bus();
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")), BusFrame(new Amqp1Detach(0)));
        bus.Publish(new BusMessage("news", "survives"u8.ToArray()));
        var snapshot = bus.TakeSnapshot();
        bus.Reset();
        bus.Restore(snapshot);

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")), BusFrame(Credit(0, deliveryCount: 0, credit: 10)));

        Assert.AreEqual("survives", DataOf(Transfers(written).Single().Payload));
    }

    [TestMethod]
    public async Task ServeAsync_DurableReattachWithAnotherSelector_ReplacesTheSubscriptionAndItsMessages()
    {
        var bus = Bus();
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")), BusFrame(new Amqp1Detach(0)));
        bus.Publish(new BusMessage("news", "old"u8.ToArray()));

        var (written, _) = await ServeOnBusAsync(
            bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never", filters: SelectorFilter("1 = 1"))), BusFrame(Credit(0, deliveryCount: 0, credit: 10)));

        Assert.IsEmpty(Transfers(written));
        Assert.IsInstanceOfType<Amqp1Attach>(written[2].Performative);
        Assert.AreEqual(0, bus.Connect("alice").InspectQueue(DurableQueue).ReadyMessageCount);
    }

    [TestMethod]
    public async Task ServeAsync_DurableSubscriptionWithASelector_AcknowledgesWhatItRefusesAtDelivery()
    {
        var bus = Bus();
        var selector = SelectorFilter("color = 'red'");
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never", filters: selector)), BusFrame(new Amqp1Detach(0)));
        foreach (var color in new[] { "red", "blue" })
        {
            bus.Publish(new BusMessage("news", Encoding.UTF8.GetBytes(color)) { Headers = [new("color", color)] });
        }

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never", filters: selector) with { SenderSettleMode = 1 }), BusFrame(Credit(0, deliveryCount: 0, credit: 10)));

        Assert.AreEqual("red", DataOf(Transfers(written).Single().Payload));
        Assert.AreEqual(0, bus.Connect("alice").InspectQueue(DurableQueue).ReadyMessageCount);
    }

    [TestMethod]
    public async Task ServeAsync_DurableSubscriptionOnATopicTheClientCannotName_AcknowledgesItAtDelivery()
    {
        var bus = Bus(new MessageBusConfiguration { TopicMaps = [new TopicMap("amqp1", "/topic/alias", "mqtt", "real")] });
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/#", "never")), BusFrame(new Amqp1Detach(0)));
        bus.Publish(new BusMessage("alias", "hidden"u8.ToArray()));
        bus.Publish(new BusMessage("news", "shown"u8.ToArray()));

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/#", "never") with { SenderSettleMode = 1 }), BusFrame(Credit(0, deliveryCount: 0, credit: 10)));

        Assert.AreEqual("shown", DataOf(Transfers(written).Single().Payload));
        Assert.AreEqual(0, bus.Connect("alice").InspectQueue(DurableQueue).ReadyMessageCount);
    }

    [TestMethod]
    public async Task ServeAsync_AttachWithNoSource_IsAnsweredWithTheKeptSubscriptionsSourceAndAClosedDetachDeletesIt()
    {
        var bus = Bus();
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never", filters: SelectorFilter("1 = 1"))), BusFrame(new Amqp1Detach(0)));

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news") with { Source = null }), BusFrame(new Amqp1Detach(0) { Closed = true }));

        var source = Fields(((Amqp1Attach)written[2].Performative!).Source);
        CollectionAssert.AreEqual(new object?[] { "/topic/news", 1u, new Amqp1Symbol("never") }, source.Take(3).ToArray());
        Assert.AreEqual(SelectorDescriptor, ((Amqp1Described)((Amqp1Map)source[7]!).Entries.Single().Value!).Descriptor);
        Assert.AreEqual(new Amqp1Detach(0) { Closed = true }, written[3].Performative);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.Connect("alice").InspectQueue(DurableQueue).Outcome);
    }

    [TestMethod]
    public async Task ServeAsync_AttachWithNoSourceAndNoKeptSubscription_IsRefusedNotFound()
    {
        var (written, _) = await ServeOnBusAsync(Bus(), Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news") with { Source = null }));

        Assert.IsNull(((Amqp1Attach)written[2].Performative!).Source);
        Assert.AreEqual(new Amqp1Detach(0) { Closed = true, Error = new(Amqp1ErrorCondition.NotFound) { Description = "no such node" } }, written[3].Performative);
    }

    [TestMethod]
    public async Task ServeAsync_ClosedDetach_DeletesTheSubscriptionAndItsMessages()
    {
        var bus = Bus();

        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")), BusFrame(new Amqp1Detach(0) { Closed = true }));

        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.Connect("alice").InspectQueue(DurableQueue).Outcome);
    }

    [TestMethod]
    public async Task ServeAsync_SecondAttachWhileTheFirstHoldsTheSubscription_IsRefusedResourceLocked()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")), BusFrame(DurableAttach(1, "/topic/news", "never")), BusFrame(new Amqp1Detach(1) { Closed = true }));

        var refused = (Amqp1Attach)written[3].Performative!;
        Assert.IsNull(refused.Source);
        Assert.AreEqual(new Amqp1Detach(1) { Closed = true, Error = new(Amqp1ErrorCondition.ResourceLocked) { Description = "subscription in use" } }, written[4].Performative);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Connect("alice").InspectQueue(DurableQueue).Outcome);
    }

    [TestMethod]
    public async Task ServeAsync_SubscriptionAnotherConnectionKeepsExclusively_IsRefusedResourceLocked()
    {
        var bus = Bus();
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "connection-close")), BusFrame(new Amqp1Detach(0)));
        var plan = Amqp1KeptSubscription.Plan("client", "durable", new Amqp1Terminus("/topic/news", false, false) { ExpiryPolicy = "connection-close" }, "news", null);
        var other = bus.Connect("bob");
        other.DeclareQueue(DurableQueue, new BusQueueFlags(Exclusive: true), plan.Description);

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "connection-close")));

        Assert.AreEqual(Amqp1ErrorCondition.ResourceLocked, ((Amqp1Detach)written[3].Performative!).Error!.Condition);
    }

    [TestMethod]
    public async Task ServeAsync_SharedAttachesWithGlobal_CompeteAcrossContainersAndDropTheContainerIdFromTheKey()
    {
        var bus = Bus();
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never", "group|1", null, "shared", "global")), BusFrame(new Amqp1Detach(0)));
        bus.Publish(new BusMessage("news", "one"u8.ToArray()));
        bus.Publish(new BusMessage("news", "two"u8.ToArray()));
        var otherOpen = Amqp1Frame.Carrying(0, new Amqp1Open("other")).Encode();

        var (written, _) = await ServeOnBusAsync(
            bus,
            otherOpen,
            Begin(),
            BusFrame(DurableAttach(0, "/topic/news", "never", "group|2", null, "shared", "global")),
            BusFrame(DurableAttach(1, "/topic/news", "never", "group|3", null, "shared", "global")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(Credit(1, deliveryCount: 0, credit: 10)));

        var answered = Fields(((Amqp1Attach)written[2].Performative!).Source);
        CollectionAssert.AreEqual(new object?[] { new Amqp1Symbol("shared"), new Amqp1Symbol("global") }, ((Amqp1Array)answered[10]!).Elements.ToArray());
        Assert.IsInstanceOfType<Amqp1Attach>(written[3].Performative);
        Assert.IsNotNull(((Amqp1Attach)written[3].Performative!).Source);
        CollectionAssert.AreEquivalent(new[] { "one", "two" }, Transfers(written).Select(transfer => DataOf(transfer.Payload)).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_SharedLinkWithAnotherSelectorWhileOneIsAttached_IsRefusedResourceLocked()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(DurableAttach(0, "/topic/news", "never", "group|1", null, "shared")),
            BusFrame(DurableAttach(1, "/topic/news", "never", "group|2", SelectorFilter("1 = 1"), "shared")));

        Assert.AreEqual(Amqp1ErrorCondition.ResourceLocked, ((Amqp1Detach)written[4].Performative!).Error!.Condition);
    }

    [TestMethod]
    public async Task ServeAsync_ClosedDetachOfASharedLink_DeletesTheSubscriptionOnlyWhenItIsTheLast()
    {
        var bus = Bus();
        var shared = Amqp1KeptSubscription.QueueNameFor("client", "group|1", isShared: true, isGlobal: false);
        var held = new List<MessageBusOutcome>();

        await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(DurableAttach(0, "/topic/news", "never", "group|1", null, "shared")),
            BusFrame(DurableAttach(1, "/topic/news", "never", "group|2", null, "shared")),
            BusFrame(new Amqp1Detach(0) { Closed = true }));
        held.Add(bus.Connect("alice").InspectQueue(shared).Outcome);

        await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(DurableAttach(0, "/topic/news", "never", "group|1", null, "shared")),
            BusFrame(new Amqp1Detach(0) { Closed = true }));
        held.Add(bus.Connect("alice").InspectQueue(shared).Outcome);

        CollectionAssert.AreEqual(new[] { MessageBusOutcome.Succeeded, MessageBusOutcome.UnknownQueue }, held);
    }

    [TestMethod]
    [DataRow("session-end")]
    [DataRow("connection-close")]
    [DataRow(null)]
    [DataRow("something-else")]
    public async Task ServeAsync_ShorterExpiryPolicy_KeepsTheSubscriptionUntilThatEventAnsweredTimeoutZero(string? policy)
    {
        var bus = Bus();
        var applied = policy is "connection-close" ? "connection-close" : "session-end";

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(DurableAttach(0, "/topic/news", policy, timeout: 30)),
            BusFrame(new Amqp1Detach(0)),
            BusFrame(SenderAttach(1, "/topic/news")),
            BusFrame(new Amqp1Transfer(1) { DeliveryId = 0, DeliveryTag = [1], Settled = true }, ColoredMessage("red", "between")),
            BusFrame(DurableAttach(2, "/topic/news", policy)),
            BusFrame(Credit(2, deliveryCount: 0, credit: 10)));

        CollectionAssert.AreEqual(
            new object?[] { "/topic/news", 1u, new Amqp1Symbol(applied), null, null, null, new Amqp1Symbol("copy") },
            Fields(((Amqp1Attach)written[2].Performative!).Source).ToArray());
        Assert.AreEqual("between", DataOf(Transfers(written).Single().Payload));
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.Connect("alice").InspectQueue(DurableQueue).Outcome);
    }

    [TestMethod]
    public async Task ServeAsync_SessionEnd_DeletesTheSubscriptionKeptUntilThen()
    {
        var bus = Bus();
        var connection = new InMemoryConnection(
            [Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "session-end")), BusFrame(new Amqp1Detach(0)), BusFrame(new Amqp1End()), BusFrame(new Amqp1Begin(0, 100, 100))],
            peerHalfClosesWhenExhausted: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new Amqp1ContainerConnection(connection, Context(new RecordingExchangeLog(), cancellation.Token), Options(), bus, "alice").ServeAsync();
        await WaitUntilWrittenAsync(async () => (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Count(frame => frame.Performative is Amqp1Begin) == 2);

        var afterSessionEnd = bus.Connect("alice").InspectQueue(DurableQueue).Outcome;
        await cancellation.CancelAsync();
        await serving;

        Assert.AreEqual(MessageBusOutcome.UnknownQueue, afterSessionEnd);
    }

    [TestMethod]
    [DataRow("link-detach")]
    [DataRow("never", true)]
    public async Task ServeAsync_LinkDetachPolicyOrNoDurability_IsAnsweredNoneAndKeepsNothing(string policy, bool noDurability = false)
    {
        var bus = Bus();
        var attach = DurableAttach(0, "/topic/news", policy) with
        {
            Source = new Amqp1Described(Amqp1Descriptor.Source, new List<object?> { "/topic/news", noDurability ? 0u : 2u, new Amqp1Symbol(policy) }),
        };

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(attach));

        CollectionAssert.AreEqual(
            new object?[] { "/topic/news", null, null, null, null, null, new Amqp1Symbol("copy") },
            Fields(((Amqp1Attach)written[2].Performative!).Source).ToArray());
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.Connect("alice").InspectQueue(DurableQueue).Outcome);
    }

    [TestMethod]
    public async Task ServeAsync_DurableAttachWithoutTheSubscribeRight_IsRefusedAtEveryAttach()
    {
        var bus = Bus(new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("#", "alice", TopicAccessRights.Publish)] });

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")));

        Assert.AreEqual(Amqp1ErrorCondition.UnauthorizedAccess, ((Amqp1Detach)written[3].Performative!).Error!.Condition);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.Connect("alice").InspectQueue(DurableQueue).Outcome);
    }

    [TestMethod]
    public async Task ServeAsync_DurableAttachWithTheTopicButNotTheQueueGranted_IsRefusedUnauthorized()
    {
        var bus = Bus(new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("news", "alice", TopicAccessRights.Subscribe)] });

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news", "never")));

        Assert.AreEqual(Amqp1ErrorCondition.UnauthorizedAccess, ((Amqp1Detach)written[3].Performative!).Error!.Condition);
    }

    [TestMethod]
    [DataRow(0, "amqp:resource-limit-exceeded", DisplayName = "no queue left")]
    [DataRow(1, "amqp:resource-limit-exceeded", DisplayName = "no subscription left")]
    [DataRow(2, "amqp:not-found", DisplayName = "a queue declared with other flags")]
    public async Task ServeAsync_DurableAttachTheBusBounds_IsRefusedWithTheBoundsError(int bound, string condition)
    {
        var clock = new ManualTimeProvider();
        var bus = new MessageBus.MessageBus(clock, maxQueues: bound == 0 ? 1 : 100, maxSubscriptionsPerConnection: bound == 1 ? 1 : 100);
        if (bound == 0)
        {
            bus.Connect().DeclareQueue("taken", new BusQueueFlags(Durable: true));
        }

        if (bound == 2)
        {
            var plan = Amqp1KeptSubscription.Plan("client", "durable", new Amqp1Terminus("/topic/news", false, false) { ExpiryPolicy = "never" }, "news", null);
            bus.Connect().DeclareQueue(DurableQueue, new BusQueueFlags(Durable: true, AutoDelete: true), plan.Description);
        }

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(DurableAttach(0, "/topic/news", "never", name: bound == 1 ? "first" : "durable")),
            BusFrame(DurableAttach(1, "/topic/news", "never", name: "second")));

        var refusal = written.Select(frame => frame.Performative).OfType<Amqp1Detach>().First();
        Assert.AreEqual(new Amqp1Symbol(condition), refusal.Error!.Condition);
    }

    [TestMethod]
    public async Task ServeAsync_QueueNamedLikeASubscription_IsRefusedNotFoundToBothRoles()
    {
        var name = "/queue/" + DurableQueue;

        var (written, _) = await ServeOnBusAsync(Bus(), Open(), Begin(), BusFrame(ReceiverAttach(0, name)), BusFrame(SenderAttach(1, name)));

        Assert.AreEqual(Amqp1ErrorCondition.NotFound, ((Amqp1Detach)written[3].Performative!).Error!.Condition);
        Assert.AreEqual(Amqp1ErrorCondition.NotFound, ((Amqp1Detach)written[5].Performative!).Error!.Condition);
    }

    [TestMethod]
    public void FromDescription_TextThatIsNotADescription_IsNull()
    {
        Assert.IsNull(Amqp1KeptSubscription.FromDescription("q", null));
        Assert.IsNull(Amqp1KeptSubscription.FromDescription("q", "not hex"));
        Assert.IsNull(Amqp1KeptSubscription.FromDescription("q", Convert.ToHexStringLower(Amqp1Encoder.Encode("a string"))));
        Assert.IsNull(Amqp1KeptSubscription.FromDescription("q", Convert.ToHexStringLower(Amqp1Encoder.Encode(new object?[] { "only", "two" }))));
        Assert.IsNull(Amqp1KeptSubscription.FromDescription("q", Convert.ToHexStringLower(Amqp1Encoder.Encode(new object?[] { "a", "b", 5, "never", false, false }))));
    }

    [TestMethod]
    public void QueueNamesFor_ANameWithABar_IsOneNamePerWayOfSharingIt()
    {
        var names = Amqp1KeptSubscription.QueueNamesFor("client", "group|1");

        Assert.HasCount(3, names);
        Assert.AreEqual(Amqp1KeptSubscription.QueueNameFor("client", "group|1", false, false), names[0]);
        Assert.AreEqual(Amqp1KeptSubscription.QueueNameFor("client", "group", true, false), names[1]);
        Assert.AreEqual(Amqp1KeptSubscription.QueueNameFor("anyone", "group|9", true, true), names[2]);
        Assert.HasCount(2, Amqp1KeptSubscription.QueueNamesFor("client", "plain").Distinct());
    }

    private static Amqp1Attach DurableAttach(uint handle, string address, string? policy = null, string name = "durable", Amqp1Map? filters = null, params string[] capabilities) =>
        new(name, handle, IsReceiver: true)
        {
            Source = new Amqp1Described(
                Amqp1Descriptor.Source,
                new List<object?>
                {
                    address,
                    1u,
                    policy is null ? null : new Amqp1Symbol(policy),
                    null,
                    null,
                    null,
                    null,
                    filters,
                    null,
                    null,
                    Amqp1Fields.SymbolsField([.. capabilities.Select(capability => new Amqp1Symbol(capability))]),
                }),
        };

    private static Amqp1Attach DurableAttach(uint handle, string address, string? policy, uint timeout) =>
        DurableAttach(handle, address, policy) with
        {
            Source = new Amqp1Described(Amqp1Descriptor.Source, new List<object?> { address, 1u, policy is null ? null : new Amqp1Symbol(policy), timeout }),
        };
}
