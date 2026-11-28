using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// How long a dynamic node's queue lasts (Part 3 section 3.5.3; ADR-0107 decision 4): it goes when the
/// link whose attach made it ends or is refused, and no other link's end deletes it by that rule; and
/// the detach sent to a link left consuming it (Part 2 section 2.8.15). Every frame is written from
/// AMQP 1.0 Part 2, and the connection is left open while it is judged.
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    private const string QueueChangePrefix = "$queue/";

    [TestMethod]
    [DataRow(false, false, DisplayName = "a dynamic target, its link detached")]
    [DataRow(false, true, DisplayName = "a dynamic target, its session ended")]
    [DataRow(true, false, DisplayName = "a dynamic source, its link detached")]
    [DataRow(true, true, DisplayName = "a dynamic source, its session ended")]
    public async Task ServeAsync_TheLinkThatMadeADynamicNodeEnds_ItsQueueIsDeleted(bool clientIsReceiver, bool endsTheSession)
    {
        Amqp1Performative end = endsTheSession ? new Amqp1End() : new Amqp1Detach(0) { Closed = true };

        var (inspected, changes) = await InspectDynamicQueueOnceWrittenAsync(
            frame => frame is Amqp1End or Amqp1Detach,
            null,
            Open(),
            Begin(),
            BusFrame(DynamicAttach(0, clientIsReceiver)),
            BusFrame(end));

        Assert.AreEqual(MessageBusOutcome.UnknownQueue, inspected);
        CollectionAssert.AreEqual(new[] { StoreChange.Written, StoreChange.Deleted }, changes);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "a dynamic target")]
    [DataRow(true, DisplayName = "a dynamic source")]
    public async Task ServeAsync_ADynamicAttachRefusedByAChosenError_LeavesNoQueue(bool clientIsReceiver)
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "attach", Reply = "amqp:resource-limit-exceeded chosen" });

        var (inspected, changes) = await InspectDynamicQueueOnceWrittenAsync(
            frame => frame is Amqp1Detach,
            plan,
            Open(),
            Begin(),
            BusFrame(DynamicAttach(0, clientIsReceiver)));

        Assert.AreEqual(MessageBusOutcome.UnknownQueue, inspected);
        CollectionAssert.AreEqual(new[] { StoreChange.Written, StoreChange.Deleted }, changes);
    }

    [TestMethod]
    public async Task ServeAsync_ASenderThatAttachedToADynamicQueueByItsAddressDetaches_TheQueueStays()
    {
        var (inspected, changes) = await InspectDynamicQueueOnceWrittenAsync(
            frame => frame is Amqp1Detach,
            null,
            Open(),
            Begin(),
            BusFrame(DynamicAttach(0, clientIsReceiver: false)),
            BusFrame(SenderAttach(1, SeededDynamicAddress())),
            BusFrame(new Amqp1Detach(1) { Closed = true }));

        Assert.AreEqual(MessageBusOutcome.QueueLocked, inspected);
        CollectionAssert.AreEqual(new[] { StoreChange.Written }, changes);
    }

    [TestMethod]
    public async Task ServeAsync_AReceiverOfADynamicQueueByItsAddressRefusedBeforeItConsumes_TheQueueStays()
    {
        // Both attaches resolve to the queue's address, so the plan picks the one by address by count: the second.
        var plan = new SecondAttachRefusingFaultPlan();

        var (inspected, changes) = await InspectDynamicQueueOnceWrittenAsync(
            frame => frame is Amqp1Detach,
            plan,
            Open(),
            Begin(),
            BusFrame(DynamicAttach(0, clientIsReceiver: false)),
            BusFrame(ReceiverAttach(1, SeededDynamicAddress())));

        Assert.AreEqual(MessageBusOutcome.QueueLocked, inspected);
        CollectionAssert.AreEqual(new[] { StoreChange.Written }, changes);
    }

    [TestMethod]
    public async Task ServeAsync_TheReceiverThatMadeADynamicNodeDetachesWhileAnotherLinkConsumesIt_TheQueueIsDeleted()
    {
        var (inspected, changes) = await InspectDynamicQueueOnceWrittenAsync(
            frame => frame is Amqp1Detach,
            null,
            Open(),
            Begin(),
            BusFrame(DynamicAttach(0, clientIsReceiver: true)),
            BusFrame(ReceiverAttach(1, SeededDynamicAddress())),
            BusFrame(new Amqp1Detach(0) { Closed = true }));

        Assert.AreEqual(MessageBusOutcome.UnknownQueue, inspected);
        CollectionAssert.AreEqual(new[] { StoreChange.Written, StoreChange.Deleted }, changes);
    }

    [TestMethod]
    public async Task ServeAsync_AQueueDeclaredAnewUnderADeletedDynamicNodesName_OutlivesTheLinkThatMadeTheNode()
    {
        // The receiver by address is the queue's last consumer, so the bus deletes it; the last sender's
        // attach then declares a durable queue under the name, which is not the dynamic node's.
        var (inspected, changes) = await InspectDynamicQueueOnceWrittenAsync(
            frame => frame is Amqp1Detach { Handle: 0 },
            null,
            Open(),
            Begin(),
            BusFrame(DynamicAttach(0, clientIsReceiver: false)),
            BusFrame(ReceiverAttach(1, SeededDynamicAddress())),
            BusFrame(new Amqp1Detach(1) { Closed = true }),
            BusFrame(SenderAttach(2, SeededDynamicAddress())),
            BusFrame(new Amqp1Detach(0) { Closed = true }));

        Assert.AreEqual(MessageBusOutcome.Succeeded, inspected);
        CollectionAssert.AreEqual(new[] { StoreChange.Written, StoreChange.Deleted, StoreChange.Written }, changes);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "made by a dynamic target")]
    [DataRow(true, DisplayName = "made by a dynamic source")]
    public async Task ServeAsync_TheLinkThatMadeADynamicNodeDetaches_ALinkConsumingItsQueueIsDetachedWithResourceDeleted(bool makerIsReceiver)
    {
        // The client's answering detach for handle 1 is answered by nothing.
        var written = await ReadDynamicNodeFramesAsync(
            Open(),
            Begin(),
            BusFrame(DynamicAttach(0, makerIsReceiver)),
            BusFrame(ReceiverAttach(1, SeededDynamicAddress())),
            BusFrame(new Amqp1Detach(0) { Closed = true }),
            BusFrame(new Amqp1Detach(1) { Closed = true }));

        var detaches = written.OfType<Amqp1Detach>().ToList();
        Assert.HasCount(2, detaches);
        Assert.AreEqual(0u, detaches[0].Handle);
        Assert.IsNull(detaches[0].Error);
        AssertIsDetachedAsDeleted(detaches[1], containerHandle: 1);
    }

    [TestMethod]
    [DataRow(false, "amqp:invalid-field", DisplayName = "a dynamic target, a first transfer without a delivery-id")]
    [DataRow(true, "amqp:illegal-state", DisplayName = "a dynamic source, a transfer on a link the client receives on")]
    public async Task ServeAsync_TheContainerDetachesTheLinkThatMadeADynamicNode_ALinkConsumingItsQueueIsDetachedWithResourceDeleted(bool makerIsReceiver, string makersCondition)
    {
        var written = await ReadDynamicNodeFramesAsync(
            Open(),
            Begin(),
            BusFrame(DynamicAttach(0, makerIsReceiver)),
            BusFrame(ReceiverAttach(1, SeededDynamicAddress())),
            BusFrame(new Amqp1Transfer(0)));

        var detaches = written.OfType<Amqp1Detach>().ToList();
        Assert.HasCount(2, detaches);
        Assert.AreEqual(new Amqp1Symbol(makersCondition), detaches[0].Error!.Condition);
        AssertIsDetachedAsDeleted(detaches[1], containerHandle: 1);
    }

    [TestMethod]
    public async Task ServeAsync_TheSessionOfTheLinkThatMadeADynamicNodeEnds_OnlyALinkOfAnotherSessionIsDetached()
    {
        var written = await ReadDynamicNodeFramesAsync(
            Open(),
            Begin(),
            Amqp1Frame.Carrying(1, new Amqp1Begin(0, 100, 100)).Encode(),
            BusFrame(DynamicAttach(0, clientIsReceiver: true)),
            BusFrame(ReceiverAttach(1, SeededDynamicAddress())),
            Amqp1Frame.Carrying(1, ReceiverAttach(0, SeededDynamicAddress())).Encode(),
            BusFrame(new Amqp1End()),
            Amqp1Frame.Carrying(1, new Amqp1End()).Encode());

        AssertIsDetachedAsDeleted(written.OfType<Amqp1Detach>().Single(), containerHandle: 0);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "its session ended")]
    [DataRow(true, DisplayName = "the connection closed")]
    public async Task ServeAsync_EveryLinkOfADynamicNodeEndsWithItsSessionOrConnection_NoDetachIsWritten(bool closesTheConnection)
    {
        var written = await ReadDynamicNodeFramesAsync(
            Open(),
            Begin(),
            BusFrame(DynamicAttach(0, clientIsReceiver: true)),
            BusFrame(ReceiverAttach(1, SeededDynamicAddress())),
            BusFrame(closesTheConnection ? new Amqp1Close() : new Amqp1End()));

        Assert.IsEmpty(written.OfType<Amqp1Detach>());
    }

    [TestMethod]
    public async Task ServeAsync_TheLinkThatMadeADynamicNodeDetaches_ASenderToItsQueueStaysAttachedAndItsNextTransferIsRejected()
    {
        // The receiver by address keeps the queue until the container deletes it with the maker.
        var written = await ReadDynamicNodeFramesAsync(
            Open(),
            Begin(),
            BusFrame(DynamicAttach(0, clientIsReceiver: true)),
            BusFrame(SenderAttach(1, SeededDynamicAddress())),
            BusFrame(ReceiverAttach(2, SeededDynamicAddress())),
            BusFrame(new Amqp1Detach(0) { Closed = true }),
            BusFrame(new Amqp1Transfer(1) { DeliveryId = 0, DeliveryTag = [1] }, ColoredMessage("red", "late")));

        CollectionAssert.AreEqual(new[] { 0u, 2u }, written.OfType<Amqp1Detach>().Select(detach => detach.Handle).ToArray());
        var rejected = new Amqp1Error(Amqp1ErrorCondition.NotFound) { Description = "no such node" };
        AssertSameFrame(new Amqp1Disposition(true, 0) { Settled = true, State = Amqp1DeliveryState.Rejected(rejected) }, written.OfType<Amqp1Disposition>().Single());
    }

    [TestMethod]
    public async Task ServeAsync_TheLinkThatMadeADynamicNodeDetachesAfterItsQueueWasDeclaredAnew_ALinkConsumingTheNewQueueStays()
    {
        // As in the test above of the name declared anew, then a receiver of the new queue.
        var written = await ReadDynamicNodeFramesAsync(
            Open(),
            Begin(),
            BusFrame(DynamicAttach(0, clientIsReceiver: false)),
            BusFrame(ReceiverAttach(1, SeededDynamicAddress())),
            BusFrame(new Amqp1Detach(1) { Closed = true }),
            BusFrame(SenderAttach(2, SeededDynamicAddress())),
            BusFrame(ReceiverAttach(3, SeededDynamicAddress())),
            BusFrame(new Amqp1Detach(0) { Closed = true }));

        Assert.IsTrue(written.OfType<Amqp1Detach>().All(detach => detach.Error is null));
        Assert.HasCount(2, written.OfType<Amqp1Detach>().ToList());
    }

    private static void AssertIsDetachedAsDeleted(Amqp1Detach detach, uint containerHandle)
    {
        Assert.AreEqual(containerHandle, detach.Handle);
        Assert.IsTrue(detach.Closed);
        Assert.AreEqual(Amqp1ErrorCondition.ResourceDeleted, detach.Error!.Condition);
        Assert.AreEqual("the node was deleted", detach.Error.Description);
    }

    // Serves the frames, then a begin on channel 9, on a connection left open until that begin is answered
    // or a close is written, so every frame before has been answered in full; the identifiers are seeded.
    private async Task<List<Amqp1Performative?>> ReadDynamicNodeFramesAsync(params byte[][] frames)
    {
        byte[][] sent = [.. frames, Amqp1Frame.Carrying(9, new Amqp1Begin(0, 100, 100)).Encode()];
        var connection = new InMemoryConnection(sent.Select(frame => (ReadOnlyMemory<byte>)frame), peerHalfClosesWhenExhausted: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var context = Context(new RecordingExchangeLog(), cancellation.Token) with { Identifiers = new UnitTestSeededIdentifierRandom(1) };
        var serving = new Amqp1ContainerConnection(connection, context, Options(), Bus(), "alice").ServeAsync();
        await WaitUntilWrittenAsync(async () => (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes))
            .Any(frame => frame.Performative is Amqp1Begin { RemoteChannel: 9 } or Amqp1Close));
        await cancellation.CancelAsync();
        await serving;
        return (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Select(frame => frame.Performative).ToList();
    }

    private static Amqp1Attach DynamicAttach(uint handle, bool clientIsReceiver)
    {
        var dynamicTerminus = new Amqp1Described(
            clientIsReceiver ? Amqp1Descriptor.Source : Amqp1Descriptor.Target,
            new List<object?> { null, null, null, null, true });
        return new($"dynamic-{handle}", handle, clientIsReceiver)
        {
            Source = clientIsReceiver ? dynamicTerminus : null,
            Target = clientIsReceiver ? null : dynamicTerminus,
            InitialDeliveryCount = clientIsReceiver ? null : 0,
        };
    }

    // The address of the first dynamic node a connection drawing from the seed 1 makes.
    private static string SeededDynamicAddress() =>
        new Amqp1BusNodeDirectory(Bus(), Bus().Connect("alice"), new UnitTestSeededIdentifierRandom(1)).Resolve(new(null, true, "alice")).CanonicalAddress!;

    // Serves the frames on a connection left open until it has written an awaited frame, then, while it is
    // still open, asks the bus as another connection about the dynamic queue made (QueueLocked while the
    // connection's own is there, UnknownQueue once gone) and lists that queue's store changes.
    private async Task<(MessageBusOutcome Inspected, StoreChange[] Changes)> InspectDynamicQueueOnceWrittenAsync(
        Func<Amqp1Performative?, bool> awaited, IFaultPlan? faults, params byte[][] frames)
    {
        var events = new RecordingSurlEventPublisher();
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), events: events);
        var connection = new InMemoryConnection(frames.Select(frame => (ReadOnlyMemory<byte>)frame), peerHalfClosesWhenExhausted: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var context = Context(new RecordingExchangeLog(), cancellation.Token) with { Identifiers = new UnitTestSeededIdentifierRandom(1) };
        context = faults is null ? context : context with { Faults = faults };
        var serving = new Amqp1ContainerConnection(connection, context, Options(), bus, "alice").ServeAsync();
        await WaitUntilWrittenAsync(async () => (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Any(frame => awaited(frame.Performative)));

        var queueName = SeededDynamicAddress()[Amqp1NodeAddress.QueuePrefix.Length..];
        var changes = events.Events.OfType<StoreChangedEvent>().Where(changed => changed.Name == QueueChangePrefix + queueName).Select(changed => changed.Change).ToArray();
        var inspected = bus.Connect("alice").InspectQueue(queueName).Outcome;
        await cancellation.CancelAsync();
        await serving;
        return (inspected, changes);
    }

    /// <summary>Answers the second <c>attach</c> it is asked about with a chosen error, and no other point.</summary>
    private sealed class SecondAttachRefusingFaultPlan : IFaultPlan
    {
        private int attaches;

        public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken) =>
            ValueTask.FromResult(point.Command == "attach" && ++attaches == 2
                ? new FaultRule("f1", FaultKind.Error) { Reply = "amqp:resource-limit-exceeded chosen" }
                : null);
    }
}
