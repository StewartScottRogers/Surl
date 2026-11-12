using System.Text;
using Surl.Content;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Flow, session windows, the client's dispositions, refused links and refused messages (AMQP 1.0 Part 2
/// sections 2.5.6, 2.6.7, 2.6.12, 2.7.4 to 2.7.6; Part 3 section 3.4; ADR-0107 decisions 5 to 7).
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    private const uint Window = Amqp1ContainerConnection.ContainerWindow;

    [TestMethod]
    public async Task ServeAsync_SessionFlowWithEcho_IsAnsweredWithTheSessionsFlow()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(new Amqp1Flow(100, 0, 100) { Echo = true }),
            BusFrame(new Amqp1Flow(100, 0, 100) { NextIncomingId = 0 }));

        Assert.AreEqual(new Amqp1Flow(Window, 0, Window) { NextIncomingId = 0 }, written[2].Performative);
        Assert.HasCount(3, written);
    }

    [TestMethod]
    public async Task ServeAsync_SenderFlowWithEcho_TakesItsDeliveryCountAndIsAnswered()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "orders")),
            BusFrame(new Amqp1Flow(100, 0, 100) { Handle = 0, DeliveryCount = 7, Echo = true }),
            BusFrame(new Amqp1Flow(100, 0, 100) { Handle = 0 }));

        Assert.AreEqual(
            new Amqp1Flow(Window, 0, Window) { NextIncomingId = 0, Handle = 0, DeliveryCount = 7, LinkCredit = 93 },
            written[4].Performative);
        Assert.HasCount(5, written);
    }

    [TestMethod]
    public async Task ServeAsync_SenderFlowPastItsCredit_IsAnsweredWithNoCredit()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "orders")),
            BusFrame(new Amqp1Flow(100, 0, 100) { Handle = 0, DeliveryCount = 150, Echo = true }));

        Assert.AreEqual(0u, ((Amqp1Flow)written.Last().Performative!).LinkCredit);
    }

    [TestMethod]
    public async Task ServeAsync_SenderDownToHalfItsCredit_IsGivenCreditBackToAHundred()
    {
        var transfers = Enumerable.Range(0, 51)
            .Select(index => BusFrame(new Amqp1Transfer(0) { DeliveryId = (uint)index, DeliveryTag = [(byte)index], Settled = true }, Message));

        var (written, _) = await ServeOnBusAsync(Bus(), [Open(), Begin(), BusFrame(SenderAttach(0, "/topic/news")), .. transfers]);

        var flows = written.Select(frame => frame.Performative).OfType<Amqp1Flow>().ToList();
        Assert.HasCount(2, flows);
        Assert.AreEqual(new Amqp1Flow(Window, 0, Window) { NextIncomingId = 50, Handle = 0, DeliveryCount = 50, LinkCredit = 100 }, flows[1]);
    }

    [TestMethod]
    public async Task ServeAsync_ReceiverFlowWithDrain_SendsWhatItHasThenGivesBackTheRestOfTheCredit()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "only");

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "orders")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 5) with { Drain = true }),
            BusFrame(new Amqp1Flow(100, 0, 100) { Handle = 0, Echo = true }));

        Assert.IsInstanceOfType<Amqp1Transfer>(written[3].Performative);
        Assert.AreEqual(
            new Amqp1Flow(Window, 1, Window) { NextIncomingId = 0, Handle = 0, DeliveryCount = 5, LinkCredit = 0, Available = 0, Drain = true },
            written[4].Performative);
        Assert.AreEqual(
            new Amqp1Flow(Window, 1, Window) { NextIncomingId = 0, Handle = 0, DeliveryCount = 5, LinkCredit = 0, Available = 0 },
            written[5].Performative);
    }

    [TestMethod]
    public async Task ServeAsync_ClientIncomingWindowClosed_WaitsForTheSessionFlowThatOpensIt()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "later");

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            BusFrame(new Amqp1Begin(0, 0, 100)),
            BusFrame(ReceiverAttach(0, "orders")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 5) with { IncomingWindow = 0 }),
            BusFrame(new Amqp1Flow(10, 0, 100) { NextIncomingId = 0 }));

        Assert.IsInstanceOfType<Amqp1Attach>(written[2].Performative);
        Assert.IsInstanceOfType<Amqp1Transfer>(written[3].Performative);
        Assert.HasCount(4, written);
    }

    [TestMethod]
    public async Task ServeAsync_DeliveryLargerThanTheClientsWindow_IsWrittenFrameByFrameAsTheWindowOpens()
    {
        var bus = Bus();
        Enqueue(bus, "orders", new string('x', 1000));

        var (written, _) = await ServeOnBusAsync(
            bus,
            Amqp1Frame.Carrying(0, new Amqp1Open("client") { MaxFrameSize = 512 }).Encode(),
            BusFrame(new Amqp1Begin(0, 1, 100)),
            BusFrame(ReceiverAttach(0, "orders")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 5) with { IncomingWindow = 1 }),
            BusFrame(new Amqp1Flow(1, 0, 100) { NextIncomingId = 1 }),
            BusFrame(new Amqp1Flow(1, 0, 100) { NextIncomingId = 2 }));

        var transfers = written.Select((frame, index) => (frame.Performative, index)).Where(frame => frame.Performative is Amqp1Transfer).ToList();
        CollectionAssert.AreEqual(new[] { true, true, false }, transfers.Select(frame => ((Amqp1Transfer)frame.Performative!).More).ToArray());
        Assert.IsInstanceOfType<Amqp1Attach>(written[2].Performative);
        Assert.HasCount(6, written);
    }

    [TestMethod]
    [DataRow(false, 1000, DisplayName = "none settled")]
    [DataRow(true, 1001, DisplayName = "one settled")]
    public async Task ServeAsync_ThousandUnsettledTopicDeliveries_HoldTheNextUntilOneIsSettled(bool settleOne, int expectedTransfers)
    {
        var sends = Enumerable.Range(0, Amqp1ContainerConnection.MaxWaitingDeliveries + 1)
            .Select(index => BusFrame(new Amqp1Transfer(1) { DeliveryId = (uint)index, DeliveryTag = BitConverter.GetBytes(index), Settled = true }, Message));

        var (written, _) = await ServeOnBusAsync(
            Bus(),
            [
                Open(),
                BusFrame(new Amqp1Begin(0, 5000, 5000)),
                BusFrame(ReceiverAttach(0, "/topic/news")),
                BusFrame(Credit(0, deliveryCount: 0, credit: 2000) with { IncomingWindow = 5000 }),
                BusFrame(SenderAttach(1, "/topic/news")),
                .. sends,
                .. (settleOne ? [BusFrame(new Amqp1Disposition(true, 0) { Settled = true, State = Amqp1DeliveryState.Accepted })] : Array.Empty<byte[]>()),
            ]);

        Assert.HasCount(expectedTransfers, Transfers(written));
    }

    [TestMethod]
    [DataRow(false, 1000, DisplayName = "none settled")]
    [DataRow(true, 1001, DisplayName = "one settled")]
    public async Task ServeAsync_ThousandUnsettledQueueDeliveries_HoldTheNextUntilOneIsSettled(bool settleOne, int expectedTransfers)
    {
        var bus = Bus();
        Enqueue(bus, "orders", [.. Enumerable.Range(0, Amqp1ContainerConnection.MaxWaitingDeliveries + 1).Select(index => $"message-{index}")]);

        var (written, _) = await ServeOnBusAsync(
            bus,
            [
                Open(),
                BusFrame(new Amqp1Begin(0, 5000, 5000)),
                BusFrame(ReceiverAttach(0, "orders")),
                BusFrame(Credit(0, deliveryCount: 0, credit: 2000) with { IncomingWindow = 5000 }),
                .. (settleOne ? [BusFrame(new Amqp1Disposition(true, 0) { Settled = true, State = Amqp1DeliveryState.Accepted })] : Array.Empty<byte[]>()),
            ]);

        Assert.HasCount(expectedTransfers, Transfers(written));
    }

    [TestMethod]
    public async Task ServeAsync_MessageLargerThanTheClientsFrames_IsSentInMoreFrames()
    {
        var bus = Bus();
        var body = new string('x', 1000);
        Enqueue(bus, "orders", body);

        var (written, _) = await ServeOnBusAsync(
            bus,
            Amqp1Frame.Carrying(0, new Amqp1Open("client") { MaxFrameSize = 512 }).Encode(),
            Begin(),
            BusFrame(ReceiverAttach(0, "orders")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 5)));

        var transfers = Transfers(written);
        CollectionAssert.AreEqual(new[] { true, true, false }, transfers.Select(transfer => transfer.Transfer.More).ToArray());
        Assert.AreEqual(0u, transfers[0].Transfer.DeliveryId);
        Assert.IsNull(transfers[1].Transfer.DeliveryId);
        Assert.AreEqual(body, DataOf([.. transfers.SelectMany(transfer => transfer.Payload)]));
    }

    [TestMethod]
    public async Task ServeAsync_TopicDeliveries_ArePreSettledOrSettledByTheClientWithNothingToPutBack()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "/topic/news", senderSettleMode: 1)),
            BusFrame(Credit(0, deliveryCount: 0, credit: 5)),
            BusFrame(ReceiverAttach(1, "/topic/news")),
            BusFrame(Credit(1, deliveryCount: 0, credit: 5)),
            BusFrame(SenderAttach(2, "/topic/news")),
            BusFrame(new Amqp1Transfer(2) { DeliveryId = 0, DeliveryTag = [1], Settled = true }, Message),
            BusFrame(new Amqp1Disposition(true, 1) { Settled = true, State = Amqp1DeliveryState.Accepted }),
            BusFrame(new Amqp1Disposition(true, 1) { Settled = true, State = Amqp1DeliveryState.Accepted }));

        CollectionAssert.AreEqual(new bool?[] { true, false }, Transfers(written).Select(transfer => transfer.Transfer.Settled).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ReceiverSettlingSecond_IsSettledByTheContainerOnceItStatesAnOutcome()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one", "two", "three");

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "orders") with { ReceiverSettleMode = 1 }),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(new Amqp1Disposition(false, 0) { Settled = true, State = Amqp1DeliveryState.Accepted }),
            BusFrame(new Amqp1Disposition(true, 0) { Last = 1, State = Amqp1DeliveryState.Accepted }),
            BusFrame(new Amqp1Disposition(true, 2) { State = State(Amqp1Descriptor.Received) }),
            BusFrame(new Amqp1Disposition(true, 2) { Settled = true }));

        var settledByContainer = written.Select(frame => frame.Performative).OfType<Amqp1Disposition>().ToList();
        CollectionAssert.AreEqual(
            new[]
            {
                new Amqp1Disposition(false, 0) { Settled = true, State = Amqp1DeliveryState.Accepted },
                new Amqp1Disposition(false, 1) { Settled = true, State = Amqp1DeliveryState.Accepted },
            },
            settledByContainer);
        CollectionAssert.AreEqual(new[] { "one", "two", "three", "three" }, Transfers(written).Select(transfer => DataOf(transfer.Payload)).ToArray());
        Assert.AreEqual("three", Encoding.UTF8.GetString(bus.Connect().GetFromQueue("orders").Delivery!.Message.Payload.Span));
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_ModifiedUndeliverableHere_IsNotRedeliveredToTheLinkAndGoesBackWhenItEnds()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "elsewhere");
        var undeliverableHere = new Amqp1Described(Amqp1Descriptor.Modified, new List<object?> { null, true });

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "orders")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(new Amqp1Disposition(true, 0) { State = undeliverableHere }),
            BusFrame(new Amqp1Disposition(true, 0) { Settled = true }));

        Assert.HasCount(1, Transfers(written));
        Assert.IsTrue(bus.Connect().GetFromQueue("orders").Delivery!.Redelivered);
    }

    [TestMethod]
    public async Task ServeAsync_ModifiedDelivery_IsRedelivered()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "again");

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "orders")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(new Amqp1Disposition(true, 0) { Settled = true, State = State(Amqp1Descriptor.Modified) }));

        Assert.HasCount(2, Transfers(written));
    }

    [TestMethod]
    public async Task ServeAsync_TransferOnALinkTheClientReceivesOn_DetachesWithIllegalStateAndIgnoresItsFrames()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "/topic/news")),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [1] }, Message),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 1, DeliveryTag = [2] }, Message),
            BusFrame(Credit(0, deliveryCount: 0, credit: 5)),
            BusFrame(new Amqp1Detach(0) { Closed = true }));

        Assert.AreEqual(
            new Amqp1Detach(0) { Closed = true, Error = new(Amqp1ErrorCondition.IllegalState) { Description = "a transfer on a link the client receives on" } },
            written[3].Performative);
        Assert.HasCount(4, written);
    }

    [TestMethod]
    [DataRow(null, null, "a continuation with no delivery open", DisplayName = "a first transfer without a delivery-id")]
    [DataRow(1u, null, "a continuation of another delivery", DisplayName = "a continuation with another delivery-id")]
    [DataRow(0u, (byte)9, "a continuation of another delivery", DisplayName = "a continuation with another delivery tag")]
    public async Task ServeAsync_TransferNamingNoOpenDelivery_DetachesWithInvalidField(uint? continuationId, byte? continuationTag, string description)
    {
        var first = continuationId is null
            ? new Amqp1Transfer(0)
            : new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [1], More = true };
        byte[]? tag = continuationTag is { } octet ? [octet] : null;

        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "orders")),
            BusFrame(first, Message.AsMemory(0, 5)),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = continuationId, DeliveryTag = tag }, Message.AsMemory(5)));

        Assert.AreEqual(
            new Amqp1Detach(0) { Closed = true, Error = new(Amqp1ErrorCondition.InvalidField) { Description = description } },
            written.Last().Performative);
    }

    [TestMethod]
    public async Task ServeAsync_MessageLargerThanTheBusTakes_DetachesWithMessageSizeExceeded()
    {
        var (written, _) = await ServeOnBusAsync(
            new MessageBus.MessageBus(new ManualTimeProvider(), maxMessageBytes: 20),
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "orders")),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [1] }, Message));

        Assert.AreEqual(
            new Amqp1Detach(0) { Closed = true, Error = new(Amqp1ErrorCondition.MessageSizeExceeded) { Description = "message size exceeded" } },
            written.Last().Performative);
    }

    [TestMethod]
    public async Task ServeAsync_PartialDeliveriesTogetherLargerThanTheBusTakes_CloseWithResourceLimitExceeded()
    {
        var (written, _) = await ServeOnBusAsync(
            new MessageBus.MessageBus(new ManualTimeProvider(), maxMessageBytes: 20),
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "orders")),
            BusFrame(SenderAttach(1, "orders")),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [1], More = true }, Message.AsMemory(0, 15)),
            BusFrame(new Amqp1Transfer(1) { DeliveryId = 1, DeliveryTag = [2], More = true }, Message.AsMemory(0, 15)));

        Assert.AreEqual(ClosedWith(Amqp1ErrorCondition.ResourceLimitExceeded, "partial deliveries exceed the message size"), written.Last().Performative);
    }

    [TestMethod]
    [DataRow(0, "amqp:not-implemented", "message format not supported", DisplayName = "a message format other than 0")]
    [DataRow(1, "amqp:decode-error", "a message that does not decode", DisplayName = "bytes that are no message")]
    [DataRow(2, "amqp:unauthorized-access", "not authorized", DisplayName = "topic access denies the publish")]
    [DataRow(3, "amqp:resource-limit-exceeded", "resource limit exceeded", DisplayName = "a full queue")]
    [DataRow(4, "amqp:resource-limit-exceeded", "resource limit exceeded", DisplayName = "too many topics")]
    [DataRow(5, "amqp:resource-limit-exceeded", "resource limit exceeded", DisplayName = "a full store")]
    [DataRow(6, "amqp:resource-limit-exceeded", "resource limit exceeded", DisplayName = "a message and its native form too large")]
    [DataRow(7, "amqp:not-found", "no such node", DisplayName = "a queue that is gone")]
    public async Task ServeAsync_MessageTheBusRefuses_IsRejected(int refusal, string condition, string description)
    {
        var clock = new ManualTimeProvider();
        var (bus, address, messageFormat, message) = refusal switch
        {
            0 => (Bus(), "/topic/news", 1u, Message),
            1 => (Bus(), "/topic/news", 0u, new byte[] { 0x40 }),
            2 => (Bus(new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("#", "bob", TopicAccessRights.PublishAndSubscribe)] }), "/topic/news", 0u, Message),
            3 => (new MessageBus.MessageBus(clock, maxMessagesPerQueue: 1), "/queue/orders", 0u, Message),
            4 => (new MessageBus.MessageBus(clock, maxTopics: 1), "/topic/news", 0u, Message),
            5 => (new MessageBus.MessageBus(clock, maxLogBytes: 10), "/topic/news", 0u, Message),
            6 => (new MessageBus.MessageBus(clock, maxMessageBytes: Message.Length), "/topic/news", 0u, Message),
            _ => (Bus(), "/queue/missing", 0u, Message),
        };
        if (refusal == 3)
        {
            Enqueue(bus, "orders", "already");
        }

        if (refusal == 4)
        {
            Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(new BusMessage("other", Array.Empty<byte>())).Outcome);
        }

        var nodes = new UnitTestAmqp1NodeDirectory(_ => Amqp1NodeResolution.Found(address));

        var (written, _) = await ServeOnBusAsync(
            bus,
            nodes,
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "anything")),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [1], MessageFormat = messageFormat }, message));

        var rejected = new Amqp1Error(new Amqp1Symbol(condition)) { Description = description };
        AssertSameFrame(new Amqp1Disposition(true, 0) { Settled = true, State = Amqp1DeliveryState.Rejected(rejected) }, written.Last().Performative);
    }

    [TestMethod]
    public async Task ServeAsync_PreSettledMessageThatWouldBeRejected_IsDroppedAndNoted()
    {
        var (written, log) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "orders") with { SenderSettleMode = 1 }),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [1] }, new byte[] { 0x40 }));

        Assert.IsFalse(written.Any(frame => frame.Performative is Amqp1Disposition));
        CollectionAssert.Contains(log.Notes.ToList(), "A pre-settled message was dropped: amqp:decode-error: a message that does not decode.");
    }

    [TestMethod]
    public async Task ServeAsync_BusThatCannotSaveItsFiles_StillAcceptsAndNotesIt()
    {
        var bus = Bus();
        await bus.LoadAsync(new MessageBusFiles(new UnitTestUnwritableContentFileSystem(), "/data/.surl"), TestContext.CancellationToken);

        var (written, log) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "/topic/news")),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [1] }, Message));

        Assert.AreEqual(new Amqp1Disposition(true, 0) { Settled = true, State = Amqp1DeliveryState.Accepted }, written.Last().Performative);
        CollectionAssert.Contains(log.Notes.ToList(), "The message bus could not save its files; the message is held in memory.");
    }

    [TestMethod]
    [DataRow(0, "amqp:unauthorized-access", "not authorized", DisplayName = "topic access denies the subscription")]
    [DataRow(1, "amqp:unauthorized-access", "not authorized", DisplayName = "a queue exclusive to another connection")]
    [DataRow(2, "amqp:unauthorized-access", "not authorized", DisplayName = "a queue another consumer holds exclusively")]
    [DataRow(3, "amqp:resource-limit-exceeded", "resource limit exceeded", DisplayName = "too many subscriptions")]
    [DataRow(4, "amqp:not-found", "no such node", DisplayName = "a filter the bus refuses")]
    public async Task ServeAsync_ReceiverTheBusRefuses_IsRefusedAtAttach(int refusal, string condition, string description)
    {
        var clock = new ManualTimeProvider();
        var bus = refusal switch
        {
            0 => Bus(new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("#", "alice", TopicAccessRights.Publish)] }),
            3 => new MessageBus.MessageBus(clock, maxSubscriptionsPerConnection: 1),
            _ => Bus(),
        };
        var other = bus.Connect("bob");
        if (refusal == 1)
        {
            other.DeclareQueue("orders", new BusQueueFlags(Exclusive: true));
        }

        if (refusal == 2)
        {
            other.DeclareQueue("orders", new BusQueueFlags(Durable: true));
            other.Consume("orders", _ => { }, exclusive: true).Consumer!.Start();
        }

        var nodes = new UnitTestAmqp1NodeDirectory(request => refusal switch
        {
            0 => Amqp1NodeResolution.Found("/topic/news"),
            4 => Amqp1NodeResolution.Found("/topic/a+b"),
            _ => Amqp1NodeResolution.Found("/queue/orders"),
        });
        if (refusal == 3)
        {
            nodes = new UnitTestAmqp1NodeDirectory(_ => Amqp1NodeResolution.Found("/topic/news"));
        }

        var (written, _) = await ServeOnBusAsync(
            bus,
            nodes,
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "first")),
            BusFrame(ReceiverAttach(1, "second")));

        Assert.AreEqual(
            new Amqp1Detach(refusal == 3 ? 1u : 0u) { Closed = true, Error = new(new Amqp1Symbol(condition)) { Description = description } },
            written.First(frame => frame.Performative is Amqp1Detach).Performative);
    }

    [TestMethod]
    public async Task ServeAsync_TopicMessagesPastTheLinksWaitingBound_AreDroppedAndNoted()
    {
        var bus = Bus();
        var connection = new InMemoryConnection([Open(), Begin(), BusFrame(ReceiverAttach(0, "/topic/#"))], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new Amqp1ContainerConnection(connection, Context(log, cancellation.Token), Options(), bus, "alice").ServeAsync();
        await WaitUntilWrittenAsync(async () => (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Any(frame => frame.Performative is Amqp1Attach));

        for (var index = 0; index <= Amqp1ContainerConnection.MaxWaitingDeliveries; index++)
        {
            bus.Publish(new BusMessage("news", Array.Empty<byte>()));
        }

        await cancellation.CancelAsync();
        await serving;
        CollectionAssert.Contains(log.Notes.ToList(), "Link 0 dropped 1 topic messages past 1000 waiting.");
    }

    [TestMethod]
    public async Task ServeAsync_TopicMessageTheClientCannotName_IsNotSent()
    {
        var bus = Bus(new MessageBusConfiguration { TopicMaps = [new TopicMap("amqp1", "/topic/alias", "mqtt", "real")] });
        var connection = new InMemoryConnection(
            [Open(), Begin(), BusFrame(ReceiverAttach(0, "/topic/#")), BusFrame(Credit(0, deliveryCount: 0, credit: 5))],
            peerHalfClosesWhenExhausted: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new Amqp1ContainerConnection(connection, Context(new RecordingExchangeLog(), cancellation.Token), Options(), bus, "alice").ServeAsync();
        await WaitUntilWrittenAsync(async () => (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Any(frame => frame.Performative is Amqp1Attach));

        bus.Publish(new BusMessage("alias", Array.Empty<byte>()));
        bus.Publish(new BusMessage("other", Array.Empty<byte>()));

        await WaitUntilWrittenAsync(async () => Transfers(await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Count == 1);
        await cancellation.CancelAsync();
        await serving;
        var payload = Transfers(await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Single().Payload;
        Assert.AreEqual("/topic/other", Amqp1Properties.FromDescribed(Amqp1MessageSection.ReadAll(payload)[0].Described).To);
    }

    // Performatives holding arrays compare by their encoding.
    private static void AssertSameFrame(Amqp1Performative expected, Amqp1Performative? actual) =>
        Assert.AreEqual(Convert.ToHexString(Amqp1Frame.Carrying(0, expected).Encode()), Convert.ToHexString(Amqp1Frame.Carrying(0, actual!).Encode()));

    private async Task<(List<(Amqp1Performative? Performative, byte[] Payload)> Written, RecordingExchangeLog Log)> ServeOnBusAsync(
        MessageBus.MessageBus bus, UnitTestAmqp1NodeDirectory nodes, params byte[][] frames)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(frames.Select(frame => (ReadOnlyMemory<byte>)frame));

        await new Amqp1ContainerConnection(connection, Context(log, TestContext.CancellationToken), Options(), bus, "alice", nodes.On).ServeAsync();

        return (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes), log);
    }

    /// <summary>A file system whose every write fails, as a full or read-only disk does.</summary>
    private sealed class UnitTestUnwritableContentFileSystem : IContentFileSystem
    {
        private readonly InMemoryContentFileSystem files = new(new ManualTimeProvider());

        public ContentEntryKind GetEntryKind(string path) => files.GetEntryKind(path);

        public string ResolveFinalPath(string path) => files.ResolveFinalPath(path);

        public long GetFileLength(string path) => files.GetFileLength(path);

        public DateTimeOffset GetLastWriteTimeUtc(string path) => files.GetLastWriteTimeUtc(path);

        public Stream OpenFileForAsyncRead(string path) => files.OpenFileForAsyncRead(path);

        public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => files.EnumerateDirectoryEntryNames(path);

        public Stream CreateFileForAsyncWrite(string path) => throw new IOException("The disk is full.");

        public Stream OpenFileForAsyncReadWrite(string path) => throw new IOException("The disk is full.");

        public void CreateDirectory(string path) => files.CreateDirectory(path);

        public void DeleteFile(string path) => files.DeleteFile(path);

        public void MoveFileReplacing(string source, string destination) => files.MoveFileReplacing(source, destination);
    }
}
