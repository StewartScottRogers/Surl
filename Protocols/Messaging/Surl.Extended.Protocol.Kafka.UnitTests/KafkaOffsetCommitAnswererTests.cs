using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// OffsetCommit v2 to v7 (BL-949; ADR-0145 decisions 4, 6 and 10), every byte written by hand from the
/// Kafka protocol guide's OffsetCommit schemas: <c>group_id generation_id_or_member_epoch member_id
/// group_instance_id (v7) retention_time_ms (v2 to v4) topics [name partitions [partition_index
/// committed_offset committed_leader_epoch (v6+) committed_metadata]]</c>, answered
/// <c>throttle_time_ms (v3+) topics [name partitions [partition_index error_code]]</c>.
/// </summary>
[TestClass]
public sealed class KafkaOffsetCommitAnswererTests
{
    private static readonly string A = MemberId("c", 1);

    public TestContext TestContext { get; set; } = null!;

    private static MessageBusConfiguration Grants(params TopicAccessGrant[] grants) => new() { AccessGrants = grants };

    private static StoreChangedEvent Committed(string name) => new(StoreKind.Bus, StoreChange.Committed, name, ByteCount: null, ExchangeId: null);

    private static async Task<KafkaGroupHarness> StableGroupAsync(CancellationToken cancellationToken)
    {
        var harness = new KafkaGroupHarness(cancellationToken);
        harness.Bus.CreateLog("t");
        await harness.JoinV3Async("");
        await harness.SendAsync(SyncGroup(3, "g", 1, A, null, (A, "assigned")));
        return harness;
    }

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var bus = new Surl.MessageBus.MessageBus(new ManualTimeProvider());
        var coordinator = new KafkaGroupCoordinator(new ManualTimeProvider());
        var answerer = new KafkaOffsetCommitAnswerer(coordinator, bus);
        var request = new KafkaRequest(new KafkaRequestHeader(8, 2, 1, null), KafkaServedApis.Find(8)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaOffsetCommitAnswerer(null!, bus));
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaOffsetCommitAnswerer(coordinator, null!));
        Assert.AreEqual(KafkaApiKey.OffsetCommit, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    [DataRow((short)2)]
    [DataRow((short)3)]
    [DataRow((short)4)]
    [DataRow((short)5)]
    [DataRow((short)6)]
    [DataRow((short)7)]
    public async Task ServeAsync_SimpleCommitIntoAGroupWithNoMembers_IsStoredInEveryVersion(short version)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CreateLog("t");

        var written = await harness.SendAsync(OffsetCommit(version, "g", -1, "", null, ("t", [new Commit(0, 42, 3, "meta")])));

        CollectionAssert.AreEqual(OffsetCommitAnswer(version, "t", 0), written);
        var stored = harness.Bus.GetCommittedOffset("g", "t")!;
        Assert.AreEqual(42, stored.Offset);
        Assert.AreEqual(version >= 6 ? 3 : -1, stored.LeaderEpoch);
        Assert.AreEqual("meta", stored.Metadata);
    }

    [TestMethod]
    public async Task ServeAsync_MemberOfTheCurrentGeneration_CommitIsStoredAndFetchedBackWithOneCommittedEvent()
    {
        var harness = await StableGroupAsync(TestContext.CancellationToken);

        var committed = await harness.SendAsync(OffsetCommit(7, "g", 1, A, null, ("t", [new Commit(0, 42, 3, "meta")])));
        var fetched = await harness.SendAsync(OffsetFetch(5, "g", ("t", [0])));

        CollectionAssert.AreEqual(OffsetCommitAnswer(7, "t", 0), committed);
        CollectionAssert.AreEqual(OffsetFetchAnswer(5, 0, ("t", [new Fetched(0, 42, 3, "meta", 0)])), fetched);
        CollectionAssert.AreEqual(new[] { Committed("$group/g/t") }, harness.Events.Events.OfType<StoreChangedEvent>().Where(change => change.Change == StoreChange.Committed).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_TwoTopicsStored_PublishOneCommittedEventForEachAndNullMetadataStaysNull()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CreateLog("t");
        harness.Bus.CreateLog("u");

        var written = await harness.SendAsync(OffsetCommit(2, "g", -1, "", null, ("t", [new Commit(0, 1)]), ("u", [new Commit(0, 2)])));

        CollectionAssert.AreEqual(OffsetCommitAnswer(2, ("t", [(0, 0)]), ("u", [(0, 0)])), written);
        CollectionAssert.AreEqual(
            new[] { Committed("$group/g/t"), Committed("$group/g/u") },
            harness.Events.Events.OfType<StoreChangedEvent>().Where(change => change.Change == StoreChange.Committed).ToArray());
        Assert.IsNull(harness.Bus.GetCommittedOffset("g", "t")!.Metadata);
    }

    [TestMethod]
    public async Task ServeAsync_SimpleCommitIntoAGroupWithMembers_IsUnknownMemberIdAndStoresNothing()
    {
        var harness = await StableGroupAsync(TestContext.CancellationToken);

        var written = await harness.SendAsync(SimpleCommit("g", "t", 5));

        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "t", 25), written);
        Assert.IsNull(harness.Bus.GetCommittedOffset("g", "t"));
    }

    [TestMethod]
    public async Task ServeAsync_SimpleCommitIntoAnotherGroupWhileOneHasMembers_IsStored()
    {
        var harness = await StableGroupAsync(TestContext.CancellationToken);

        var written = await harness.SendAsync(SimpleCommit("other", "t", 5));

        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "t", 0), written);
        Assert.AreEqual(5, harness.Bus.GetCommittedOffset("other", "t")!.Offset);
    }

    [TestMethod]
    public async Task ServeAsync_AnotherGeneration_IsIllegalGeneration()
    {
        var harness = await StableGroupAsync(TestContext.CancellationToken);

        var written = await harness.SendAsync(OffsetCommit(2, "g", 2, A, null, ("t", [new Commit(0, 5)])));

        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "t", 22), written);
        Assert.IsNull(harness.Bus.GetCommittedOffset("g", "t"));
    }

    [TestMethod]
    public async Task ServeAsync_UnknownMemberAndFencedInstance_AreUnknownMemberIdAndFencedInstanceId()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CreateLog("t");
        var staticId = MemberId("i", 1);
        await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, "", "i", Consumer, Protocols()));
        await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, staticId, "i", Consumer, Protocols()));

        var unknown = await harness.SendAsync(OffsetCommit(7, "g", 1, "unknown", null, ("t", [new Commit(0, 5)])));
        var fenced = await harness.SendAsync(OffsetCommit(7, "g", 1, "unknown", "i", ("t", [new Commit(0, 5)])));
        var unknownGroup = await harness.SendAsync(OffsetCommit(2, "nobody", 1, "unknown", null, ("t", [new Commit(0, 5)])));

        CollectionAssert.AreEqual(OffsetCommitAnswer(7, "t", 25), unknown);
        CollectionAssert.AreEqual(OffsetCommitAnswer(7, "t", 82), fenced);
        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "t", 25), unknownGroup);
    }

    [TestMethod]
    public async Task ServeAsync_GroupCompletingARebalance_IsRebalanceInProgressAndStoresNothing()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CreateLog("t");
        await harness.JoinV3Async("");

        var written = await harness.SendAsync(OffsetCommit(2, "g", 1, A, null, ("t", [new Commit(0, 5)])));

        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "t", 27), written);
        Assert.IsNull(harness.Bus.GetCommittedOffset("g", "t"));
    }

    [TestMethod]
    public async Task ServeAsync_GroupPreparingARebalance_TakesTheCommit()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CreateLog("t");
        await harness.JoinV3Async("");
        var (follower, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));

        var written = await harness.SendAsync(OffsetCommit(2, "g", 1, A, null, ("t", [new Commit(0, 5)])));
        await harness.JoinV3Async(A);
        await follower;

        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "t", 0), written);
        Assert.AreEqual(5, harness.Bus.GetCommittedOffset("g", "t")!.Offset);
    }

    [TestMethod]
    public async Task ServeAsync_MembersCommit_RefreshesItsSession()
    {
        var harness = await StableGroupAsync(TestContext.CancellationToken);
        var clock = (ManualTimeProvider)harness.Clock;

        clock.Advance(TimeSpan.FromSeconds(9));
        var committed = await harness.SendAsync(OffsetCommit(2, "g", 1, A, null, ("t", [new Commit(0, 5)])));
        clock.Advance(TimeSpan.FromSeconds(9));
        var heartbeat = await harness.SendAsync(Heartbeat(0, "g", 1, A));

        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "t", 0), committed);
        CollectionAssert.AreEqual(ErrorAnswer(0, 0), heartbeat);
    }

    [TestMethod]
    public async Task ServeAsync_EachPartition_IsAnsweredOnItsOwnWithItsOwnError()
    {
        var harness = new KafkaGroupHarness(
            TestContext.CancellationToken,
            busConfiguration: Grants(
                new TopicAccessGrant("t", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                new TopicAccessGrant("nolog", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                new TopicAccessGrant("$group/g", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe)));
        harness.Bus.CreateLog("t");
        harness.Bus.CreateLog("secret");

        var written = await harness.SendAsync(OffsetCommit(
            2,
            "g",
            -1,
            "",
            null,
            ("bad name!", [new Commit(0, 1)]),
            ("nolog", [new Commit(0, 1)]),
            ("secret", [new Commit(0, 1)]),
            ("t", [new Commit(1, 1), new Commit(0, 2, -1, new string('x', 4097)), new Commit(0, 3, -1, new string('x', 4096)), new Commit(0, 7)])));

        CollectionAssert.AreEqual(
            OffsetCommitAnswer(2, ("bad name!", [(0, 17)]), ("nolog", [(0, 3)]), ("secret", [(0, 29)]), ("t", [(1, 3), (0, 12), (0, 0), (0, 0)])),
            written);
        Assert.AreEqual(7, harness.Bus.GetCommittedOffset("g", "t")!.Offset);
        Assert.IsNull(harness.Bus.GetCommittedOffset("g", "secret"));
        Assert.HasCount(2, harness.Events.Events.OfType<StoreChangedEvent>().Where(change => change.Change == StoreChange.Committed).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_TheBusBound_IsPolicyViolationForANewEntryButAReplacementIsTaken()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, maxCommittedOffsets: 1);
        harness.Bus.CreateLog("t");
        harness.Bus.CreateLog("u");

        var first = await harness.SendAsync(SimpleCommit("g", "t", 1));
        var refused = await harness.SendAsync(SimpleCommit("g", "u", 1));
        var replaced = await harness.SendAsync(SimpleCommit("g", "t", 2));

        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "t", 0), first);
        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "u", 44), refused);
        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "t", 0), replaced);
        Assert.IsNull(harness.Bus.GetCommittedOffset("g", "u"));
    }

    [TestMethod]
    public async Task ServeAsync_DeniedGroup_IsGroupAuthorizationFailedInEveryPartitionAndStoresNothing()
    {
        var harness = new KafkaGroupHarness(
            TestContext.CancellationToken,
            busConfiguration: Grants(new TopicAccessGrant("t", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe)));
        harness.Bus.CreateLog("t");

        var written = await harness.SendAsync(OffsetCommit(2, "g", -1, "", null, ("t", [new Commit(0, 1), new Commit(5, 1)]), ("nolog", [new Commit(0, 1)])));

        CollectionAssert.AreEqual(OffsetCommitAnswer(2, ("t", [(0, 30), (5, 30)]), ("nolog", [(0, 30)])), written);
        Assert.IsEmpty(harness.Bus.GetCommittedGroups());
    }

    [TestMethod]
    public async Task ServeAsync_EmptyGroupId_IsInvalidGroupId()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CreateLog("t");

        var written = await harness.SendAsync(SimpleCommit("", "t", 1));

        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "t", 24), written);
        Assert.IsEmpty(harness.Bus.GetCommittedGroups());
    }

    [TestMethod]
    public async Task ServeAsync_NoTopics_AnswersAnEmptyArray()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(OffsetCommit(3, "g", -1, "", null));

        CollectionAssert.AreEqual(OffsetCommitAnswer(3), written);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRule_AnswersItsCodeInEveryPartitionAndStoresNothing()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CreateLog("t");
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "OffsetCommit", Reply = "15" });

        var refused = await harness.SendAsync(OffsetCommit(2, "g", -1, "", null, ("t", [new Commit(0, 1), new Commit(3, 1)]), ("u", [new Commit(0, 1)])));
        harness.Faults = EmptyFaultPlan.Instance;
        var taken = await harness.SendAsync(SimpleCommit("g", "t", 1));

        CollectionAssert.AreEqual(OffsetCommitAnswer(2, ("t", [(0, 15), (3, 15)]), ("u", [(0, 15)])), refused);
        CollectionAssert.AreEqual(OffsetCommitAnswer(2, "t", 0), taken);
        Assert.AreEqual(1, harness.Bus.GetCommittedOffset("g", "t")!.Offset);
        Assert.IsNull(harness.Bus.GetCommittedOffset("g", "u"));
    }

    [TestMethod]
    public async Task ServeAsync_DropRule_ClosesWithNoBytesAndStoresNothing()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CreateLog("t");
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "OffsetCommit" });

        var written = await harness.SendAsync(SimpleCommit("g", "t", 1));

        Assert.IsEmpty(written);
        Assert.Contains("OffsetCommit was dropped by fault rule f1; closed with no reply.", harness.Log.Notes);
        Assert.IsNull(harness.Bus.GetCommittedOffset("g", "t"));
    }
}
