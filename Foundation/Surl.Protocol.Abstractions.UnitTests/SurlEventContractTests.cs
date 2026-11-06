using System.Net;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class SurlEventContractTests
{
    private static readonly string[] SecretWords = ["Password", "Secret", "Token"];

    [TestMethod]
    public void EventRecords_AreTheAdrsRecordsExactly()
    {
        var names = EventRecordTypes().Select(type => type.Name).Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                nameof(ConsumerGroupChangedEvent),
                nameof(ExchangeBytesEvent),
                nameof(ExchangeEndedEvent),
                nameof(ExchangeStartedEvent),
                nameof(FaultAppliedEvent),
                nameof(HookFailedEvent),
                nameof(LoginJudgedEvent),
                nameof(StoreChangedEvent),
            },
            names);
    }

    [TestMethod]
    public void EventRecords_NoPropertyNamesASecret()
    {
        var secretProperties = EventRecordTypes()
            .Append(typeof(SurlEvent))
            .SelectMany(type => type.GetProperties().Select(property => $"{type.Name}.{property.Name}"))
            .Where(name => SecretWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.IsEmpty(secretProperties, string.Join(", ", secretProperties));
    }

    [TestMethod]
    public void SurlEvent_Unstamped_HasSequenceZeroAndDefaultTime()
    {
        var surlEvent = new HookFailedEvent("h1", HookFailure.TimedOut, "override ran past 1 second");

        Assert.AreEqual(0, surlEvent.Sequence);
        Assert.AreEqual(default, surlEvent.At);
    }

    [TestMethod]
    public void SurlEvent_StampedWithWith_KeepsItsMembersAndGainsTheStamp()
    {
        var client = new IPEndPoint(IPAddress.Loopback, 50000);
        var at = new DateTimeOffset(2026, 11, 5, 9, 30, 0, TimeSpan.Zero);
        var started = new ExchangeStartedEvent(4, "http://127.0.0.1:8080", "http", client);

        var stamped = started with { Sequence = 1, At = at };

        Assert.AreEqual(4, stamped.ExchangeId);
        Assert.AreEqual("http://127.0.0.1:8080", stamped.ListenUrl);
        Assert.AreEqual("http", stamped.Scheme);
        Assert.AreSame(client, stamped.Client);
        Assert.AreEqual(1, stamped.Sequence);
        Assert.AreEqual(at, stamped.At);
    }

    [TestMethod]
    public void DiscardingSurlEventPublisher_PublishesNothingAndAsksForNoBytes()
    {
        var publisher = DiscardingSurlEventPublisher.Instance;

        publisher.Publish(new ExchangeBytesEvent(1, ExchangeDirection.Sent, new byte[] { 1 }));

        Assert.IsFalse(publisher.IsPublishingBytes);
    }

    [TestMethod]
    public void FaultPoint_UnsetAttributes_AreNull()
    {
        var point = new FaultPoint(FaultStage.StoreWrite);

        Assert.AreEqual(FaultStage.StoreWrite, point.Stage);
        Assert.IsNull(point.ExchangeId);
        Assert.IsNull(point.ListenUrl);
        Assert.IsNull(point.Scheme);
        Assert.IsNull(point.Account);
        Assert.IsNull(point.Path);
        Assert.IsNull(point.Command);
    }

    [TestMethod]
    public void FaultRule_CarriesItsScopeAndParameters()
    {
        var rule = new FaultRule("f2", FaultKind.Drop) { AfterBytes = 10, Times = 3, Stage = FaultStage.Command };

        Assert.AreEqual("f2", rule.Id);
        Assert.AreEqual(FaultKind.Drop, rule.Kind);
        Assert.AreEqual(10, rule.AfterBytes);
        Assert.AreEqual(3, rule.Times);
        Assert.AreEqual(FaultStage.Command, rule.Stage);
        Assert.IsNull(rule.DelayMilliseconds);
        Assert.IsNull(rule.Reply);
    }

    [TestMethod]
    public void ConsumerGroupChange_GetValues_IsAdr0145sSixChangesInOrder()
    {
        var changes = Enum.GetValues<ConsumerGroupChange>();

        CollectionAssert.AreEqual(
            new[]
            {
                ConsumerGroupChange.MemberJoined,
                ConsumerGroupChange.MemberLeft,
                ConsumerGroupChange.MemberRemoved,
                ConsumerGroupChange.MemberFenced,
                ConsumerGroupChange.GenerationStarted,
                ConsumerGroupChange.AssignmentDistributed,
            },
            changes);
        Assert.AreEqual(0, (int)changes[0]);
    }

    [TestMethod]
    public void ConsumerGroupChangedEvent_Constructed_KeepsEveryMember()
    {
        var changed = new ConsumerGroupChangedEvent(7, "kafka", "orders", ConsumerGroupChange.MemberJoined, "c-1", 2, "alice");

        Assert.AreEqual(7, changed.ExchangeId);
        Assert.AreEqual("kafka", changed.Scheme);
        Assert.AreEqual("orders", changed.Group);
        Assert.AreEqual(ConsumerGroupChange.MemberJoined, changed.Change);
        Assert.AreEqual("c-1", changed.MemberId);
        Assert.AreEqual(2, changed.GenerationId);
        Assert.AreEqual("alice", changed.Account);
    }

    [TestMethod]
    public void StoreChange_Committed_IsTheLastMember()
    {
        Assert.AreEqual(StoreChange.Committed, Enum.GetValues<StoreChange>()[^1]);
    }

    private static IEnumerable<Type> EventRecordTypes() =>
        typeof(SurlEvent).Assembly.GetExportedTypes()
            .Where(type => type.IsSubclassOf(typeof(SurlEvent)));
}
