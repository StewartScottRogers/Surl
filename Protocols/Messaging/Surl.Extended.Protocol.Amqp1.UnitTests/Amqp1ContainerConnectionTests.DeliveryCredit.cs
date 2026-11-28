using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// A queue's messages go only to links that can send them: link credit, the session's window and the 1000
/// unsettled bound (AMQP 1.0 Part 2 sections 2.5.6 and 2.6.7; ADR-0146 Amendment 7). The connections here
/// open with the <c>container-id</c> <c>client</c>.
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    private const string PlainQueueLinks = "a /queue/ address";

    private const string KeptSharedLinks = "a shared kept subscription";

    private const string NonDurableSharedLinks = "a non-durable shared subscription";

    private static readonly string[] FourMessages = ["one", "two", "three", "four"];

    [TestMethod]
    [DataRow(PlainQueueLinks)]
    [DataRow(KeptSharedLinks)]
    [DataRow(NonDurableSharedLinks)]
    public async Task ServeAsync_TwoQueueLinksWithNoCredit_LeaveTheMessagesReadyOnTheBus(string kind)
    {
        var bus = Bus();
        var observer = bus.Connect("alice");
        var connection = new InMemoryConnection(
            [Open(), Begin(), BusFrame(CompetingAttach(kind, 0)), BusFrame(CompetingAttach(kind, 1))],
            peerHalfClosesWhenExhausted: false);

        var inspected = await InspectOnceWrittenAsync(bus, connection, frame => frame is Amqp1Attach, 2, () =>
        {
            PublishToCompetingLinks(bus, kind, FourMessages);
            return observer.InspectQueue(CompetingQueue(kind));
        });

        Assert.AreEqual(4, inspected.ReadyMessageCount);
        Assert.AreEqual(2, inspected.ConsumerCount);
        Assert.IsEmpty(Transfers(await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)));
    }

    [TestMethod]
    [DataRow(PlainQueueLinks, false)]
    [DataRow(PlainQueueLinks, true)]
    [DataRow(KeptSharedLinks, false)]
    [DataRow(KeptSharedLinks, true)]
    [DataRow(NonDurableSharedLinks, false)]
    [DataRow(NonDurableSharedLinks, true)]
    public async Task ServeAsync_OneOfTwoQueueLinksHasCredit_ItAloneIsSentEveryMessage(string kind, bool creditGrantedFirst)
    {
        byte[] credit = BusFrame(Credit(1, deliveryCount: 0, credit: 10));
        byte[][] publications = [.. FourMessages.Select((body, index) => Publication(2, (uint)index, body))];

        var (written, _) = await ServeOnBusAsync(
            Bus(),
            [
                Open(),
                Begin(),
                BusFrame(CompetingAttach(kind, 0)),
                BusFrame(CompetingAttach(kind, 1)),
                BusFrame(SenderAttach(2, kind == PlainQueueLinks ? "orders" : "/topic/news")),
                .. creditGrantedFirst ? [credit, .. publications] : (byte[][])[.. publications, credit],
            ]);

        CollectionAssert.AreEqual(FourMessages, Bodies(written, handle: 1));
        Assert.IsEmpty(Bodies(written, handle: 0));
    }

    [TestMethod]
    [DataRow(PlainQueueLinks)]
    [DataRow(KeptSharedLinks)]
    [DataRow(NonDurableSharedLinks)]
    public async Task ServeAsync_MessagesQueuedBeforeEitherLinkAttached_GoOnlyToTheLinkGrantedCredit(string kind)
    {
        var bus = Bus();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var holder = Task.CompletedTask;
        if (kind == PlainQueueLinks)
        {
            Enqueue(bus, "orders", FourMessages);
        }
        else if (kind == KeptSharedLinks)
        {
            await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(CompetingAttach(kind, 0)), BusFrame(new Amqp1Detach(0)));
            PublishToCompetingLinks(bus, kind, FourMessages);
        }
        else
        {
            // A non-durable queue lives only while a link consumes it: another connection's, with no credit.
            var holding = new InMemoryConnection([Open(), Begin(), BusFrame(CompetingAttach(kind, 0))], peerHalfClosesWhenExhausted: false);
            holder = new Amqp1ContainerConnection(holding, Context(new RecordingExchangeLog(), cancellation.Token), Options(), bus, "alice").ServeAsync();
            await WaitUntilWrittenAsync(async () => (await ReadWrittenWithPayloadsAsync(holding.WrittenBytes)).Any(frame => frame.Performative is Amqp1Attach));
            PublishToCompetingLinks(bus, kind, FourMessages);
        }

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(CompetingAttach(kind, 0)),
            BusFrame(CompetingAttach(kind, 1)),
            BusFrame(Credit(1, deliveryCount: 0, credit: 10)));

        await cancellation.CancelAsync();
        await holder;
        CollectionAssert.AreEqual(FourMessages, Bodies(written, handle: 1));
        Assert.IsEmpty(Bodies(written, handle: 0));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "credit withdrawn")]
    [DataRow(true, DisplayName = "credit drained")]
    public async Task ServeAsync_OneQueueLinksCreditGoes_TheOtherIsSentTheNextMessages(bool drain)
    {
        var flow = drain ? Credit(0, deliveryCount: 0, credit: 10) with { Drain = true } : Credit(0, deliveryCount: 0, credit: 0);

        var (written, _) = await ServeOnBusAsync(
            Bus(),
            [
                Open(),
                Begin(),
                BusFrame(ReceiverAttach(0, "orders")),
                BusFrame(ReceiverAttach(1, "orders")),
                BusFrame(SenderAttach(2, "orders")),
                BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
                BusFrame(Credit(1, deliveryCount: 0, credit: 10)),
                BusFrame(flow),
                .. FourMessages.Select((body, index) => Publication(2, (uint)index, body)),
            ]);

        CollectionAssert.AreEqual(FourMessages, Bodies(written, handle: 1));
        Assert.IsEmpty(Bodies(written, handle: 0));
        CollectionAssert.AreEqual(new[] { 0u, 0u, 0u, 0u }, DeliveryCounts(written));
    }

    [TestMethod]
    public async Task ServeAsync_ASessionsWindowIsClosedByAFlow_ALinkInAnotherSessionIsSentTheNextMessages()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            [
                Open(),
                Begin(),
                OnSecondSession(new Amqp1Begin(0, 100, 100)),
                BusFrame(ReceiverAttach(0, "orders")),
                OnSecondSession(SenderAttach(0, "orders")),
                OnSecondSession(ReceiverAttach(1, "orders")),
                BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
                OnSecondSession(Credit(1, deliveryCount: 0, credit: 10)),
                BusFrame(new Amqp1Flow(100, 0, 100) { NextIncomingId = 0, IncomingWindow = 0 }),
                .. FourMessages.Select((body, index) => OnSecondSession(
                    new Amqp1Transfer(0) { DeliveryId = (uint)index, DeliveryTag = [(byte)index], Settled = true }, ColoredMessage("red", body))),
            ]);

        CollectionAssert.AreEqual(FourMessages, Bodies(written, handle: 1));
        Assert.IsEmpty(Bodies(written, handle: 0));
        CollectionAssert.AreEqual(new[] { 0u, 0u, 0u, 0u }, DeliveryCounts(written));
    }

    [TestMethod]
    public async Task ServeAsync_ASendClosesTheSessionsWindow_WhatTheBusHeldForTheLinkGoesToAnotherNotRedelivered()
    {
        var bus = Bus();
        Enqueue(bus, "orders", FourMessages);

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            BusFrame(new Amqp1Begin(0, 1, 100)),
            OnSecondSession(new Amqp1Begin(0, 100, 100)),
            BusFrame(ReceiverAttach(0, "orders")),
            OnSecondSession(SenderAttach(0, "unused")),
            OnSecondSession(ReceiverAttach(1, "orders")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10) with { IncomingWindow = 1 }),
            OnSecondSession(Credit(1, deliveryCount: 0, credit: 10)));

        CollectionAssert.AreEqual(new[] { "one" }, Bodies(written, handle: 0));
        CollectionAssert.AreEqual(new[] { "two", "three", "four" }, Bodies(written, handle: 1));
        CollectionAssert.AreEqual(new[] { 0u, 0u, 0u, 0u }, DeliveryCounts(written));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "no outcome stated")]
    [DataRow(true, DisplayName = "outcomes stated and unsettled")]
    public async Task ServeAsync_AQueueLinkHoldsAThousandUnsettled_AnotherIsSentTheNextMessages(bool statesOutcomes)
    {
        var bus = Bus();
        Enqueue(bus, "orders", [.. Enumerable.Range(0, Amqp1ContainerConnection.MaxWaitingDeliveries).Select(index => $"message-{index}")]);
        var stated = BusFrame(new Amqp1Disposition(true, 0)
        {
            Last = Amqp1ContainerConnection.MaxWaitingDeliveries - 1,
            State = Amqp1DeliveryState.Accepted,
        });

        var (written, _) = await ServeOnBusAsync(
            bus,
            [
                Open(),
                BusFrame(new Amqp1Begin(0, 5000, 5000)),
                BusFrame(ReceiverAttach(0, "orders")),
                BusFrame(ReceiverAttach(1, "orders")),
                BusFrame(SenderAttach(2, "orders")),
                BusFrame(Credit(0, deliveryCount: 0, credit: 2000) with { IncomingWindow = 5000 }),
                .. statesOutcomes ? [stated] : Array.Empty<byte[]>(),
                BusFrame(Credit(1, deliveryCount: 0, credit: 10) with { IncomingWindow = 5000 }),
                .. FourMessages.Select((body, index) => Publication(2, (uint)index, body)),
            ]);

        Assert.HasCount(Amqp1ContainerConnection.MaxWaitingDeliveries, Bodies(written, handle: 0));
        CollectionAssert.AreEqual(FourMessages, Bodies(written, handle: 1));
        Assert.IsTrue(DeliveryCounts(written).All(count => count == 0));
    }

    [TestMethod]
    [DataRow(1u, false, DisplayName = "the message uses the last credit, so no drain flow follows")]
    [DataRow(5u, true, DisplayName = "the credit left is drained after the message")]
    public async Task ServeAsync_DrainOfASubscriptionQueueWhoseFirstMessageIsRefused_SendsTheNextWithoutUsingCreditOnTheRefused(uint credit, bool drained)
    {
        var bus = Bus();
        var attach = DurableAttach(0, "/topic/news", "never", filters: SelectorFilter("color = 'red'"));
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(attach), BusFrame(new Amqp1Detach(0)));
        foreach (var color in new[] { "blue", "red" })
        {
            bus.Publish(new BusMessage("news", Encoding.UTF8.GetBytes(color)) { Headers = [new("color", color)] });
        }

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(attach with { SenderSettleMode = 1 }),
            BusFrame(Credit(0, deliveryCount: 0, credit: credit) with { Drain = true }));

        CollectionAssert.AreEqual(new[] { "red" }, Bodies(written));
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 0, 0, bus.Connect("alice").InspectQueue(DurableQueue).SubscriptionDescription), bus.Connect("alice").InspectQueue(DurableQueue));
        var afterAttach = written.Skip(3).Select(frame => frame.Performative).ToList();
        Assert.IsInstanceOfType<Amqp1Transfer>(afterAttach[0]);
        Assert.HasCount(drained ? 2 : 1, afterAttach);
        if (drained)
        {
            var flow = (Amqp1Flow)afterAttach[1]!;
            Assert.AreEqual((true, 5u, 0u, 0u), (flow.Drain, flow.DeliveryCount, flow.LinkCredit, flow.Available));
        }
    }

    [TestMethod]
    [DataRow(false, 1u, 2u, DisplayName = "after one is sent")]
    [DataRow(true, 0u, 3u, DisplayName = "a drain while the session's window is closed")]
    public async Task ServeAsync_QueueLinksFlow_StatesTheQueuesReadyCountAsAvailable(bool windowClosed, uint transfers, uint available)
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one", "two", "three");
        var flow = Credit(0, deliveryCount: 0, credit: 1) with { Echo = true };

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "orders")),
            BusFrame(windowClosed ? flow with { IncomingWindow = 0, Drain = true } : flow));

        Assert.HasCount((int)transfers, Transfers(written));
        Assert.AreEqual(available, ((Amqp1Flow)written[^1].Performative!).Available);
    }

    [TestMethod]
    public async Task ServeAsync_LiveTopicLinksFlow_StatesWhatWaitsOnTheLinkAsAvailable()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "/topic/news")),
            BusFrame(SenderAttach(1, "/topic/news")),
            Publication(1, 0, "waiting"),
            BusFrame(Credit(0, deliveryCount: 0, credit: 0) with { Echo = true }));

        Assert.AreEqual(1u, ((Amqp1Flow)written[^1].Performative!).Available);
    }

    [TestMethod]
    public async Task ServeAsync_TransactionalFlowBeforeAMessageIsQueued_AcquiresItAsItIsSent()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            [
                .. AcquisitionFrames("orders"),
                BusFrame(SenderAttach(2, "orders")),
                Publication(2, 1, "later"),
            ]);

        AssertSameState(TransactionalState(FirstTransactionId), Transfers(written).Single().Transfer.State);
    }

    // One of two competing receivers of the kind: the shared subscriptions' links are named "group|0" and "group|1".
    private static Amqp1Attach CompetingAttach(string kind, uint handle) => kind switch
    {
        PlainQueueLinks => ReceiverAttach(handle, "/queue/orders"),
        KeptSharedLinks => SharedAttach(handle, $"group|{handle}", durable: 2, policy: "never"),
        _ => SharedAttach(handle, $"group|{handle}"),
    };

    private static string CompetingQueue(string kind) => kind switch
    {
        PlainQueueLinks => "orders",
        KeptSharedLinks => Amqp1SubscriptionQueue.QueueNameFor("client", "group|0", isShared: true, isGlobal: false),
        _ => NonDurableSharedQueue,
    };

    // The bus's own publish of each body, to the queue or to the topic the subscriptions are bound to.
    private static void PublishToCompetingLinks(MessageBus.MessageBus bus, string kind, string[] bodies)
    {
        foreach (var body in bodies)
        {
            var message = new BusMessage(kind == PlainQueueLinks ? "orders" : "news", Encoding.UTF8.GetBytes(body));
            if (kind == PlainQueueLinks)
            {
                Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("orders", message));
            }
            else
            {
                Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(message).Outcome);
            }
        }
    }

    private static byte[] OnSecondSession(Amqp1Performative performative, ReadOnlyMemory<byte> payload = default) =>
        Amqp1Frame.Carrying(1, performative, payload).Encode();

    // Each delivery's header delivery-count, 0 with no header.
    private static uint[] DeliveryCounts(List<(Amqp1Performative? Performative, byte[] Payload)> written) =>
        [.. Transfers(written).Select(transfer => Amqp1MessageSection.ReadAll(transfer.Payload)
            .Where(section => section.DescriptorCode == Amqp1Descriptor.Header)
            .Select(section => Amqp1Header.FromDescribed(section.Described).DeliveryCount)
            .SingleOrDefault())];
}
