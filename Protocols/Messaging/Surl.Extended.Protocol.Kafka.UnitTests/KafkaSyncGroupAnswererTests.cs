using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// SyncGroup v0 to v3 (BL-948; ADR-0145 decisions 3, 6, 9 and 10), every byte written by hand from
/// the Kafka protocol guide's SyncGroup schemas (<see cref="KafkaGroupHarness.SyncGroup"/> and
/// <see cref="KafkaGroupHarness.SyncAnswer"/>).
/// </summary>
[TestClass]
public sealed class KafkaSyncGroupAnswererTests
{
    private static readonly string A = MemberId("c", 1);
    private static readonly string B = MemberId("c", 2);
    private static readonly string C = MemberId("c", 3);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var bus = new Surl.MessageBus.MessageBus(new ManualTimeProvider());
        var coordinator = new KafkaGroupCoordinator(new ManualTimeProvider());
        var answerer = new KafkaSyncGroupAnswerer(coordinator, bus);
        var request = new KafkaRequest(new KafkaRequestHeader(14, 0, 1, null), KafkaServedApis.Find(14)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaSyncGroupAnswerer(null!, bus));
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaSyncGroupAnswerer(coordinator, null!));
        Assert.AreEqual(KafkaApiKey.SyncGroup, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    public async Task ServeAsync_FollowersHeldUntilTheLeadersSync_EachReceiveExactlyTheirBytesAndAnUnnamedMemberNone()
    {
        var harness = await ThreeMembersOfGenerationTwoAsync();

        var (b, _) = await harness.SendHeldAsync(SyncGroup(1, "g", 2, B, null));
        var (c, _) = await harness.SendHeldAsync(SyncGroup(0, "g", 2, C, null));
        var leader = await harness.SendAsync(SyncGroup(3, "g", 2, A, null, (A, "a-bytes"), (B, "b-bytes"), ("ghost", "x")));

        CollectionAssert.AreEqual(SyncAnswer(3, 0, "a-bytes"), leader);
        CollectionAssert.AreEqual(SyncAnswer(1, 0, "b-bytes"), await b);
        CollectionAssert.AreEqual(SyncAnswer(0, 0), await c);
        Assert.AreEqual(new ConsumerGroupChangedEvent(1, "kafka", "g", ConsumerGroupChange.AssignmentDistributed, null, 2, null), harness.GroupEvents[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_StableGroup_AnswersTheMembersAssignmentAtOnce()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        await harness.SendAsync(SyncGroup(2, "g", 1, A, null, (A, "a-bytes")));

        var written = await harness.SendAsync(SyncGroup(2, "g", 1, A, null));

        CollectionAssert.AreEqual(SyncAnswer(2, 0, "a-bytes"), written);
    }

    [TestMethod]
    [DataRow("g", "unknown", 1, (short)25)]
    [DataRow("other", "unknown", 1, (short)25)]
    [DataRow("g", "member", 2, (short)22)]
    public async Task ServeAsync_UnknownMemberOrAnotherGeneration_IsRefused(string group, string member, int generationId, short error)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");

        var written = await harness.SendAsync(SyncGroup(1, group, generationId, member == "member" ? A : member, null));

        CollectionAssert.AreEqual(SyncAnswer(1, error), written);
    }

    [TestMethod]
    public async Task ServeAsync_FencedInstance_IsFencedInstanceId()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, "", "i", Consumer, Protocols()));
        await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, "", "i", Consumer, Protocols()));

        var written = await harness.SendAsync(SyncGroup(3, "g", 2, MemberId("i", 1), "i"));

        CollectionAssert.AreEqual(SyncAnswer(3, 82), written);
    }

    [TestMethod]
    public async Task ServeAsync_GroupPreparingARebalance_IsRebalanceInProgress()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));

        var written = await harness.SendAsync(SyncGroup(1, "g", 1, A, null));

        CollectionAssert.AreEqual(SyncAnswer(1, 27), written);
    }

    [TestMethod]
    public async Task ServeAsync_LeadersAssignmentsPastTheStateBound_ArePolicyViolationAndTheHeldFollowerRebalances()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, clock => new KafkaGroupCoordinator(clock, maxGroupStateBytes: 20));
        await harness.JoinV3Async("");
        var (follower, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));
        await harness.JoinV3Async(A);
        await follower;
        var (followerSync, _) = await harness.SendHeldAsync(SyncGroup(1, "g", 2, B, null));

        var leader = await harness.SendAsync(SyncGroup(1, "g", 2, A, null, (A, "0123456789")));

        CollectionAssert.AreEqual(SyncAnswer(1, 44), leader);
        CollectionAssert.AreEqual(SyncAnswer(1, 27), await followerSync);
        CollectionAssert.AreEqual(ErrorAnswer(1, 27), await harness.SendAsync(Heartbeat(1, "g", 2, A)));
    }

    [TestMethod]
    public async Task ServeAsync_DeniedGroup_IsGroupAuthorizationFailed()
    {
        var harness = new KafkaGroupHarness(
            TestContext.CancellationToken,
            busConfiguration: new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("t", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe)] });

        var written = await harness.SendAsync(SyncGroup(1, "g", 1, A, null));

        CollectionAssert.AreEqual(SyncAnswer(1, 30), written);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRule_AnswersItsCodeAndHandsOutNothing()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "SyncGroup", Reply = "15" });

        var refused = await harness.SendAsync(SyncGroup(1, "g", 1, A, null, (A, "a-bytes")));
        harness.Faults = EmptyFaultPlan.Instance;
        var heartbeat = await harness.SendAsync(Heartbeat(1, "g", 1, A));

        CollectionAssert.AreEqual(SyncAnswer(1, 15), refused);
        CollectionAssert.AreEqual(ErrorAnswer(1, 0), heartbeat);
        Assert.IsFalse(harness.GroupEvents.Any(change => change.Change == ConsumerGroupChange.AssignmentDistributed));
    }

    [TestMethod]
    public async Task ServeAsync_DropRule_ClosesWithNoBytes()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "SyncGroup" });

        var written = await harness.SendAsync(SyncGroup(1, "g", 1, A, null));

        Assert.IsEmpty(written);
        Assert.Contains("SyncGroup was dropped by fault rule f1; closed with no reply.", harness.Log.Notes);
    }

    // A leads B and C in generation 2, the group completing its rebalance.
    private async Task<KafkaGroupHarness> ThreeMembersOfGenerationTwoAsync()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        var (b, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));
        var (c, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));
        await harness.JoinV3Async(A);
        await Task.WhenAll(b, c);
        return harness;
    }
}
