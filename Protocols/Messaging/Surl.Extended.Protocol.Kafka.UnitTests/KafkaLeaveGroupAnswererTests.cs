using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// LeaveGroup v0 to v3 (BL-948; ADR-0145 decisions 3, 6 and 10), every byte written by hand from the
/// Kafka protocol guide's LeaveGroup schemas: v0 to v2 are <c>group_id member_id</c>, answered
/// <c>throttle_time_ms</c> (v1+) <c>error_code</c>; v3 is <c>group_id members [member_id
/// group_instance_id]</c>, answered <c>throttle_time_ms error_code members [member_id
/// group_instance_id error_code]</c>.
/// </summary>
[TestClass]
public sealed class KafkaLeaveGroupAnswererTests
{
    private static readonly string A = MemberId("c", 1);
    private static readonly string B = MemberId("c", 2);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var bus = new Surl.MessageBus.MessageBus(new ManualTimeProvider());
        var coordinator = new KafkaGroupCoordinator(new ManualTimeProvider());
        var answerer = new KafkaLeaveGroupAnswerer(coordinator, bus);
        var request = new KafkaRequest(new KafkaRequestHeader(13, 0, 1, null), KafkaServedApis.Find(13)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaLeaveGroupAnswerer(null!, bus));
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaLeaveGroupAnswerer(coordinator, null!));
        Assert.AreEqual(KafkaApiKey.LeaveGroup, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    public async Task ServeAsync_V0LastMember_LeavesAndTheGroupIsForgotten()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");

        var left = await harness.SendAsync(LeaveGroup(0, "g", A));
        var heartbeat = await harness.SendAsync(Heartbeat(0, "g", 1, A));
        var rejoined = await harness.JoinV3Async("");

        CollectionAssert.AreEqual(ErrorAnswer(0, 0), left);
        CollectionAssert.AreEqual(ErrorAnswer(0, 25), heartbeat);
        CollectionAssert.AreEqual(JoinAnswer(3, 0, 1, "range", B, B, (B, null, "m-range")), rejoined);
        Assert.AreEqual(new ConsumerGroupChangedEvent(1, "kafka", "g", ConsumerGroupChange.MemberLeft, A, 1, null), harness.GroupEvents.Single(change => change.Change == ConsumerGroupChange.MemberLeft));
    }

    [TestMethod]
    [DataRow((short)0, "g", "unknown")]
    [DataRow((short)1, "g", "")]
    [DataRow((short)2, "other", "")]
    public async Task ServeAsync_V0ToV2MemberTheGroupDoesNotHold_IsUnknownMemberId(short version, string group, string memberId)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");

        var written = await harness.SendAsync(LeaveGroup(version, group, memberId));

        CollectionAssert.AreEqual(ErrorAnswer(version, 25), written);
    }

    [TestMethod]
    public async Task ServeAsync_MemberLeavingAStableGroup_RebalancesTheRest()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        var (follower, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));
        await harness.JoinV3Async(A);
        await follower;

        var left = await harness.SendAsync(LeaveGroup(1, "g", B));
        var heartbeat = await harness.SendAsync(Heartbeat(1, "g", 2, A));
        var rejoined = await harness.JoinV3Async(A);

        CollectionAssert.AreEqual(ErrorAnswer(1, 0), left);
        CollectionAssert.AreEqual(ErrorAnswer(1, 27), heartbeat);
        CollectionAssert.AreEqual(JoinAnswer(3, 0, 3, "range", A, A, (A, null, "m-range")), rejoined);
    }

    [TestMethod]
    public async Task ServeAsync_MemberLeavingWhileItsJoinIsHeld_HasThatJoinAnsweredUnknownMemberId()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        var (held, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));

        var left = await harness.SendAsync(LeaveGroup(0, "g", B));
        var leader = await harness.JoinV3Async(A);

        CollectionAssert.AreEqual(ErrorAnswer(0, 0), left);
        CollectionAssert.AreEqual(JoinAnswer(3, 0, 2, "range", A, A, (A, null, "m-range")), leader);
        CollectionAssert.AreEqual(JoinRefused(3, 25, B), await held);
    }

    [TestMethod]
    public async Task ServeAsync_V3_LeavesEachMemberByIdOrInstanceIdWithItsOwnError()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        var staticId = MemberId("i", 1);
        await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, "", "i", Consumer, Protocols()));
        var (follower, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));
        await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, staticId, "i", Consumer, Protocols()));
        await follower;

        var written = await harness.SendAsync(LeaveGroupV3("g", ("wrong", "i"), ("", "i"), ("unknown", null), ("", null), (B, null)));
        var heartbeat = await harness.SendAsync(Heartbeat(3, "g", 2, B));

        CollectionAssert.AreEqual(LeaveV3Answer(0, ("wrong", "i", 82), ("", "i", 0), ("unknown", null, 25), ("", null, 25), (B, null, 0)), written);
        CollectionAssert.AreEqual(ErrorAnswer(3, 25), heartbeat);
    }

    [TestMethod]
    public async Task ServeAsync_V3UnknownGroup_AnswersEachMemberUnknownMemberId()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(LeaveGroupV3("g", ("", "i")));

        CollectionAssert.AreEqual(LeaveV3Answer(0, ("", "i", 25)), written);
    }

    [TestMethod]
    public async Task ServeAsync_LastMemberLeavingWhileAnotherIsPending_KeepsTheGroupsGeneration()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, "", null, Consumer, Protocols()));
        await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, A, null, Consumer, Protocols()));
        await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, "", null, Consumer, Protocols()));

        await harness.SendAsync(LeaveGroup(2, "g", A));
        var pending = await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, B, null, Consumer, Protocols()));

        CollectionAssert.AreEqual(JoinAnswer(4, 0, 2, "range", B, B, (B, null, "m-range")), pending);
    }

    [TestMethod]
    public async Task ServeAsync_DeniedGroup_IsGroupAuthorizationFailedWithNoMembers()
    {
        var harness = new KafkaGroupHarness(
            TestContext.CancellationToken,
            busConfiguration: new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("t", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe)] });

        var written = await harness.SendAsync(LeaveGroupV3("g", (A, null)));

        CollectionAssert.AreEqual(LeaveV3Answer(30), written);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRule_AnswersItsCodeAndRemovesNobody()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "LeaveGroup", Reply = "15" });

        var refused = await harness.SendAsync(LeaveGroup(0, "g", A));
        harness.Faults = EmptyFaultPlan.Instance;
        var heartbeat = await harness.SendAsync(Heartbeat(0, "g", 1, A));

        CollectionAssert.AreEqual(ErrorAnswer(0, 15), refused);
        CollectionAssert.AreEqual(ErrorAnswer(0, 0), heartbeat);
    }

    [TestMethod]
    public async Task ServeAsync_DropRule_ClosesWithNoBytes()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "LeaveGroup" });

        var written = await harness.SendAsync(LeaveGroup(0, "g", A));

        Assert.IsEmpty(written);
        Assert.Contains("LeaveGroup was dropped by fault rule f1; closed with no reply.", harness.Log.Notes);
    }
}
