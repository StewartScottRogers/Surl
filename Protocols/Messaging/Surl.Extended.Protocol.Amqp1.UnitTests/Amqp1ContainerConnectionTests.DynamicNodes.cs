using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// How long a dynamic node's queue lasts (Part 3 section 3.5.3; ADR-0107 decision 4): it goes when the
/// link whose attach made it ends or is refused, and no other link's end deletes it by that rule. Every
/// frame is written from AMQP 1.0 Part 2 and the bus is asked while the connection is still open.
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
