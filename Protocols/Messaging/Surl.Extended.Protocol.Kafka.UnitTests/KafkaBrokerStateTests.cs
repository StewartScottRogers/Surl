using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// The Kafka broker's producer states and consumer groups under reset, snapshot and restore (BL-1190;
/// ADR-0159 decision 2).
/// </summary>
[TestClass]
public sealed class KafkaBrokerStateTests
{
    private static readonly string A = MemberId("c", 1);
    private static readonly string B = MemberId("c", 2);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaBrokerState(null!, new KafkaGroupCoordinator(new ManualTimeProvider())));
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaBrokerState(new KafkaProducerStates(), null!));
    }

    [TestMethod]
    public void Broker_WithoutAState_HoldsAnEmptyOneAndWithAStateHoldsThat()
    {
        var policy = new UnitTestKafkaAuthenticationPolicy(true, "PLAIN");
        var given = new KafkaBrokerState(new KafkaProducerStates(), new KafkaGroupCoordinator(new ManualTimeProvider()));

        Assert.IsNotNull(new KafkaBroker(policy, []).State);
        Assert.AreSame(given, new KafkaBroker(policy, [], given).State);
    }

    [TestMethod]
    public void Reset_AfterAProduce_EmptiesTheDuplicateWindowSoTheOldSequenceIsAppended()
    {
        var states = new KafkaProducerStates();
        var state = new KafkaBrokerState(states, new KafkaGroupCoordinator(new ManualTimeProvider()));
        KafkaProducedBatch first = new(1, 0, 0, 0);
        states.Record("t", [first], [0]);
        Assert.IsTrue(states.Check("t", [first]).IsDuplicate);

        state.Reset();

        Assert.AreEqual(0, states.Count);
        Assert.AreEqual(KafkaSequenceCheck.Append, states.Check("t", [first]));
    }

    [TestMethod]
    public async Task Reset_AMemberOfAGroup_IsAnsweredUnknownMemberIdByItsNextHeartbeat()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        CollectionAssert.AreEqual(ErrorAnswer(0, 0), await harness.SendAsync(Heartbeat(0, "g", 1, A)));

        new KafkaBrokerState(new KafkaProducerStates(), harness.Coordinator).Reset();

        CollectionAssert.AreEqual(ErrorAnswer(0, 25), await harness.SendAsync(Heartbeat(0, "g", 1, A)));
    }

    [TestMethod]
    public async Task Reset_AJoinGroupHeld_IsAnsweredUnknownMemberId()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        var (heldJoin, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));

        harness.Coordinator.Reset();

        CollectionAssert.AreEqual(JoinRefused(3, 25, B), await heldJoin);
    }

    [TestMethod]
    public async Task Reset_ASyncGroupHeld_IsAnsweredRebalanceInProgress()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        var (heldJoin, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));
        await harness.SendAsync(JoinGroup(3, "g", 10000, 10000, A, null, Consumer, Protocols()));
        await heldJoin;
        var (heldSync, _) = await harness.SendHeldAsync(SyncGroup(3, "g", 2, B, null));

        harness.Coordinator.Reset();

        CollectionAssert.AreEqual(SyncAnswer(3, 27), await heldSync);
    }

    [TestMethod]
    public async Task Reset_AMemberIdRequiredIdWaiting_IsUnknownWhenSentBack()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, "", null, Consumer, Protocols()));

        harness.Coordinator.Reset();
        var answer = await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, A, null, Consumer, Protocols()));

        CollectionAssert.AreEqual(JoinRefused(4, 25, A), answer);
    }

    [TestMethod]
    public void Reset_AnEmptyCoordinator_ChangesNothing()
    {
        var coordinator = new KafkaGroupCoordinator(new ManualTimeProvider());

        coordinator.Reset();

        Assert.AreEqual(0, coordinator.ListMemberedGroups().Count);
    }

    [TestMethod]
    public void TakeSnapshotChangeRestore_GivesBackTheWindowsAndTheProducerIdCounter()
    {
        var states = new KafkaProducerStates();
        var state = new KafkaBrokerState(states, new KafkaGroupCoordinator(new ManualTimeProvider()));
        var identifiers = new FixedBytesIdentifierRandom(0x01);
        var firstId = states.NextProducerId(identifiers);
        states.Record("t", [new(1, 0, 0, 0)], [7]);
        var snapshot = state.TakeSnapshot();
        states.Record("t", [new(1, 0, 1, 0)], [8]);
        states.Record("u", [new(2, 0, 0, 0)], [9]);
        states.NextProducerId(identifiers);

        state.Restore(snapshot);

        Assert.AreEqual(16, snapshot.ByteCount);
        Assert.AreEqual(1, states.Count);
        Assert.AreEqual(KafkaSequenceCheck.Duplicate(7), states.Check("t", [new(1, 0, 0, 0)]));
        Assert.AreEqual(KafkaSequenceCheck.Append, states.Check("t", [new(1, 0, 1, 0)]));
        Assert.AreEqual(firstId + 1, states.NextProducerId(identifiers));
    }

    [TestMethod]
    public async Task Restore_DropsTheGroupsSoAMemberIsAnsweredUnknownMemberId()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        var state = new KafkaBrokerState(new KafkaProducerStates(), harness.Coordinator);
        var snapshot = state.TakeSnapshot();
        await harness.JoinV3Async("");

        state.Restore(snapshot);

        CollectionAssert.AreEqual(ErrorAnswer(0, 25), await harness.SendAsync(Heartbeat(0, "g", 1, A)));
    }

    [TestMethod]
    public void Reset_ThenNextProducerId_DrawsTheStartAgainFromTheIdentifiers()
    {
        var states = new KafkaProducerStates();
        var identifiers = new FixedBytesIdentifierRandom(0x01);
        var first = states.NextProducerId(identifiers);
        states.NextProducerId(identifiers);

        states.Reset();

        Assert.AreEqual(first, states.NextProducerId(identifiers));
    }

    [TestMethod]
    public void Restore_ASnapshotFromAnotherBrokerOrNull_IsRefused()
    {
        var one = new KafkaBrokerState(new KafkaProducerStates(), new KafkaGroupCoordinator(new ManualTimeProvider()));
        var other = new KafkaBrokerState(new KafkaProducerStates(), new KafkaGroupCoordinator(new ManualTimeProvider()));

        Assert.ThrowsExactly<InvalidOperationException>(() => one.Restore(other.TakeSnapshot()));
        Assert.ThrowsExactly<ArgumentNullException>(() => one.Restore(null!));
        Assert.ThrowsExactly<InvalidOperationException>(() => new KafkaProducerStates().Restore(new KafkaProducerStates().TakeSnapshot()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaProducerStates().Restore(null!));
    }
}
