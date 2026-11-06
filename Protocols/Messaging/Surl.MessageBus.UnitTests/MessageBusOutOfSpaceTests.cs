using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// The out-of-space fault (ADR-0124 decision 4, BL-790): a publish whose topic, or a send whose
/// <see cref="MessageBus.QueueChangeName"/>, an out-of-space rule matches is answered with the
/// outcome the bus already has for a full store - <see cref="MessageBusOutcome.StoreFull"/> for a
/// topic, <see cref="MessageBusOutcome.QueueFull"/> for a queue - and the rule is published as a
/// <see cref="FaultAppliedEvent"/>.
/// </summary>
[TestClass]
public sealed class MessageBusOutOfSpaceTests
{
    private readonly SettableTimeProvider clock = new();
    private readonly RecordingSurlEventPublisher events = new();

    [TestMethod]
    public void Publish_ARuleCoveringTheTopicFilterAHash_RefusesAToBAsStoreFullAndLetsCToDThrough()
    {
        var plan = new SpendingFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Path = "a" });
        var bus = new MessageBus(clock, events: events, faults: plan);
        var delivered = new List<BusMessage>();
        using var connection = bus.Connect();
        connection.Subscribe("#", delivered.Add);

        var refused = bus.Publish(Message("a/b"), "alice");
        var taken = bus.Publish(Message("c/d"));

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.StoreFull, 0), refused);
        Assert.AreEqual(MessageBusOutcome.Succeeded, taken.Outcome);
        Assert.AreEqual("c/d", delivered.Single().Topic);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.OutOfSpace, FaultStage.StoreWrite, null, "a/b"), events.Events[0]);
        Assert.AreEqual(new FaultPoint(FaultStage.StoreWrite) { Account = "alice", Path = "a/b" }, plan.Points[0]);
        Assert.AreEqual(Change(StoreChange.Published, "c/d"), events.Events[1]);
        CollectionAssert.AreEqual(new[] { "c/d" }, bus.GetLogTopics().ToArray());
    }

    [TestMethod]
    public void Publish_ARuleWithTimesOne_FiresOnce()
    {
        var plan = new SpendingFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Path = "a", Times = 1 });
        var bus = new MessageBus(clock, events: events, faults: plan);

        var first = bus.Publish(Message("a/b"));
        var second = bus.Publish(Message("a/b"));

        Assert.AreEqual(MessageBusOutcome.StoreFull, first.Outcome);
        Assert.AreEqual(MessageBusOutcome.Succeeded, second.Outcome);
        Assert.HasCount(1, events.Events.OfType<FaultAppliedEvent>().ToList());
    }

    [TestMethod]
    public void Publish_ARuleOfAnotherKind_IsNotOutOfSpace()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Delay) { DelayMilliseconds = 5 });
        var bus = new MessageBus(clock, events: events, faults: plan);

        var published = bus.Publish(Message("a/b"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, published.Outcome);
        Assert.IsEmpty(events.Events.OfType<FaultAppliedEvent>().ToList());
    }

    [TestMethod]
    public void Publish_AnExpiredMessage_IsNotAWriteAndAsksThePlanNothing()
    {
        var plan = new SpendingFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));
        var bus = new MessageBus(clock, faults: plan);

        var published = bus.Publish(Message("a/b") with { ExpiresAt = clock.UtcNow });

        Assert.AreEqual(MessageBusOutcome.Succeeded, published.Outcome);
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public void Publish_APlanStillDeciding_IsServedAsNoRuleAndItsLaterFailureIsObserved()
    {
        var answer = new TaskCompletionSource<FaultRule?>();
        var bus = new MessageBus(clock, events: events, faults: new PendingFaultPlan(answer.Task));

        var published = bus.Publish(Message("a/b"));
        answer.SetException(new InvalidOperationException("hook failed"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, published.Outcome);
        Assert.IsEmpty(events.Events.OfType<FaultAppliedEvent>().ToList());
    }

    [TestMethod]
    public void PublishToQueue_ARuleForTheQueue_RefusesAsQueueFullAndLetsAnotherQueueThrough()
    {
        var plan = new SpendingFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Path = "$queue/q" });
        var bus = new MessageBus(clock, events: events, faults: plan);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        connection.DeclareQueue("r");

        var refused = bus.PublishToQueue("q", Message("t"), "alice");
        var taken = bus.PublishToQueue("r", Message("t"));

        Assert.AreEqual(MessageBusOutcome.QueueFull, refused);
        Assert.AreEqual(MessageBusOutcome.Succeeded, taken);
        Assert.AreEqual(0, connection.InspectQueue("q").ReadyMessageCount);
        Assert.Contains(new FaultAppliedEvent("f1", FaultKind.OutOfSpace, FaultStage.StoreWrite, null, "$queue/q"), events.Events.ToList());
        Assert.AreEqual(new FaultPoint(FaultStage.StoreWrite) { Account = "alice", Path = "$queue/q" }, plan.Points[0]);
    }

    [TestMethod]
    public void PublishToQueue_AMissingQueueOrAnExpiredMessage_AsksThePlanNothing()
    {
        var plan = new SpendingFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));
        var bus = new MessageBus(clock, faults: plan);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");

        var missing = bus.PublishToQueue("nope", Message("t"));
        var expired = bus.PublishToQueue("q", Message("t") with { ExpiresAt = clock.UtcNow });

        Assert.AreEqual(MessageBusOutcome.UnknownQueue, missing);
        Assert.AreEqual(MessageBusOutcome.Succeeded, expired);
        Assert.IsEmpty(plan.Points);
    }

    private static StoreChangedEvent Change(StoreChange change, string name) =>
        new(StoreKind.Bus, change, name, 1, ExchangeId: null);

    private static BusMessage Message(string topic) => new(topic, Encoding.UTF8.GetBytes("x"));

    private sealed class PendingFaultPlan(Task<FaultRule?> answer) : IFaultPlan
    {
        public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken) => new(answer);
    }
}
