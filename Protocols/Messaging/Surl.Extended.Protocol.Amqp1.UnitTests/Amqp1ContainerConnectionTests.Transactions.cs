using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Local transactions (AMQP 1.0 Part 4 sections 4.2 to 4.5; ADR-0146 decisions 1.1 to 1.4 and 1.6 to
/// 1.8). The control messages are written by hand from Part 4 section 4.5 and Part 3 section 3.2.8;
/// the performatives are built with the library's codec, which BL-510's tests pin.
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    // amqp-value (0x77) holding declare (0x31) with no field: list0.
    private const string DeclareHex = "005377" + "005331" + "45";

    // amqp-value holding declare with global-id the binary 01 02 03 04.
    private const string DeclareWithGlobalIdHex = "005377" + "005331" + "C00701" + "A00401020304";

    // amqp-value holding discharge (0x32) of txn-id 00 00 00 01, fail false.
    private const string DischargeCommitHex = "005377" + "005332" + "C00802" + "A00400000001" + "42";

    // amqp-value holding discharge of txn-id 00 00 00 01, fail true.
    private const string DischargeRollBackHex = "005377" + "005332" + "C00802" + "A00400000001" + "41";

    private static readonly byte[] FirstTransactionId = [0, 0, 0, 1];

    [TestMethod]
    public async Task ServeAsync_AttachToACoordinator_IsAnsweredAsAReceiverWithTheServedCapabilities()
    {
        var (written, _) = await ServeOnBusAsync(Bus(), Open(), Begin(), BusFrame(CoordinatorAttach(0)));

        var attach = (Amqp1Attach)written[2].Performative!;
        Assert.IsTrue(attach.IsReceiver);
        Assert.AreEqual(Amqp1Descriptor.Coordinator, ((Amqp1Described)attach.Target!).Descriptor);
        var capabilities = (Amqp1Array)Fields(attach.Target)[0]!;
        CollectionAssert.AreEqual(
            new object?[] { new Amqp1Symbol("amqp:local-transactions"), new Amqp1Symbol("amqp:multi-txns-per-ssn"), new Amqp1Symbol("amqp:multi-ssns-per-txn") },
            capabilities.Elements.ToArray());
        Assert.AreEqual(Amqp1ContainerConnection.LinkCredit, ((Amqp1Flow)written[3].Performative!).LinkCredit);
    }

    [TestMethod]
    public async Task ServeAsync_CoordinatorAsASource_IsRefusedWithNotAllowed()
    {
        var attach = new Amqp1Attach("txn", 0, IsReceiver: true) { Source = Amqp1Terminus.Coordinator };

        var (written, _) = await ServeOnBusAsync(Bus(), Open(), Begin(), BusFrame(attach));

        Assert.IsNull(((Amqp1Attach)written[2].Performative!).Source);
        Assert.AreEqual(
            new Amqp1Detach(0) { Closed = true, Error = new(Amqp1ErrorCondition.NotAllowed) { Description = "a coordinator is only a target" } },
            written[3].Performative);
    }

    [TestMethod]
    public async Task ServeAsync_Declares_AreAnsweredWithIdsCountingUpFromOne()
    {
        var (written, _) = await ServeOnBusAsync(Bus(), Open(), Begin(), BusFrame(CoordinatorAttach(0)), Control(0, 0, DeclareHex), Control(0, 1, DeclareHex));

        var dispositions = Dispositions(written);
        AssertSameFrame(Settled(0, Amqp1DeliveryState.Declared([0, 0, 0, 1])), dispositions[0]);
        AssertSameFrame(Settled(1, Amqp1DeliveryState.Declared([0, 0, 0, 2])), dispositions[1]);
    }

    [TestMethod]
    public async Task ServeAsync_PreSettledDeclare_IsAppliedAndAnsweredByNothing()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(), Open(), Begin(), BusFrame(CoordinatorAttach(0)), Control(0, 0, DeclareHex, settled: true), Control(0, 1, DeclareHex));

        AssertSameFrame(Settled(1, Amqp1DeliveryState.Declared([0, 0, 0, 2])), Dispositions(written).Single());
    }

    [TestMethod]
    [DataRow(DeclareWithGlobalIdHex, "amqp:not-implemented", "distributed transactions are not supported")]
    [DataRow("005377" + "A103616263", "amqp:decode-error", "not a declare or discharge")]
    [DataRow("005377" + "005333" + "45", "amqp:decode-error", "not a declare or discharge")]
    [DataRow("005375" + "A00100", "amqp:decode-error", "not a declare or discharge")]
    [DataRow("005377" + "005332" + "45", "amqp:decode-error", "not a declare or discharge")]
    [DataRow("005377" + "005331" + "A100", "amqp:decode-error", "not a declare or discharge")]
    [DataRow("FF", "amqp:decode-error", "not a declare or discharge")]
    public async Task ServeAsync_ControlMessageThatIsNoPlainDeclareOrDischarge_IsRejected(string messageHex, string condition, string description)
    {
        var (written, _) = await ServeOnBusAsync(Bus(), Open(), Begin(), BusFrame(CoordinatorAttach(0)), Control(0, 0, messageHex));

        AssertSameFrame(Settled(0, Amqp1DeliveryState.Rejected(new(new Amqp1Symbol(condition)) { Description = description })), Dispositions(written).Single());
    }

    [TestMethod]
    public async Task ServeAsync_A101stOpenTransaction_IsRejectedWithResourceLimitExceeded()
    {
        var frames = new List<byte[]> { Open(), Begin(), BusFrame(CoordinatorAttach(0)) };
        frames.AddRange(Enumerable.Range(0, Amqp1ContainerConnection.MaxOpenTransactions + 1).Select(index => Control(0, (uint)index, DeclareHex)));

        var (written, _) = await ServeOnBusAsync(Bus(), [.. frames]);

        var dispositions = Dispositions(written);
        AssertSameFrame(Settled(99, Amqp1DeliveryState.Declared([0, 0, 0, 100])), dispositions[99]);
        AssertSameFrame(
            Settled(100, Amqp1DeliveryState.Rejected(new(Amqp1ErrorCondition.ResourceLimitExceeded) { Description = "too many open transactions" })),
            dispositions[100]);
    }

    [TestMethod]
    public async Task ServeAsync_CommittedTransaction_PublishesItsMessagesOnlyAtTheDischarge()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(bus, [.. TransactionFrames(Posted(1, 1), Posted(1, 2)), Control(0, 3, DischargeCommitHex)]);

        var dispositions = Dispositions(written);
        AssertSameFrame(Settled(1, Amqp1DeliveryState.TransactionalState(FirstTransactionId, Amqp1DeliveryState.Accepted)), dispositions[1]);
        AssertSameFrame(Settled(2, Amqp1DeliveryState.TransactionalState(FirstTransactionId, Amqp1DeliveryState.Accepted)), dispositions[2]);
        AssertSameFrame(Settled(3, Amqp1DeliveryState.Accepted), dispositions[3]);
        var consumer = bus.Connect();
        Assert.AreEqual("hello", Encoding.UTF8.GetString(consumer.GetFromQueue("orders").Delivery!.Message.Payload.Span));
        Assert.AreEqual("hello", Encoding.UTF8.GetString(consumer.GetFromQueue("orders").Delivery!.Message.Payload.Span));
        Assert.IsNull(consumer.GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_TransactionNotYetDischarged_HasPublishedNothing()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(bus, [.. TransactionFrames(Posted(1, 1))]);

        AssertSameFrame(Settled(1, Amqp1DeliveryState.TransactionalState(FirstTransactionId, Amqp1DeliveryState.Accepted)), Dispositions(written)[1]);
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_CommitTheBusRefusesInPart_PublishesNoneAndIsRejectedWithRollback()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxMessagesPerQueue: 1);

        var (written, log) = await ServeOnBusAsync(bus, [.. TransactionFrames(Posted(1, 1), Posted(1, 2)), Control(0, 3, DischargeCommitHex), Control(0, 4, DischargeCommitHex)]);

        var dispositions = Dispositions(written);
        AssertSameFrame(
            Settled(3, Amqp1DeliveryState.Rejected(new(Amqp1ErrorCondition.TransactionRollback) { Description = "the transaction was rolled back" })),
            dispositions[3]);
        AssertSameFrame(Settled(4, Amqp1DeliveryState.Rejected(UnknownTransactionError())), dispositions[4]);
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
        CollectionAssert.Contains(log.Notes.ToList(), "A transaction was rolled back: the bus refused its message 1 with QueueFull.");
    }

    [TestMethod]
    public async Task ServeAsync_DischargeWithFail_DropsTheHeldMessagesAndForgetsTheId()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(bus, [.. TransactionFrames(Posted(1, 1)), Control(0, 2, DischargeRollBackHex), Posted(1, 3)]);

        var dispositions = Dispositions(written);
        AssertSameFrame(Settled(2, Amqp1DeliveryState.Accepted), dispositions[2]);
        AssertSameFrame(Settled(3, Amqp1DeliveryState.Rejected(UnknownTransactionError())), dispositions[3]);
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_UnknownTransactionIds_AreRejectedWithUnknownId()
    {
        var bus = Bus();
        var notBinary = new Amqp1Described(Amqp1Descriptor.TransactionalState, new List<object?> { "1" });

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(CoordinatorAttach(0)),
            BusFrame(SenderAttach(1, "orders")),
            Control(0, 0, DischargeCommitHex),
            Posted(1, 1),
            Posted(1, 2, notBinary),
            Posted(1, 3, TransactionalState([1])));

        var dispositions = Dispositions(written);
        Assert.HasCount(4, dispositions);
        foreach (var (disposition, deliveryId) in dispositions.Select((disposition, index) => (disposition, (uint)index)))
        {
            AssertSameFrame(Settled(deliveryId, Amqp1DeliveryState.Rejected(UnknownTransactionError())), disposition);
        }

        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_TransactionalTransferTheContainerWouldRefuse_IsRejectedInTransactionalStateAndNotHeld()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(
            bus, [.. TransactionFrames(Posted(1, 1, messageHex: "FF"), Posted(1, 2, messageFormat: 1)), Control(0, 3, DischargeCommitHex)]);

        var dispositions = Dispositions(written);
        AssertSameFrame(Settled(1, InTransaction(Rejected(Amqp1ErrorCondition.DecodeError, "a message that does not decode"))), dispositions[1]);
        AssertSameFrame(Settled(2, InTransaction(Rejected(Amqp1ErrorCondition.NotImplemented, "message format not supported"))), dispositions[2]);
        AssertSameFrame(Settled(3, Amqp1DeliveryState.Accepted), dispositions[3]);
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_TransactionalTopicTransfer_IsPublishedToTheTopicAtTheCommit()
    {
        var bus = Bus();
        var received = new List<string>();
        bus.Connect().Subscribe("news", message => received.Add(Encoding.UTF8.GetString(message.Payload.Span)));
        var frames = TransactionFrames(Posted(1, 1)).ToList();
        frames[3] = BusFrame(SenderAttach(1, "/topic/news"));

        var (written, _) = await ServeOnBusAsync(bus, [.. frames, Control(0, 2, DischargeCommitHex)]);

        var dispositions = Dispositions(written);
        AssertSameFrame(Settled(1, InTransaction(Amqp1DeliveryState.Accepted)), dispositions[1]);
        AssertSameFrame(Settled(2, Amqp1DeliveryState.Accepted), dispositions[2]);
        CollectionAssert.AreEqual(new[] { "hello" }, received);
    }

    [TestMethod]
    public async Task ServeAsync_HeldMessagesPastMaxFilesize_AreRejectedWithResourceLimitExceeded()
    {
        var bus = Bus();
        var limits = ExchangeLimits.Default with { MaxUploadBytes = Message.Length + 1 };

        var (written, _) = await ServeWithLimitsAsync(bus, limits, [.. TransactionFrames(Posted(1, 1), Posted(1, 2))]);

        var dispositions = Dispositions(written);
        AssertSameFrame(Settled(1, InTransaction(Amqp1DeliveryState.Accepted)), dispositions[1]);
        AssertSameFrame(Settled(2, InTransaction(Rejected(Amqp1ErrorCondition.ResourceLimitExceeded, "resource limit exceeded"))), dispositions[2]);
    }

    [TestMethod]
    public async Task ServeAsync_PreSettledTransactionalTransfer_IsHeldAndAnsweredByNothing()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(bus, [.. TransactionFrames(Posted(1, 1, settled: true)), Control(0, 2, DischargeCommitHex)]);

        var dispositions = Dispositions(written);
        Assert.HasCount(2, dispositions);
        AssertSameFrame(Settled(2, Amqp1DeliveryState.Accepted), dispositions[1]);
        Assert.IsNotNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_TransactionalStateOnTheLastOfMoreTransfers_PostsTheMessageInTheTransaction()
    {
        var bus = Bus();
        var frames = TransactionFrames().ToList();
        frames.Add(BusFrame(new Amqp1Transfer(1) { DeliveryId = 1, DeliveryTag = [1], MessageFormat = 0, More = true }, Message.AsMemory(0, 10)));
        frames.Add(BusFrame(new Amqp1Transfer(1) { State = TransactionalState(FirstTransactionId) }, Message.AsMemory(10)));

        var (written, _) = await ServeOnBusAsync(bus, [.. frames, Control(0, 2, DischargeRollBackHex)]);

        AssertSameFrame(Settled(1, InTransaction(Amqp1DeliveryState.Accepted)), Dispositions(written)[1]);
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_CoordinatorDetach_RollsBackItsTransaction()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(
            bus, [.. TransactionFrames(Posted(1, 1)), BusFrame(new Amqp1Detach(0) { Closed = true }), BusFrame(CoordinatorAttach(2)), Control(2, 2, DischargeCommitHex)]);

        AssertSameFrame(Settled(2, Amqp1DeliveryState.Rejected(UnknownTransactionError())), Dispositions(written)[2]);
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_SessionEnd_RollsBackItsCoordinatorsTransaction()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            Amqp1Frame.Carrying(1, new Amqp1Begin(0, 100, 100)).Encode(),
            Amqp1Frame.Carrying(1, CoordinatorAttach(0)).Encode(),
            Amqp1Frame.Carrying(1, new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [0], MessageFormat = 0 }, Convert.FromHexString(DeclareHex)).Encode(),
            BusFrame(SenderAttach(1, "orders")),
            Posted(1, 0),
            Amqp1Frame.Carrying(1, new Amqp1End()).Encode(),
            BusFrame(CoordinatorAttach(0)),
            Control(0, 1, DischargeCommitHex));

        var dispositions = Dispositions(written);
        AssertSameFrame(Settled(0, InTransaction(Amqp1DeliveryState.Accepted)), dispositions[1]);
        AssertSameFrame(Settled(1, Amqp1DeliveryState.Rejected(UnknownTransactionError())), dispositions[2]);
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionEnd_RollsBackAnOpenTransaction()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(bus, [.. TransactionFrames(Posted(1, 1)), BusFrame(new Amqp1Close())]);

        AssertSameFrame(Settled(1, InTransaction(Amqp1DeliveryState.Accepted)), Dispositions(written)[1]);
        Assert.IsInstanceOfType<Amqp1Close>(written[^1].Performative);
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_TransactionDeclaredInOneSession_IsUsedFromAnother()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(
            bus,
            [
                .. TransactionFrames(),
                Amqp1Frame.Carrying(1, new Amqp1Begin(0, 100, 100)).Encode(),
                Amqp1Frame.Carrying(1, SenderAttach(0, "orders")).Encode(),
                Amqp1Frame.Carrying(1, new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [0], MessageFormat = 0, State = TransactionalState(FirstTransactionId) }, Message).Encode(),
                Control(0, 1, DischargeCommitHex),
            ]);

        var dispositions = Dispositions(written);
        AssertSameFrame(Settled(0, InTransaction(Amqp1DeliveryState.Accepted)), dispositions[1]);
        AssertSameFrame(Settled(1, Amqp1DeliveryState.Accepted), dispositions[2]);
        Assert.IsNotNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    private static Amqp1Attach CoordinatorAttach(uint handle) => new($"txn-{handle}", handle, IsReceiver: false)
    {
        Source = new Amqp1Described(Amqp1Descriptor.Source, new List<object?>()),
        Target = new Amqp1Described(Amqp1Descriptor.Coordinator, new List<object?> { new Amqp1Symbol("amqp:local-transactions") }),
    };

    // A coordinator and a sender on queue "orders", then a declare of transaction 00 00 00 01 as delivery 0.
    private static byte[][] TransactionFrames(params byte[][] posted) =>
        [Open(), Begin(), BusFrame(CoordinatorAttach(0)), BusFrame(SenderAttach(1, "orders")), Control(0, 0, DeclareHex), .. posted];

    private static byte[] Control(uint handle, uint deliveryId, string messageHex, bool settled = false) =>
        BusFrame(new Amqp1Transfer(handle) { DeliveryId = deliveryId, DeliveryTag = [(byte)deliveryId], MessageFormat = 0, Settled = settled ? true : null }, Convert.FromHexString(messageHex));

    private static byte[] Posted(uint handle, uint deliveryId, Amqp1Described? state = null, string? messageHex = null, uint messageFormat = 0, bool settled = false) =>
        BusFrame(
            new Amqp1Transfer(handle)
            {
                DeliveryId = deliveryId,
                DeliveryTag = [(byte)deliveryId],
                MessageFormat = messageFormat,
                Settled = settled ? true : null,
                State = state ?? TransactionalState(FirstTransactionId),
            },
            messageHex is null ? Message : Convert.FromHexString(messageHex));

    private static Amqp1Described TransactionalState(byte[] transactionId) =>
        new(Amqp1Descriptor.TransactionalState, new List<object?> { transactionId });

    private static Amqp1Described InTransaction(Amqp1Described outcome) => Amqp1DeliveryState.TransactionalState(FirstTransactionId, outcome);

    private static Amqp1Described Rejected(Amqp1Symbol condition, string description) => Amqp1DeliveryState.Rejected(new(condition) { Description = description });

    private static Amqp1Error UnknownTransactionError() => new(Amqp1ErrorCondition.TransactionUnknownId) { Description = "unknown transaction" };

    private static Amqp1Disposition Settled(uint deliveryId, Amqp1Described state) => new(IsReceiver: true, deliveryId) { Settled = true, State = state };

    private static List<Amqp1Performative?> Dispositions(List<(Amqp1Performative? Performative, byte[] Payload)> written) =>
        [.. written.Where(frame => frame.Performative is Amqp1Disposition).Select(frame => frame.Performative)];

    private async Task<(List<(Amqp1Performative? Performative, byte[] Payload)> Written, RecordingExchangeLog Log)> ServeWithLimitsAsync(
        MessageBus.MessageBus bus, ExchangeLimits limits, params byte[][] frames)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(frames.Select(frame => (ReadOnlyMemory<byte>)frame));

        await new Amqp1ContainerConnection(connection, Context(log, TestContext.CancellationToken) with { Limits = limits }, Options(), bus, "alice").ServeAsync();

        return (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes), log);
    }
}
