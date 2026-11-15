using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// What a client's change to the exchange table publishes and the <c>out-of-space</c> rule that refuses it
/// (ADR-0166 decisions 1 to 3).
/// </summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    private static StoreChangedEvent ExchangeChange(StoreChange change, string name) =>
        new(StoreKind.Exchanges, change, name, ByteCount: null, ExchangeId: 1);

    private static StoreChangedEvent[] ExchangeChangesOf(RecordingSurlEventPublisher events) =>
        [.. events.Events.OfType<StoreChangedEvent>()];

    private static FaultRule OutOfSpaceRule(string exchange) =>
        new("f1", FaultKind.OutOfSpace) { Stage = FaultStage.StoreWrite, Path = exchange };

    private static IEnumerable<FaultPoint> StoreWritePointsOf(ScriptedFaultPlan plan) =>
        plan.Points.Where(point => point.Stage == FaultStage.StoreWrite);

    [TestMethod]
    public async Task ExchangeDeclare_CreatingAndRedeclaring_PublishesCreatedOnce()
    {
        var events = new RecordingSurlEventPublisher();

        await ServeFaultedAsync(new ScriptedFaultPlan(), events, ExchangeDeclare(1, "x", "direct"), ExchangeDeclare(1, "x", "direct"), ExchangeDeclare(1, "x", "direct", bits: 1));

        CollectionAssert.AreEqual(new[] { ExchangeChange(StoreChange.Created, "x") }, ExchangeChangesOf(events));
    }

    [TestMethod]
    public async Task QueueBindAndUnbind_PublishBoundAndUnboundOnlyWhenTheTableChanged()
    {
        var events = new RecordingSurlEventPublisher();

        await ServeFaultedAsync(
            new ScriptedFaultPlan(),
            events,
            ExchangeDeclare(1, "x", "direct"),
            QueueDeclare(1, "q"),
            QueueBind(1, "q", "x", "k"),
            QueueBind(1, "q", "x", "k"),
            QueueUnbind(1, "q", "x", "k"),
            QueueUnbind(1, "q", "x", "k"));

        CollectionAssert.AreEqual(
            new[] { ExchangeChange(StoreChange.Created, "x"), ExchangeChange(StoreChange.Bound, "x/q/k"), ExchangeChange(StoreChange.Unbound, "x/q/k") },
            ExchangeChangesOf(events));
    }

    [TestMethod]
    public async Task QueueBind_OnTheDefaultTopicSpace_PublishesNoExchangeChange()
    {
        var events = new RecordingSurlEventPublisher();

        await ServeFaultedAsync(new ScriptedFaultPlan(), events, QueueDeclare(1, "q"), QueueBind(1, "q", "amq.topic", "a.b"), QueueUnbind(1, "q", "amq.topic", "a.b"));

        Assert.IsEmpty(ExchangeChangesOf(events));
    }

    [TestMethod]
    public async Task ExchangeDelete_PublishesEachUnboundBeforeDeleted()
    {
        var events = new RecordingSurlEventPublisher();

        await ServeFaultedAsync(
            new ScriptedFaultPlan(),
            events,
            ExchangeDeclare(1, "x", "direct"),
            QueueDeclare(1, "q"),
            QueueBind(1, "q", "x", "a"),
            QueueBind(1, "q", "x", "b"),
            ExchangeDelete(1, "x"));

        CollectionAssert.AreEqual(
            new[]
            {
                ExchangeChange(StoreChange.Created, "x"),
                ExchangeChange(StoreChange.Bound, "x/q/a"),
                ExchangeChange(StoreChange.Bound, "x/q/b"),
                ExchangeChange(StoreChange.Unbound, "x/q/a"),
                ExchangeChange(StoreChange.Unbound, "x/q/b"),
                ExchangeChange(StoreChange.Deleted, "x"),
            },
            ExchangeChangesOf(events));
    }

    [TestMethod]
    public async Task ExchangeDelete_RefusedBecauseInUse_PublishesNothingForTheDelete()
    {
        var events = new RecordingSurlEventPublisher();

        await ServeFaultedAsync(new ScriptedFaultPlan(), events, ExchangeDeclare(1, "x", "direct"), QueueDeclare(1, "q"), QueueBind(1, "q", "x", "a"), ExchangeDelete(1, "x", bits: 1));

        Assert.IsFalse(ExchangeChangesOf(events).Any(change => change.Change is StoreChange.Deleted or StoreChange.Unbound));
    }

    [TestMethod]
    public async Task Names_PastTwoHundredAndFiftySixCharacters_AreTruncated()
    {
        var events = new RecordingSurlEventPublisher();
        var queue = new string('q', 100);

        await ServeFaultedAsync(new ScriptedFaultPlan(), events, ExchangeDeclare(1, "x", "direct"), QueueDeclare(1, queue), QueueBind(1, queue, "x", new string('k', 200)));

        var bound = ExchangeChangesOf(events).Single(change => change.Change == StoreChange.Bound);
        Assert.AreEqual(256, bound.Name.Length);
        Assert.StartsWith("x/qqq", bound.Name);
    }

    [TestMethod]
    public async Task ExchangeDeclare_UnderAnOutOfSpaceRule_ClosesWith506WritesNothingAndPublishesOneFault()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(OutOfSpaceRule("x"));

        var (written, log) = await ServeFaultedAsync(plan, events, ExchangeDeclare(1, "x", "direct"));

        AssertAnswered(written, BrokerClose(506, "RESOURCE_ERROR - out of space", 40, 10));
        Assert.IsEmpty(ExchangeChangesOf(events));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.OutOfSpace, FaultStage.StoreWrite, 1, "x"), events.Events.OfType<FaultAppliedEvent>().Single());
        Assert.AreEqual("alice", StoreWritePointsOf(plan).Single().Account);
        CollectionAssert.Contains(log.Notes.ToArray(), "A write to exchange x was refused as out of space by fault rule f1.");
    }

    [TestMethod]
    public async Task ExchangeDeclare_OfAnExistingExchangeUnderAnOutOfSpaceRule_IsNotRefused()
    {
        var plan = new ScriptedFaultPlan(OutOfSpaceRule("amq.direct"));

        var (written, _) = await ServeFaultedAsync(plan, new RecordingSurlEventPublisher(), ExchangeDeclare(1, "amq.direct", "direct", bits: 1), ExchangeDeclare(1, "amq.direct", "direct", bits: 1));

        AssertAnswered(written, ExchangeDeclareOk(1), ExchangeDeclareOk(1));
        Assert.IsEmpty(StoreWritePointsOf(plan));
    }

    [TestMethod]
    public async Task ExchangeDeclare_AfterARefusedOneForAnotherName_IsServed()
    {
        var plan = new ScriptedFaultPlan(OutOfSpaceRule("x"));

        var (written, _) = await ServeFaultedAsync(plan, new RecordingSurlEventPublisher(), ExchangeDeclare(1, "y", "direct"));

        AssertAnswered(written, ExchangeDeclareOk(1));
    }

    [TestMethod]
    public async Task QueueBind_UnderAnOutOfSpaceRule_ClosesWith506AndAddsNoBinding()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(OutOfSpaceRule("amq.direct"));

        var (written, _) = await ServeFaultedAsync(plan, events, QueueDeclare(1, "q"), QueueBind(1, "q", "amq.direct", "k"));

        AssertAnswered(written, QueueDeclareOk(1, "q"), BrokerClose(506, "RESOURCE_ERROR - out of space", 50, 20));
        Assert.IsEmpty(ExchangeChangesOf(events));
    }

    [TestMethod]
    public async Task QueueBind_OfAnExistingBindingOrToTheTopicSpaceOrAnUnknownQueue_IsNotAsked()
    {
        var plan = new ScriptedFaultPlan();

        await ServeFaultedAsync(
            plan,
            new RecordingSurlEventPublisher(),
            QueueDeclare(1, "q"),
            QueueBind(1, "q", "amq.direct", "k"),
            QueueBind(1, "q", "amq.direct", "k"),
            QueueBind(1, "q", "amq.topic", "a"),
            QueueBind(1, string.Empty, "amq.direct", "k2"),
            QueueBind(1, "nope", "amq.direct", "k"));

        CollectionAssert.AreEqual(new[] { "amq.direct", "amq.direct" }, StoreWritePointsOf(plan).Select(point => point.Path).ToArray());
    }

    [TestMethod]
    public async Task QueueBind_WithoutALastQueueOrWithABadMatchOrAnUnknownExchange_IsNotAsked()
    {
        var plan = new ScriptedFaultPlan();
        byte[] badMatch = [0, 0, 0, 16, .. ShortString("x-match"), (byte)'S', 0, 0, 0, 3, (byte)'b', (byte)'a', (byte)'d'];

        await ServeFaultedAsync(plan, new RecordingSurlEventPublisher(), QueueBind(1, string.Empty, "amq.direct", "k"));
        await ServeFaultedAsync(plan, new RecordingSurlEventPublisher(), QueueDeclare(1, "q"), QueueBind(1, "q", "amq.headers", "k", table: badMatch));
        await ServeFaultedAsync(plan, new RecordingSurlEventPublisher(), QueueDeclare(1, "q"), QueueBind(1, "q", "missing", "k"));
        await ServeFaultedAsync(plan, new RecordingSurlEventPublisher(), ExchangeDeclare(1, "bad name", "direct"));

        Assert.IsEmpty(StoreWritePointsOf(plan));
    }
}
