using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// The bounds on what a connection's open transactions keep (ADR-0006; ADR-0146, BL-2125 note to
/// decisions 1.4 and 1.5): the node names of posted messages, the deliveries acquired and the outcomes
/// held.
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    // The deliveries a test's client takes at a time: as many as a link may hold unsettled.
    private const uint Round = Amqp1ContainerConnection.MaxWaitingDeliveries;

    [TestMethod]
    [DataRow(0L, DisplayName = "--max-filesize 0")]
    [DataRow(1_048_576L, DisplayName = "--max-filesize 1048576")]
    public async Task ServeAsync_PostingsFromLinksAttachedAgainWithLongNodeNames_AreRefusedPastMaxHeldNodeNameChars(long maxUploadBytes)
    {
        // One topic name, each attach of it a link with its own string. As many as fit are held, a second
        // posting from the first link adding nothing; the next link's posting is refused, a short name
        // still fits, and the rollback frees the room.
        var longName = "/topic/" + new string('n', 60_000);
        var fitting = (uint)(Amqp1ContainerConnection.MaxHeldNodeNameChars / 60_000);
        var frames = TransactionFrames().ToList();
        frames.RemoveAt(3);
        var deliveryId = 0u;
        void PostFromANewLink(string address, int postings = 1, byte[]? transactionId = null)
        {
            frames.Add(BusFrame(SenderAttach(1, address)));
            for (var posting = 0; posting < postings; posting++)
            {
                frames.Add(Posted(1, ++deliveryId, TransactionalState(transactionId ?? FirstTransactionId), messageHex: string.Empty));
            }

            frames.Add(BusFrame(new Amqp1Detach(1) { Closed = true }));
        }

        PostFromANewLink(longName, postings: 2);
        for (var link = 1u; link < fitting; link++)
        {
            PostFromANewLink(longName);
        }

        PostFromANewLink(longName);
        PostFromANewLink("/topic/short");
        frames.Add(Control(0, ++deliveryId, DischargeRollBackHex));
        frames.Add(Control(0, ++deliveryId, DeclareHex));
        PostFromANewLink(longName, transactionId: [0, 0, 0, 2]);

        var (written, _) = await ServeWithLimitsAsync(Bus(), ExchangeLimits.Default with { MaxUploadBytes = maxUploadBytes }, [.. frames]);

        var dispositions = Dispositions(written);
        var refused = Rejected(Amqp1ErrorCondition.ResourceLimitExceeded, "too many node names held in open transactions");
        var refusedId = fitting + 2;
        AssertSameFrame(Settled(refusedId - 1, InTransaction(Amqp1DeliveryState.Accepted)), dispositions[(int)refusedId - 1]);
        AssertSameFrame(Settled(refusedId, InTransaction(refused)), dispositions[(int)refusedId]);
        AssertSameFrame(Settled(refusedId + 1, InTransaction(Amqp1DeliveryState.Accepted)), dispositions[(int)refusedId + 1]);
        AssertSameFrame(
            Settled(deliveryId, Amqp1DeliveryState.TransactionalState([0, 0, 0, 2], Amqp1DeliveryState.Accepted)),
            dispositions[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_AcquisitionPastMaxAcquiredDeliveries_SendsNothingMoreInTheTransactionUntilALinkEnds()
    {
        // The client settles each round outside the transaction, which frees the link's 1000 unsettled
        // but not the transaction's room.
        const uint Full = Amqp1ContainerConnection.MaxAcquiredDeliveries;
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxMessagesPerQueue: (int)Full + 3);
        Enqueue(bus, "orders", [.. Enumerable.Range(0, (int)Full + 3).Select(index => $"message-{index}")]);
        List<byte[]> frames =
        [
            Open(),
            BusFrame(new Amqp1Begin(0, 200_000, 200_000)),
            BusFrame(CoordinatorAttach(0)),
            Control(0, 0, DeclareHex),
            BusFrame(ReceiverAttach(1, "orders")),
        ];
        for (var first = 0u; first < Full; first += Round)
        {
            frames.Add(AcquiringCredit(1, deliveryCount: first, credit: Round));
            frames.Add(BusFrame(new Amqp1Disposition(true, first) { Last = first + Round - 1, Settled = true, State = Amqp1DeliveryState.Accepted }));
        }

        frames.AddRange(
        [
            BusFrame(WideCredit(1, deliveryCount: Full, credit: 10) with { Drain = true, Properties = AcquireInFirstTransaction }),
            BusFrame(ReceiverAttach(2, "orders")),
            AcquiringCredit(2, deliveryCount: 0, credit: 1),
            BusFrame(ReceiverAttach(3, "orders")),
            BusFrame(WideCredit(3, deliveryCount: 0, credit: 1)),
            BusFrame(ReceiverAttach(4, "orders", senderSettleMode: 1)),
            AcquiringCredit(4, deliveryCount: 0, credit: 1),
            BusFrame(new Amqp1Detach(1) { Closed = true }),
        ]);

        var (written, _) = await ServeOnBusAsync(bus, [.. frames]);

        var sent = Transfers(written);
        Assert.HasCount((int)Full + 3, sent);

        // A drain of a link at the bound gives its credit back.
        var drained = written.Select(frame => frame.Performative).OfType<Amqp1Flow>().Single(flow => flow.Drain);
        Assert.AreEqual(Full + 10, drained.DeliveryCount);
        Assert.AreEqual(0u, drained.LinkCredit);

        // At the bound the acquiring links send nothing, so the next message goes to the link outside
        // the transaction.
        var outside = sent[(int)Full];
        Assert.IsNull(outside.Transfer.State);
        Assert.AreEqual(3u, outside.Transfer.Handle);
        Assert.AreEqual($"message-{Full}", DataOf(outside.Payload));

        // A pre-settled link's delivery is kept by no transaction, so the bound does not stop it.
        var preSettled = sent[(int)Full + 1];
        Assert.AreEqual(4u, preSettled.Transfer.Handle);
        AssertSameState(TransactionalState(FirstTransactionId), preSettled.Transfer.State);

        // The first link's end frees its share, and the second link acquires.
        var performatives = written.Select(frame => frame.Performative).ToList();
        var acquired = sent[(int)Full + 2];
        Assert.AreEqual(2u, acquired.Transfer.Handle);
        AssertSameState(TransactionalState(FirstTransactionId), acquired.Transfer.State);
        Assert.IsGreaterThan(performatives.FindIndex(performative => performative is Amqp1Detach), performatives.IndexOf(acquired.Transfer));
    }

    [TestMethod]
    public async Task ServeAsync_TransactionalDispositionPastMaxHeldOutcomes_IsRejectedWithResourceLimitExceededAndChangesNothing()
    {
        // A live topic subscription's deliveries are on no queue, so only this bound limits how many
        // outcomes are held for them. 101 links take each of 1000 messages: 101000 deliveries, none past
        // a link's 1000 unsettled.
        const uint Full = Amqp1ContainerConnection.MaxHeldOutcomes;
        const uint Receivers = (Full / Round) + 1;
        const uint Publisher = Receivers + 1;
        List<byte[]> frames =
        [
            Open(),
            BusFrame(new Amqp1Begin(0, 200_000, 200_000)),
            BusFrame(CoordinatorAttach(0)),
            Control(0, 0, DeclareHex),
        ];
        for (var handle = 1u; handle <= Receivers; handle++)
        {
            frames.Add(BusFrame(ReceiverAttach(handle, "/topic/news") with { ReceiverSettleMode = 1 }));
            frames.Add(BusFrame(WideCredit(handle, deliveryCount: 0, credit: Round)));
        }

        frames.Add(BusFrame(SenderAttach(Publisher, "/topic/news")));
        frames.AddRange(Enumerable.Range(0, (int)Round).Select(index =>
            BusFrame(new Amqp1Transfer(Publisher) { DeliveryId = (uint)index, DeliveryTag = [0], MessageFormat = 0, Settled = true }, Message)));
        frames.AddRange(
        [
            BusFrame(new Amqp1Disposition(true, 0) { Last = Full - 1, State = Amqp1DeliveryState.TransactionalState(FirstTransactionId, Amqp1DeliveryState.Accepted) }),
            RetireInTransaction(Full, Amqp1DeliveryState.Accepted, settled: true),
            RetireInTransaction(0, Released()),
            BusFrame(new Amqp1Detach(1) { Closed = true }),
            RetireInTransaction(Full, Amqp1DeliveryState.Accepted),
            Control(0, 1, DischargeRollBackHex),
            BusFrame(new Amqp1Disposition(true, Full) { State = Amqp1DeliveryState.Accepted }),
        ]);

        var (written, _) = await ServeOnBusAsync(Bus(), [.. frames]);

        // One refusal only: restating a held outcome adds none, and the first link's end freed the room
        // the refused delivery's outcome then took.
        var fromTheContainer = written.Select(frame => frame.Performative).OfType<Amqp1Disposition>().Where(disposition => !disposition.IsReceiver).ToList();
        Assert.HasCount(2, fromTheContainer);
        AssertSameFrame(
            new Amqp1Disposition(false, Full) { State = Rejected(Amqp1ErrorCondition.ResourceLimitExceeded, "too many outcomes held in open transactions") },
            fromTheContainer[0]);

        // The refused disposition settled nothing: the delivery is still unsettled for a later one to settle.
        AssertSameFrame(new Amqp1Disposition(false, Full) { Settled = true, State = Amqp1DeliveryState.Accepted }, fromTheContainer[1]);
    }

    [TestMethod]
    public async Task ServeAsync_CommitUnderSettleSecond_SettlesWithTheHeldOutcomeWithoutWhatThePeerSized()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");
        var rejected = Rejected(Amqp1ErrorCondition.DecodeError, new string('d', 1000));

        var (written, _) = await ServeOnBusAsync(
            bus, [.. AcquisitionFrames("orders", receiverSettleMode: 1, acquire: false), RetireInTransaction(0, rejected), Control(0, 1, DischargeCommitHex)]);

        var settledByContainer = written.Select(frame => frame.Performative).OfType<Amqp1Disposition>().Single(disposition => !disposition.IsReceiver);
        AssertSameState(State(Amqp1Descriptor.Rejected), settledByContainer.State);
        Assert.IsNull(LeftIn(bus));
    }

    private static Amqp1Flow WideCredit(uint handle, uint deliveryCount, uint credit) =>
        Credit(handle, deliveryCount, credit) with { IncomingWindow = 200_000 };

    private static byte[] AcquiringCredit(uint handle, uint deliveryCount, uint credit) =>
        BusFrame(WideCredit(handle, deliveryCount, credit) with { Properties = AcquireInFirstTransaction });
}
