using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9): a matching
/// <see cref="FaultKind.Error"/> rule refuses an <c>attach</c> with a null terminus and a closing
/// <c>detach</c> carrying its error, or a message with a <c>rejected</c> disposition carrying it, and
/// publishes one <see cref="FaultAppliedEvent"/>. Every frame is written from AMQP 1.0 Part 2.
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    private static readonly Amqp1Error ChosenError = new(new Amqp1Symbol("amqp:resource-limit-exceeded")) { Description = "chosen" };

    [TestMethod]
    public async Task ServeAsync_AttachToARulesNode_IsRefusedWithTheChosenErrorAndAnotherIsAttached()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(FaultRule("attach", "/queue/orders", "amqp:resource-limit-exceeded chosen"));

        var (written, log) = await ServeFaultedAsync(plan, events, Open(), Begin(), BusFrame(SenderAttach(0, "audit")), BusFrame(SenderAttach(1, "orders")));

        var detaches = written.Select(frame => frame.Performative).OfType<Amqp1Detach>().ToList();
        Assert.AreEqual(new Amqp1Detach(1) { Closed = true, Error = ChosenError }, detaches.Single());
        var attaches = written.Select(frame => frame.Performative).OfType<Amqp1Attach>().ToList();
        Assert.IsNotNull(attaches[0].Target);
        Assert.IsNull(attaches[1].Target);
        AssertOneFaultApplied(events, "attach /queue/orders");
        CollectionAssert.Contains(log.Notes.ToArray(), "attach /queue/orders answered by fault rule f1 with amqp:resource-limit-exceeded.");
        var point = plan.Points.Last();
        Assert.AreEqual(FaultStage.Command, point.Stage);
        Assert.AreEqual(1L, point.ExchangeId);
        Assert.AreEqual("amqp1://127.0.0.1:5672/", point.ListenUrl);
        Assert.AreEqual("amqp1", point.Scheme);
        Assert.AreEqual("alice", point.Account);
    }

    [TestMethod]
    public async Task ServeAsync_TransferToARulesNode_IsRejectedWithTheChosenErrorAndNotStored()
    {
        var bus = Bus();
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(FaultRule("transfer", "/queue/orders", "amqp:resource-limit-exceeded chosen"));

        var (written, _) = await ServeFaultedAsync(
            bus,
            plan,
            events,
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "orders")),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [7], MessageFormat = 0 }, Message));

        AssertSameFrame(
            new Amqp1Disposition(IsReceiver: true, 0) { Settled = true, State = Amqp1DeliveryState.Rejected(ChosenError) },
            written.Single(frame => frame.Performative is Amqp1Disposition).Performative);
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
        AssertOneFaultApplied(events, "transfer /queue/orders");
    }

    [TestMethod]
    [DataRow("amqp:x")]
    [DataRow("amqp:x ")]
    [DataRow(" amqp:x y")]
    [DataRow("amqp:é y")]
    [DataRow(null)]
    public async Task ServeAsync_ARuleWhoseReplyIsNotAnError_IsNotedAndTheAttachServed(string? reply)
    {
        var events = new RecordingSurlEventPublisher();

        var (written, log) = await ServeFaultedAsync(new ScriptedFaultPlan(FaultRule("attach", "/queue/orders", reply)), events, Open(), Begin(), BusFrame(SenderAttach(0, "orders")));

        Assert.IsNotNull(written.Select(frame => frame.Performative).OfType<Amqp1Attach>().Single().Target);
        Assert.IsEmpty(events.Events);
        CollectionAssert.Contains(log.Notes.ToArray(), "attach /queue/orders: fault rule f1 is not an error with an AMQP 1.0 condition and description; not applied.");
    }

    [TestMethod]
    public async Task ServeAsync_ARuleOfAKindAnAttachCannotApply_IsNotedAndTheAttachServed()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Command = "attach", Path = "/queue/orders" });

        var (written, log) = await ServeFaultedAsync(plan, events, Open(), Begin(), BusFrame(SenderAttach(0, "orders")));

        Assert.IsNotNull(written.Select(frame => frame.Performative).OfType<Amqp1Attach>().Single().Target);
        Assert.IsEmpty(events.Events);
        CollectionAssert.Contains(log.Notes.ToArray(), "attach /queue/orders: fault rule f1 is not an error with an AMQP 1.0 condition and description; not applied.");
    }

    [TestMethod]
    [DataRow("attach")]
    [DataRow("transfer")]
    public async Task ServeAsync_ADelayRuleForAPerformative_HoldsItForTheDelayThenHandlesItAsUsual(string command)
    {
        var events = new RecordingSurlEventPublisher();
        var idleClock = new RecordingIdleClock();
        var clock = new ManualTimeProvider();
        var bus = Bus();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Delay) { Command = command, DelayMilliseconds = 10 });
        var connection = new InMemoryConnection(new[]
        {
            Open(), Begin(), BusFrame(SenderAttach(0, "orders")),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [7], MessageFormat = 0 }, Message),
        }.Select(frame => (ReadOnlyMemory<byte>)frame), peerHalfClosesWhenExhausted: false);
        // The client stays connected: a script that half-closes would end the hold at once (ADR-0141 amendment 2).
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var context = Context(clock, new RecordingExchangeLog(), exchange.Token) with { Faults = plan, Events = events, IdleClock = idleClock };

        var serving = new Amqp1ContainerConnection(connection, context, Options(), bus, "alice").ServeAsync();
        await idleClock.FirstPause.WaitAsync(TestContext.CancellationToken);
        Assert.IsTrue(idleClock.IsPaused);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        var stored = bus.Connect().GetFromQueue("orders").Delivery;
        while (stored is null && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
            stored = bus.Connect().GetFromQueue("orders").Delivery;
        }

        await exchange.CancelAsync();
        await serving;

        var written = await ReadWrittenWithPayloadsAsync(connection.WrittenBytes);
        Assert.IsNotNull(written.Select(frame => frame.Performative).OfType<Amqp1Attach>().Single().Target);
        Assert.IsNotNull(stored);
        Assert.AreEqual(1, idleClock.RestartCount);
        Assert.IsFalse(connection.Aborted);
        var applied = events.Events.OfType<FaultAppliedEvent>().Single();
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, $"{command} /queue/orders"), applied);
    }

    [TestMethod]
    public async Task ServeAsync_ADropRuleForAnAttach_AbortsTheConnectionWithNoAnsweringPerformative()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "attach", Path = "/queue/orders" });
        var connection = new InMemoryConnection(new[] { Open(), Begin(), BusFrame(SenderAttach(0, "orders")) }.Select(frame => (ReadOnlyMemory<byte>)frame));
        var context = Context(new RecordingExchangeLog(), TestContext.CancellationToken) with { Faults = plan, Events = events };

        await new Amqp1ContainerConnection(connection, context, Options(), Bus(), "alice").ServeAsync();

        var written = await ReadWrittenWithPayloadsAsync(connection.WrittenBytes);
        Assert.IsTrue(connection.Aborted);
        Assert.IsEmpty(written.Select(frame => frame.Performative).OfType<Amqp1Attach>());
        Assert.IsEmpty(written.Select(frame => frame.Performative).OfType<Amqp1Detach>());
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "attach /queue/orders"), events.Events.OfType<FaultAppliedEvent>().Single());
    }

    [TestMethod]
    public async Task ServeAsync_ADropRuleForATransfer_AbortsTheConnectionAndStoresNothing()
    {
        var bus = Bus();
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "transfer" });

        var (written, _) = await ServeFaultedAsync(
            bus,
            plan,
            events,
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "orders")),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [7], MessageFormat = 0 }, Message));

        Assert.IsEmpty(written.Select(frame => frame.Performative).OfType<Amqp1Disposition>());
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "transfer /queue/orders"), events.Events.OfType<FaultAppliedEvent>().Single());
    }

    [TestMethod]
    public async Task ServeAsync_ARuleForAnotherPerformative_ChangesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "transfer" });

        var (written, _) = await ServeFaultedAsync(plan, events, Open(), Begin(), BusFrame(SenderAttach(0, "orders")));

        Assert.IsNotNull(written.Select(frame => frame.Performative).OfType<Amqp1Attach>().Single().Target);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public void ReadChosenError_ASymbolAndADescription_IsTheError() =>
        Assert.AreEqual(new Amqp1Error(new Amqp1Symbol("amqp:not-found")) { Description = "gone away" }, Amqp1ContainerConnection.ReadChosenError("amqp:not-found gone away"));

    private static FaultRule FaultRule(string command, string path, string? reply) =>
        new("f1", FaultKind.Error) { Scheme = "amqp1", Command = command, Path = path, Reply = reply };

    private static void AssertOneFaultApplied(RecordingSurlEventPublisher events, string target) =>
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, target), events.Events.OfType<FaultAppliedEvent>().Single());

    private Task<(List<(Amqp1Performative? Performative, byte[] Payload)> Written, RecordingExchangeLog Log)> ServeFaultedAsync(
        IFaultPlan plan, ISurlEventPublisher events, params byte[][] frames) =>
        ServeFaultedAsync(Bus(), plan, events, frames);

    private async Task<(List<(Amqp1Performative? Performative, byte[] Payload)> Written, RecordingExchangeLog Log)> ServeFaultedAsync(
        MessageBus.MessageBus bus, IFaultPlan plan, ISurlEventPublisher events, params byte[][] frames)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(frames.Select(frame => (ReadOnlyMemory<byte>)frame));
        var context = Context(log, TestContext.CancellationToken) with { Faults = plan, Events = events };

        await new Amqp1ContainerConnection(connection, context, Options(), bus, "alice").ServeAsync();

        return (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes), log);
    }
}
