using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// The group coordinator's sessions, join phases, bounds and events (BL-948; ADR-0145 decisions 3,
/// 9 and 10), driven through the group APIs on a fake clock: every session timeout and rebalance
/// timeout here is 10000 ms.
/// </summary>
[TestClass]
public sealed class KafkaGroupCoordinatorTests
{
    private static readonly string A = MemberId("c", 1);
    private static readonly string B = MemberId("c", 2);
    private static readonly string C = MemberId("c", 3);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_DefaultsAndInvalidArguments()
    {
        var coordinator = new KafkaGroupCoordinator(new ManualTimeProvider());

        Assert.AreEqual(1000, coordinator.MaxGroups);
        Assert.AreEqual(1000, coordinator.MaxMembersPerGroup);
        Assert.AreEqual(268435456, coordinator.MaxGroupStateBytes);
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaGroupCoordinator(null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new KafkaGroupCoordinator(new ManualTimeProvider(), maxGroups: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new KafkaGroupCoordinator(new ManualTimeProvider(), maxMembersPerGroup: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new KafkaGroupCoordinator(new ManualTimeProvider(), maxGroupStateBytes: 0));
    }

    [TestMethod]
    public async Task SessionTimeout_PassedWithNoRequest_RemovesTheMemberAndRebalancesTheGroup()
    {
        var clock = new ManualTimeProvider();
        var harness = await TwoMembersOfGenerationTwoAsync(clock);
        clock.Advance(TimeSpan.FromSeconds(6));
        await harness.SendAsync(Heartbeat(1, "g", 2, A));

        clock.Advance(TimeSpan.FromSeconds(4));
        var heartbeat = await harness.SendAsync(Heartbeat(1, "g", 2, A));
        var rejoined = await harness.JoinV3Async(A);

        Assert.AreEqual(new ConsumerGroupChangedEvent(null, "kafka", "g", ConsumerGroupChange.MemberRemoved, B, 2, null), harness.GroupEvents.Single(change => change.Change == ConsumerGroupChange.MemberRemoved));
        CollectionAssert.AreEqual(ErrorAnswer(1, 27), heartbeat);
        CollectionAssert.AreEqual(JoinAnswer(3, 0, 3, "range", A, A, (A, null, "m-range")), rejoined);
    }

    [TestMethod]
    public async Task RebalanceTimeout_PassedBeforeAMemberRejoins_RemovesItAndEndsTheJoinPhase()
    {
        var clock = new ManualTimeProvider();
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, clock: clock);
        await harness.JoinV3Async("");
        var (joiner, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));

        clock.Advance(TimeSpan.FromSeconds(10));

        CollectionAssert.AreEqual(JoinAnswer(3, 0, 2, "range", B, B, (B, null, "m-range")), await joiner);
        CollectionAssert.AreEqual(
            new[]
            {
                new ConsumerGroupChangedEvent(null, "kafka", "g", ConsumerGroupChange.MemberRemoved, A, 1, null),
                new ConsumerGroupChangedEvent(null, "kafka", "g", ConsumerGroupChange.GenerationStarted, null, 2, null),
            },
            harness.GroupEvents.Skip(3).ToArray());
    }

    [TestMethod]
    public async Task SessionTimeout_EveryMemberAtOnce_ForgetsTheGroup()
    {
        var clock = new ManualTimeProvider();
        var harness = await TwoMembersOfGenerationTwoAsync(clock);

        clock.Advance(TimeSpan.FromSeconds(10));
        var heartbeat = await harness.SendAsync(Heartbeat(1, "g", 2, A));
        var joined = await harness.JoinV3Async("");

        CollectionAssert.AreEqual(ErrorAnswer(1, 25), heartbeat);
        CollectionAssert.AreEqual(JoinAnswer(3, 0, 1, "range", C, C, (C, null, "m-range")), joined);
    }

    [TestMethod]
    public async Task PendingMemberId_NotSentBackWithinTheSessionTimeout_IsForgotten()
    {
        var clock = new ManualTimeProvider();
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, clock: clock);
        await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, "", null, Consumer, Protocols()));

        clock.Advance(TimeSpan.FromSeconds(10));
        var written = await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, A, null, Consumer, Protocols()));

        CollectionAssert.AreEqual(JoinRefused(4, 25, A), written);
    }

    [TestMethod]
    public async Task PhaseTimer_FiringLateAfterThePhaseEnded_ChangesNothing()
    {
        var clock = new CallbackRecordingTimeProvider();
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, clock: clock);
        await harness.JoinV3Async("");
        var eventCount = harness.GroupEvents.Count;

        clock.Timers[0].Callback(clock.Timers[0].State);
        var heartbeat = await harness.SendAsync(Heartbeat(1, "g", 1, A));

        Assert.HasCount(eventCount, harness.GroupEvents);
        CollectionAssert.AreEqual(ErrorAnswer(1, 0), heartbeat);
    }

    [TestMethod]
    public async Task JoinAndSync_OfOneMember_PublishJoinedGenerationStartedAndAssignmentDistributed()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        await harness.JoinV3Async("");
        await harness.SendAsync(SyncGroup(1, "g", 1, A, null, (A, "a")));

        CollectionAssert.AreEqual(
            new[]
            {
                new ConsumerGroupChangedEvent(1, "kafka", "g", ConsumerGroupChange.MemberJoined, A, 0, null),
                new ConsumerGroupChangedEvent(1, "kafka", "g", ConsumerGroupChange.GenerationStarted, null, 1, null),
                new ConsumerGroupChangedEvent(1, "kafka", "g", ConsumerGroupChange.AssignmentDistributed, null, 1, null),
            },
            harness.GroupEvents.ToArray());
    }

    // A leads B in generation 2, both sessions started now.
    private async Task<KafkaGroupHarness> TwoMembersOfGenerationTwoAsync(TimeProvider clock)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, clock: clock);
        await harness.JoinV3Async("");
        var (follower, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));
        await harness.JoinV3Async(A);
        await follower;
        return harness;
    }
}
