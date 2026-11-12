using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// OffsetFetch v1 to v5 (BL-949; ADR-0145 decisions 4, 6 and 10), every byte written by hand from the
/// Kafka protocol guide's OffsetFetch schemas: <c>group_id topics [name partition_indexes]</c>
/// (null topics from v2), answered <c>throttle_time_ms (v3+) topics [name partitions [partition_index
/// committed_offset committed_leader_epoch (v5) metadata error_code]] error_code (v2+)</c>.
/// </summary>
[TestClass]
public sealed class KafkaOffsetFetchAnswererTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var bus = new Surl.MessageBus.MessageBus(new ManualTimeProvider());
        var answerer = new KafkaOffsetFetchAnswerer(bus);
        var request = new KafkaRequest(new KafkaRequestHeader(9, 1, 1, null), KafkaServedApis.Find(9)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaOffsetFetchAnswerer(null!));
        Assert.AreEqual(KafkaApiKey.OffsetFetch, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    [DataRow((short)1)]
    [DataRow((short)2)]
    [DataRow((short)3)]
    [DataRow((short)4)]
    [DataRow((short)5)]
    public async Task ServeAsync_NamedTopics_AnswersCommittedAndNotCommittedPartitionsInEveryVersion(short version)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CommitOffset(null, "g", "t", 42, 3, "meta");
        harness.Bus.CommitOffset(null, "g", "plain", 9, -1, null);

        var written = await harness.SendAsync(OffsetFetch(version, "g", ("t", [0]), ("plain", [0]), ("never", [0]), ("other-log", [0])));

        CollectionAssert.AreEqual(
            OffsetFetchAnswer(
                version,
                0,
                ("t", [new Fetched(0, 42, 3, "meta", 0)]),
                ("plain", [new Fetched(0, 9, -1, "", 0)]),
                ("never", [NotCommitted()]),
                ("other-log", [NotCommitted()])),
            written);
    }

    [TestMethod]
    public async Task ServeAsync_AGroupThatDoesNotExist_AnswersMinusOneForEveryPartition()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(OffsetFetch(5, "nobody", ("t", [0])));

        CollectionAssert.AreEqual(OffsetFetchAnswer(5, 0, ("t", [NotCommitted()])), written);
    }

    [TestMethod]
    public async Task ServeAsync_EachPartition_IsAnsweredWithItsOwnError()
    {
        var harness = new KafkaGroupHarness(
            TestContext.CancellationToken,
            busConfiguration: new MessageBusConfiguration
            {
                AccessGrants =
                [
                    new TopicAccessGrant("t", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                    new TopicAccessGrant("$group/g", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                ],
            });
        harness.Bus.CommitOffset(null, "g", "t", 42, -1, null);

        var written = await harness.SendAsync(OffsetFetch(5, "g", ("bad name!", [0]), ("secret", [0]), ("t", [1, 0, 2])));

        CollectionAssert.AreEqual(
            OffsetFetchAnswer(
                5,
                0,
                ("bad name!", [NotCommitted(17) with { Index = 0 }]),
                ("secret", [NotCommitted(29)]),
                ("t", [NotCommitted(3) with { Index = 1 }, new Fetched(0, 42, -1, "", 0), NotCommitted(3) with { Index = 2 }])),
            written);
    }

    [TestMethod]
    [DataRow((short)2)]
    [DataRow((short)5)]
    public async Task ServeAsync_NullTopics_ListsEveryCommittedTopicKafkaCanNameWithDotsForSlashesInOrdinalOrder(short version)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CommitOffset(null, "g", "b", 2, 7, "bm");
        Assert.AreEqual(MessageBusOutcome.Succeeded, harness.Bus.CommitOffset(null, "g", "x/y", 3, -1, null));
        Assert.AreEqual(MessageBusOutcome.Succeeded, harness.Bus.CommitOffset(null, "g", "no way", 5, -1, null));
        harness.Bus.CommitOffset(null, "g", "a", 1, -1, null);
        harness.Bus.CommitOffset(null, "h", "c", 4, -1, null);

        var written = await harness.SendAsync(OffsetFetch(version, "g", topics: null));

        CollectionAssert.AreEqual(
            OffsetFetchAnswer(
                version,
                0,
                ("a", [new Fetched(0, 1, -1, "", 0)]),
                ("b", [new Fetched(0, 2, 7, "bm", 0)]),
                ("x.y", [new Fetched(0, 3, -1, "", 0)])),
            written);
    }

    [TestMethod]
    public async Task ServeAsync_NullTopicsOfAGroupThatCommittedNothing_ListsNoTopics()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(OffsetFetch(3, "g", topics: null));

        CollectionAssert.AreEqual(OffsetFetchAnswer(3, 0), written);
    }

    [TestMethod]
    public async Task ServeAsync_NullTopicsLeaveOutTopicsTheCallerMayNotSubscribeTo()
    {
        var harness = new KafkaGroupHarness(
            TestContext.CancellationToken,
            busConfiguration: new MessageBusConfiguration
            {
                AccessGrants =
                [
                    new TopicAccessGrant("open", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                    new TopicAccessGrant("closed", "alice", TopicAccessRights.Subscribe),
                    new TopicAccessGrant("$group/g", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                ],
            });
        harness.Bus.CommitOffset(null, "g", "open", 1, -1, null);
        harness.Bus.CommitOffset("alice", "g", "closed", 2, -1, null);

        var written = await harness.SendAsync(OffsetFetch(5, "g", topics: null));

        CollectionAssert.AreEqual(OffsetFetchAnswer(5, 0, ("open", [new Fetched(0, 1, -1, "", 0)])), written);
    }

    [TestMethod]
    public async Task ServeAsync_V1NullTopics_AsksForNoTopics()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CommitOffset(null, "g", "t", 1, -1, null);

        var written = await harness.SendAsync(OffsetFetch(1, "g", topics: null));

        CollectionAssert.AreEqual(OffsetFetchAnswer(1, 0), written);
    }

    [TestMethod]
    public async Task ServeAsync_DeniedGroupV2ToV5_IsTopLevelGroupAuthorizationFailedWithNoTopics()
    {
        var harness = DeniedGroupHarness();
        harness.Bus.CommitOffset("alice", "g", "t", 1, -1, null);

        var named = await harness.SendAsync(OffsetFetch(5, "g", ("t", [0])));
        var all = await harness.SendAsync(OffsetFetch(2, "g", topics: null));

        CollectionAssert.AreEqual(OffsetFetchAnswer(5, 30), named);
        CollectionAssert.AreEqual(OffsetFetchAnswer(2, 30), all);
    }

    [TestMethod]
    public async Task ServeAsync_DeniedGroupV1_IsGroupAuthorizationFailedInEveryPartitionAsked()
    {
        var harness = DeniedGroupHarness();

        var written = await harness.SendAsync(OffsetFetch(1, "g", ("t", [0, 1]), ("u", [0])));

        CollectionAssert.AreEqual(
            OffsetFetchAnswer(1, 0, ("t", [NotCommitted(30), NotCommitted(30) with { Index = 1 }]), ("u", [NotCommitted(30)])),
            written);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRule_IsTopLevelFromV2AndPerPartitionInV1()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CommitOffset(null, "g", "t", 1, -1, null);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "OffsetFetch", Reply = "15" });

        var v3 = await harness.SendAsync(OffsetFetch(3, "g", ("t", [0])));
        var v1 = await harness.SendAsync(OffsetFetch(1, "g", ("t", [0])));

        CollectionAssert.AreEqual(OffsetFetchAnswer(3, 15), v3);
        CollectionAssert.AreEqual(OffsetFetchAnswer(1, 0, ("t", [NotCommitted(15)])), v1);
    }

    [TestMethod]
    public async Task ServeAsync_DropRule_ClosesWithNoBytes()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "OffsetFetch" });

        var written = await harness.SendAsync(OffsetFetch(2, "g", topics: null));

        Assert.IsEmpty(written);
        Assert.Contains("OffsetFetch was dropped by fault rule f1; closed with no reply.", harness.Log.Notes);
    }

    private KafkaGroupHarness DeniedGroupHarness() => new(
        TestContext.CancellationToken,
        busConfiguration: new MessageBusConfiguration
        {
            AccessGrants =
            [
                new TopicAccessGrant("t", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                new TopicAccessGrant("$group/g", "alice", TopicAccessRights.Subscribe),
            ],
        });
}
