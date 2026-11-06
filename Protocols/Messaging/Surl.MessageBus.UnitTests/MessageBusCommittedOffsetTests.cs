using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// The committed-offset table (ADR-0145 decisions 4, 6, 9 and 10, BL-947): a commit is stored as
/// given and stamped with the bus's clock, replaces the group's earlier commit on the topic, is
/// refused past <see cref="MessageBus.MaxCommittedOffsets"/> only when it makes a new entry, needs
/// <c>sub</c> on the topic and on <c>$group/&lt;id&gt;</c>, meets the out-of-space fault, and
/// publishes one <see cref="StoreChange.Committed"/> named by the encoded group and the topic.
/// </summary>
[TestClass]
public sealed class MessageBusCommittedOffsetTests
{
    private readonly SettableTimeProvider clock = new();
    private readonly RecordingSurlEventPublisher events = new();

    [TestMethod]
    public void CommitOffset_WithAndWithoutMetadata_IsReadBackWithItsOffsetLeaderEpochMetadataAndCommitTime()
    {
        var bus = new MessageBus(clock);
        var firstTime = clock.UtcNow;

        var first = bus.CommitOffset("alice", "g", "t", 42, -1, metadata: null);
        clock.UtcNow = firstTime.AddSeconds(5);
        var second = bus.CommitOffset(null, "g", "a/b", -1, 7, "consumer-1");

        Assert.AreEqual(MessageBusOutcome.Succeeded, first);
        Assert.AreEqual(MessageBusOutcome.Succeeded, second);
        Assert.AreEqual(new BusCommittedOffset("g", "t", 42, -1, null, firstTime), bus.GetCommittedOffset("g", "t"));
        Assert.AreEqual(new BusCommittedOffset("g", "a/b", -1, 7, "consumer-1", firstTime.AddSeconds(5)), bus.GetCommittedOffset("g", "a/b"));
        Assert.IsNull(bus.GetCommittedOffset("g", "other"));
        Assert.IsNull(bus.GetCommittedOffset("h", "t"));
        CollectionAssert.AreEqual(new[] { "a/b", "t" }, bus.GetCommittedOffsets("g").Select(entry => entry.Topic).ToArray());
        Assert.IsEmpty(bus.GetCommittedOffsets("h"));
    }

    [TestMethod]
    public void GetCommittedGroups_ListsEachGroupHoldingAnEntryOnceInOrdinalOrder()
    {
        var bus = new MessageBus(clock);
        Assert.IsEmpty(bus.GetCommittedGroups());

        bus.CommitOffset(null, "b", "t", 1, -1, null);
        bus.CommitOffset(null, "B", "t", 1, -1, null);
        bus.CommitOffset(null, "b", "u", 1, -1, null);

        CollectionAssert.AreEqual(new[] { "B", "b" }, bus.GetCommittedGroups().ToArray());
    }

    [TestMethod]
    public void CommitOffset_AgainForTheSameGroupAndTopic_ReplacesTheEntry()
    {
        var bus = new MessageBus(clock);
        bus.CommitOffset(null, "g", "t", 1, -1, "old");
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        var replaced = bus.CommitOffset(null, "g", "t", 2, 4, null);

        Assert.AreEqual(MessageBusOutcome.Succeeded, replaced);
        Assert.AreEqual(new BusCommittedOffset("g", "t", 2, 4, null, clock.UtcNow), bus.GetCommittedOffsets("g").Single());
    }

    [TestMethod]
    public void CommitOffset_The10001stNewEntry_IsStoreFullButReplacingAnExistingOneSucceeds()
    {
        var bus = new MessageBus(clock, events: events);
        Assert.AreEqual(MessageBus.DefaultMaxCommittedOffsets, bus.MaxCommittedOffsets);
        for (var index = 0; index < MessageBus.DefaultMaxCommittedOffsets; index++)
        {
            Assert.AreEqual(MessageBusOutcome.Succeeded, bus.CommitOffset(null, "g", "t/" + index, index, -1, null));
        }

        var eventCount = events.Events.Count;
        var refused = bus.CommitOffset(null, "g", "t/new", 0, -1, null);
        var refusedInAnotherGroup = bus.CommitOffset(null, "h", "t/0", 0, -1, null);
        var replaced = bus.CommitOffset(null, "g", "t/0", 99, -1, null);

        Assert.AreEqual(MessageBusOutcome.StoreFull, refused);
        Assert.AreEqual(MessageBusOutcome.StoreFull, refusedInAnotherGroup);
        Assert.AreEqual(MessageBusOutcome.Succeeded, replaced);
        Assert.IsNull(bus.GetCommittedOffset("g", "t/new"));
        Assert.AreEqual(99, bus.GetCommittedOffset("g", "t/0")!.Offset);
        Assert.AreEqual(eventCount + 1, events.Events.Count);
    }

    [TestMethod]
    public void CommitOffset_WithoutSubOnTheTopicOrOnDollarGroup_IsAccessDeniedAndStoresNothing()
    {
        var bus = new MessageBus(clock, events: events, configuration: new()
        {
            AccessGrants =
            [
                new TopicAccessGrant("shop/#", "alice", TopicAccessRights.Subscribe),
                new TopicAccessGrant("$group/orders", "alice", TopicAccessRights.Subscribe),
                new TopicAccessGrant("shop/#", "bob", TopicAccessRights.Subscribe),
                new TopicAccessGrant("$group/orders", "bob", TopicAccessRights.Publish),
                new TopicAccessGrant("$group/orders", "carol", TopicAccessRights.Subscribe),
                new TopicAccessGrant("shop/#", "carol", TopicAccessRights.Publish),
            ],
        });

        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.CommitOffset("alice", "orders", "shop/x", 1, -1, null));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.CommitOffset("alice", "other", "shop/x", 1, -1, null));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.CommitOffset("alice", "orders", "elsewhere", 1, -1, null));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.CommitOffset("bob", "orders", "shop/x", 2, -1, null));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.CommitOffset("carol", "orders", "shop/x", 3, -1, null));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.CommitOffset(null, "orders", "shop/x", 4, -1, null));
        Assert.AreEqual(1, bus.GetCommittedOffset("orders", "shop/x")!.Offset);
        Assert.HasCount(1, events.Events);
        Assert.IsTrue(bus.MayUseGroup("alice", "orders"));
        Assert.IsFalse(bus.MayUseGroup("bob", "orders"));
    }

    [TestMethod]
    public void CommitOffset_AnOutOfSpaceRuleOnTheChangeName_IsStoreFullAndStoresNothing()
    {
        var plan = new SpendingFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Path = "$group/g" });
        var bus = new MessageBus(clock, events: events, faults: plan);

        var refused = bus.CommitOffset("alice", "g", "t", 1, -1, null);
        var taken = bus.CommitOffset("alice", "h", "t", 1, -1, null);

        Assert.AreEqual(MessageBusOutcome.StoreFull, refused);
        Assert.AreEqual(MessageBusOutcome.Succeeded, taken);
        Assert.IsNull(bus.GetCommittedOffset("g", "t"));
        Assert.AreEqual(new FaultPoint(FaultStage.StoreWrite) { Account = "alice", Path = "$group/g/t" }, plan.Points[0]);
        CollectionAssert.AreEqual(
            new SurlEvent[]
            {
                new FaultAppliedEvent("f1", FaultKind.OutOfSpace, FaultStage.StoreWrite, null, "$group/g/t"),
                Committed("$group/h/t"),
            },
            events.Events.ToArray());
    }

    [TestMethod]
    public void CommitOffset_EachCommit_PublishesOneCommittedNamedByTheEncodedGroupAndTheTopic()
    {
        var bus = new MessageBus(clock, events: events);

        bus.CommitOffset(null, "a/b%c", "x/y", 1, -1, null);
        bus.CommitOffset(null, "a/b%c", "x/y", 2, -1, null);
        bus.CommitOffset(null, "plain", "t", 1, -1, null);

        CollectionAssert.AreEqual(
            new[] { Committed("$group/a%2Fb%25c/x/y"), Committed("$group/a%2Fb%25c/x/y"), Committed("$group/plain/t") },
            events.Events.ToArray());
        Assert.AreEqual("$group/a%2Fb%25c/x/y", MessageBus.CommittedChangeName("a/b%c", "x/y"));
    }

    [TestMethod]
    public void CommitOffset_ATopicThatIsNotCanonical_IsInvalidTopicAndStoresNothing()
    {
        var bus = new MessageBus(clock, events: events);

        Assert.AreEqual(MessageBusOutcome.InvalidTopic, bus.CommitOffset(null, "g", "a/+", 1, -1, null));
        Assert.AreEqual(MessageBusOutcome.InvalidTopic, bus.CommitOffset(null, "g", string.Empty, 1, -1, null));
        Assert.IsEmpty(bus.GetCommittedGroups());
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public void CommittedOffsets_NullArgumentsOrABoundBelowOne_Throw()
    {
        var bus = new MessageBus(clock);

        Assert.ThrowsExactly<ArgumentNullException>(() => bus.CommitOffset(null, null!, "t", 0, -1, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.CommitOffset(null, "g", null!, 0, -1, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.GetCommittedOffset(null!, "t"));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.GetCommittedOffset("g", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.GetCommittedOffsets(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.MayUseGroup(null, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => MessageBus.CommittedChangeName(null!, "t"));
        Assert.ThrowsExactly<ArgumentNullException>(() => MessageBus.CommittedChangeName("g", null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MessageBus(clock, maxCommittedOffsets: 0));
        Assert.AreEqual(1, new MessageBus(clock, maxCommittedOffsets: 1).MaxCommittedOffsets);
    }

    private static StoreChangedEvent Committed(string name) =>
        new(StoreKind.Bus, StoreChange.Committed, name, ByteCount: null, ExchangeId: null);
}
