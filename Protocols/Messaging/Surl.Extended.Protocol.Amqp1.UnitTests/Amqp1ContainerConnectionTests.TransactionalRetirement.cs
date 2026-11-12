using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Transactional retirement and acquisition (AMQP 1.0 Part 4 sections 4.4.2 and 4.4.3; ADR-0146
/// decision 1.5). A delivery a test leaves outstanding goes back to its queue when the connection ends,
/// so each test reads what is left in the queue afterwards to tell a held outcome from an applied one.
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    private static Amqp1Map AcquireInFirstTransaction => new([new(new Amqp1Symbol("txn-id"), FirstTransactionId)]);

    // A coordinator on handle 0 and transaction 00 00 00 01 declared, then a client receiver of the address on handle 1.
    private static byte[][] AcquisitionFrames(string address, byte receiverSettleMode = 0, uint credit = 10, bool acquire = true, params byte[][] more) =>
    [
        Open(),
        Begin(),
        BusFrame(CoordinatorAttach(0)),
        Control(0, 0, DeclareHex),
        BusFrame(ReceiverAttach(1, address) with { ReceiverSettleMode = receiverSettleMode }),
        BusFrame(Credit(1, deliveryCount: 0, credit: credit) with { Properties = acquire ? AcquireInFirstTransaction : null }),
        .. more,
    ];

    private static byte[] RetireInTransaction(uint deliveryId, Amqp1Described outcome, bool settled = false) =>
        BusFrame(new Amqp1Disposition(true, deliveryId) { Settled = settled, State = Amqp1DeliveryState.TransactionalState(FirstTransactionId, outcome) });

    private static byte[] Retire(uint deliveryId, Amqp1Described outcome) =>
        BusFrame(new Amqp1Disposition(true, deliveryId) { Settled = true, State = outcome });

    // Delivery states hold their fields in lists and arrays, which compare by reference: compare the bytes they encode to.
    private static void AssertSameState(Amqp1Described expected, object? actual) =>
        Assert.AreEqual(Convert.ToHexString(Amqp1Encoder.Encode(expected)), Convert.ToHexString(Amqp1Encoder.Encode(actual)));

    private static Amqp1Described Released() => State(Amqp1Descriptor.Released);

    private static Amqp1Described Modified() => State(Amqp1Descriptor.Modified);

    private static BusQueueDelivery? LeftIn(MessageBus.MessageBus bus) => bus.Connect().GetFromQueue("orders").Delivery;

    [TestMethod]
    [DataRow(Amqp1Descriptor.Accepted, false)]
    [DataRow(Amqp1Descriptor.Rejected, false)]
    [DataRow(Amqp1Descriptor.Released, false)]
    [DataRow(Amqp1Descriptor.Modified, false)]
    [DataRow(Amqp1Descriptor.Accepted, true)]
    public async Task ServeAsync_RetirementInATransactionNotYetDischarged_HasChangedNothingInTheQueue(ulong outcome, bool settled)
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        await ServeOnBusAsync(bus, [.. AcquisitionFrames("orders", acquire: false), RetireInTransaction(0, State(outcome), settled)]);

        Assert.IsNotNull(LeftIn(bus));
    }

    [TestMethod]
    [DataRow(Amqp1Descriptor.Accepted, false, false)]
    [DataRow(Amqp1Descriptor.Rejected, false, false)]
    [DataRow(Amqp1Descriptor.Released, false, true)]
    [DataRow(Amqp1Descriptor.Modified, false, true)]
    [DataRow(Amqp1Descriptor.Accepted, true, false)]
    [DataRow(Amqp1Descriptor.Released, true, true)]
    public async Task ServeAsync_RetirementInACommittedTransaction_IsAppliedAtTheCommit(ulong outcome, bool settled, bool returnsToTheQueue)
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        var (written, _) = await ServeOnBusAsync(
            bus, [.. AcquisitionFrames("orders", acquire: false), RetireInTransaction(0, State(outcome), settled), Control(0, 1, DischargeCommitHex)]);

        AssertSameFrame(Settled(1, Amqp1DeliveryState.Accepted), Dispositions(written).Last());
        Assert.AreEqual(returnsToTheQueue, LeftIn(bus) is not null);
    }

    [TestMethod]
    public async Task ServeAsync_RetirementStatedAgainInTheTransaction_ReplacesTheFirst()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        await ServeOnBusAsync(
            bus, [.. AcquisitionFrames("orders", acquire: false), RetireInTransaction(0, Amqp1DeliveryState.Accepted), RetireInTransaction(0, Released()), Control(0, 1, DischargeCommitHex)]);

        Assert.IsNotNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_DeliveryRetiredOutsideTheTransactionAfterItsHeldOutcome_IsNotChangedByTheCommit()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        await ServeOnBusAsync(
            bus, [.. AcquisitionFrames("orders", acquire: false), RetireInTransaction(0, Released()), Retire(0, Amqp1DeliveryState.Accepted), Control(0, 1, DischargeCommitHex)]);

        Assert.IsNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_HeldOutcomeThatStatesNoOutcome_ChangesNothingAtTheCommit()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");
        var noOutcome = BusFrame(new Amqp1Disposition(true, 0) { State = TransactionalState(FirstTransactionId) });

        await ServeOnBusAsync(bus, [.. AcquisitionFrames("orders", acquire: false), noOutcome, Control(0, 1, DischargeCommitHex)]);

        Assert.IsNotNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_CommitOfAnOutcomeStatedUnsettledUnderSettleSecond_SettlesTheDelivery()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        var (written, _) = await ServeOnBusAsync(
            bus, [.. AcquisitionFrames("orders", receiverSettleMode: 1, acquire: false), RetireInTransaction(0, Amqp1DeliveryState.Accepted), Control(0, 1, DischargeCommitHex)]);

        var settledByContainer = written.Select(frame => frame.Performative).OfType<Amqp1Disposition>().Single(disposition => !disposition.IsReceiver);
        Assert.AreEqual(new Amqp1Disposition(false, 0) { Settled = true, State = Amqp1DeliveryState.Accepted }, settledByContainer);
        Assert.IsNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_CommitOfAnOutcomeWhoseLinkHasEnded_SettlesNothing()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        var (written, _) = await ServeOnBusAsync(
            bus,
            [
                .. AcquisitionFrames("orders", receiverSettleMode: 1, acquire: false),
                RetireInTransaction(0, Amqp1DeliveryState.Accepted),
                BusFrame(new Amqp1Detach(1) { Closed = true }),
                Control(0, 1, DischargeCommitHex),
            ]);

        Assert.IsFalse(written.Any(frame => frame.Performative is Amqp1Disposition { IsReceiver: false }));
        Assert.IsNotNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_RollBackAfterASettledRetirement_LeavesTheDeliveryUnsettledOnItsLinkAgain()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        await ServeOnBusAsync(
            bus,
            [
                .. AcquisitionFrames("orders", acquire: false),
                RetireInTransaction(0, Amqp1DeliveryState.Accepted, settled: true),
                Control(0, 1, DischargeRollBackHex),
                Retire(0, Amqp1DeliveryState.Accepted),
            ]);

        Assert.IsNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_RollBackAfterARetirement_GoesBackToTheQueueWhenTheLinkEnds()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        await ServeOnBusAsync(
            bus,
            [
                .. AcquisitionFrames("orders", acquire: false),
                RetireInTransaction(0, Amqp1DeliveryState.Accepted, settled: true),
                Control(0, 1, DischargeRollBackHex),
            ]);

        Assert.IsNotNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_RollBackWhoseRetiredDeliveryLinkHasEnded_UnsettlesNothing()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        var (written, _) = await ServeOnBusAsync(
            bus,
            [
                .. AcquisitionFrames("orders", acquire: false),
                RetireInTransaction(0, Amqp1DeliveryState.Accepted, settled: true),
                BusFrame(new Amqp1Detach(1) { Closed = true }),
                Control(0, 1, DischargeRollBackHex),
            ]);

        Assert.IsInstanceOfType<Amqp1Disposition>(written[^1].Performative);
        Assert.IsNotNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_RetirementNamingAnUnknownTransaction_IsAnsweredRejectedAndChangesNothing()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one", "two");
        var unknown = BusFrame(new Amqp1Disposition(true, 0)
        {
            Last = 1,
            State = Amqp1DeliveryState.TransactionalState([0, 0, 0, 9], Amqp1DeliveryState.Accepted),
        });

        var (written, _) = await ServeOnBusAsync(bus, [.. AcquisitionFrames("orders", acquire: false), unknown]);

        AssertSameFrame(
            new Amqp1Disposition(false, 0) { Last = 1, State = Amqp1DeliveryState.Rejected(UnknownTransactionError()) },
            written.Select(frame => frame.Performative).OfType<Amqp1Disposition>().Single(disposition => !disposition.IsReceiver));
        Assert.IsNotNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_FlowCarryingATransactionId_AcquiresTheDeliveriesSentUntilAFlowWithout()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one", "two", "three");
        var otherProperty = new Amqp1Map([new(new Amqp1Symbol("other"), FirstTransactionId)]);

        var (written, _) = await ServeOnBusAsync(
            bus,
            [
                .. AcquisitionFrames("orders", credit: 1),
                BusFrame(Credit(1, deliveryCount: 1, credit: 1) with { Properties = otherProperty }),
                BusFrame(Credit(1, deliveryCount: 2, credit: 1) with { Properties = AcquireInFirstTransaction }),
            ]);

        var states = Transfers(written).Select(transfer => transfer.Transfer.State).ToList();
        Assert.HasCount(3, states);
        AssertSameState(TransactionalState(FirstTransactionId), states[0]);
        Assert.IsNull(states[1]);
        AssertSameState(TransactionalState(FirstTransactionId), states[2]);
    }

    [TestMethod]
    public async Task ServeAsync_FlowNamingAnUnknownTransaction_AcquiresNothing()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");
        var unknown = new Amqp1Map([new(new Amqp1Symbol("txn-id"), new byte[] { 0, 0, 0, 9 })]);

        var (written, _) = await ServeOnBusAsync(bus, [.. AcquisitionFrames("orders", acquire: false), BusFrame(Credit(1, 0, 10) with { Properties = unknown })]);

        Assert.IsNull(Transfers(written).Single().Transfer.State);
    }

    [TestMethod]
    public async Task ServeAsync_RollBackOfAcquiredDeliveries_ReturnsThemToTheirQueueRedelivered()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        await ServeOnBusAsync(
            bus, [.. AcquisitionFrames("orders", credit: 1), Control(0, 1, DischargeRollBackHex), Retire(0, Amqp1DeliveryState.Accepted)]);

        var returned = LeftIn(bus);
        Assert.IsNotNull(returned);
        Assert.IsTrue(returned.Redelivered);
    }

    [TestMethod]
    public async Task ServeAsync_RollBackOfAcquiredDeliveryRetiredInTheTransaction_ReturnsItToo()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        await ServeOnBusAsync(
            bus, [.. AcquisitionFrames("orders", credit: 1), RetireInTransaction(0, Amqp1DeliveryState.Accepted, settled: true), Control(0, 1, DischargeRollBackHex), Retire(0, Amqp1DeliveryState.Accepted)]);

        Assert.IsNotNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_RollBackOfAcquiredDeliveryRetiredOutsideTheTransaction_LeavesItRetired()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        await ServeOnBusAsync(bus, [.. AcquisitionFrames("orders", credit: 1), Retire(0, Amqp1DeliveryState.Accepted), Control(0, 1, DischargeRollBackHex)]);

        Assert.IsNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_CommitOfAcquiredDeliveries_KeepsThemAcquiredUntilTheClientSettlesThem()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        await ServeOnBusAsync(
            bus, [.. AcquisitionFrames("orders", credit: 1), Control(0, 1, DischargeCommitHex), Retire(0, Amqp1DeliveryState.Accepted)]);

        Assert.IsNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_CoordinatorDetach_ReturnsTheDeliveriesItsTransactionAcquired()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one");

        await ServeOnBusAsync(
            bus, [.. AcquisitionFrames("orders", credit: 1), BusFrame(new Amqp1Detach(0) { Closed = true }), Retire(0, Amqp1DeliveryState.Accepted)]);

        Assert.IsNotNull(LeftIn(bus));
    }

    [TestMethod]
    public async Task ServeAsync_RollBackOfAcquiredTopicDelivery_HasNothingToReturn()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(
            bus,
            [
                Open(),
                Begin(),
                BusFrame(CoordinatorAttach(0)),
                Control(0, 0, DeclareHex),
                BusFrame(ReceiverAttach(1, "/topic/news")),
                BusFrame(SenderAttach(2, "/topic/news")),
                BusFrame(new Amqp1Transfer(2) { DeliveryId = 1, DeliveryTag = [1], Settled = true }, Message),
                BusFrame(Credit(1, deliveryCount: 0, credit: 10) with { Properties = AcquireInFirstTransaction }),
                Control(0, 2, DischargeRollBackHex),
            ]);

        AssertSameState(TransactionalState(FirstTransactionId), Transfers(written).Single().Transfer.State);
        AssertSameFrame(Settled(2, Amqp1DeliveryState.Accepted), Dispositions(written).Last());
    }
}
