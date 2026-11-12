using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// Heartbeat v0 to v3 (BL-948; ADR-0145 decisions 3, 6 and 10), every byte written by hand from the
/// Kafka protocol guide's Heartbeat schemas: a request is <c>group_id generation_id member_id
/// group_instance_id</c> (v3); a response is <c>throttle_time_ms</c> (v1+) <c>error_code</c>.
/// </summary>
[TestClass]
public sealed class KafkaHeartbeatAnswererTests
{
    private static readonly string A = MemberId("c", 1);
    private static readonly string B = MemberId("c", 2);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var bus = new Surl.MessageBus.MessageBus(new ManualTimeProvider());
        var coordinator = new KafkaGroupCoordinator(new ManualTimeProvider());
        var answerer = new KafkaHeartbeatAnswerer(coordinator, bus);
        var request = new KafkaRequest(new KafkaRequestHeader(12, 0, 1, null), KafkaServedApis.Find(12)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaHeartbeatAnswerer(null!, bus));
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaHeartbeatAnswerer(coordinator, null!));
        Assert.AreEqual(KafkaApiKey.Heartbeat, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    public async Task ServeAsync_CompletingRebalanceAndStable_AreAnsweredZero()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");

        var completing = await harness.SendAsync(Heartbeat(0, "g", 1, A));
        await harness.SendAsync(SyncGroup(0, "g", 1, A, null, (A, "a")));
        var stable = await harness.SendAsync(Heartbeat(2, "g", 1, A));

        CollectionAssert.AreEqual(ErrorAnswer(0, 0), completing);
        CollectionAssert.AreEqual(ErrorAnswer(2, 0), stable);
    }

    [TestMethod]
    public async Task ServeAsync_GroupPreparingARebalance_IsRebalanceInProgressEvenForTheHeldJoiner()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));

        var absent = await harness.SendAsync(Heartbeat(1, "g", 1, A));
        var held = await harness.SendAsync(Heartbeat(1, "g", 1, B));

        CollectionAssert.AreEqual(ErrorAnswer(1, 27), absent);
        CollectionAssert.AreEqual(ErrorAnswer(1, 27), held);
    }

    [TestMethod]
    [DataRow("other", "member", 1, (short)25)]
    [DataRow("g", "unknown", 1, (short)25)]
    [DataRow("g", "member", 0, (short)22)]
    public async Task ServeAsync_UnknownMemberOrAnotherGeneration_IsRefused(string group, string member, int generationId, short error)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");

        var written = await harness.SendAsync(Heartbeat(1, group, generationId, member == "member" ? A : member));

        CollectionAssert.AreEqual(ErrorAnswer(1, error), written);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("unheld")]
    public async Task ServeAsync_V3DynamicMemberWithAnInstanceIdNobodyHolds_IsAnsweredZero(string? instanceId)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");

        var written = await harness.SendAsync(Heartbeat(3, "g", 1, A, instanceId));

        CollectionAssert.AreEqual(ErrorAnswer(3, 0), written);
    }

    [TestMethod]
    public async Task ServeAsync_V3StaticMemberWithItsOwnInstance_IsAnsweredZero()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, "", "i", Consumer, Protocols()));

        var written = await harness.SendAsync(Heartbeat(3, "g", 1, MemberId("i", 1), "i"));

        CollectionAssert.AreEqual(ErrorAnswer(3, 0), written);
    }

    [TestMethod]
    public async Task ServeAsync_DeniedGroup_IsGroupAuthorizationFailed()
    {
        var harness = new KafkaGroupHarness(
            TestContext.CancellationToken,
            busConfiguration: new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("t", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe)] });

        var written = await harness.SendAsync(Heartbeat(1, "g", 1, A));

        CollectionAssert.AreEqual(ErrorAnswer(1, 30), written);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRule_AnswersItsCodeAndRefreshesNoSession()
    {
        var clock = new ManualTimeProvider();
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, clock: clock);
        await harness.JoinV3Async("");
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "Heartbeat", Reply = "15" });
        harness.Faults = faults;
        clock.Advance(TimeSpan.FromSeconds(9));

        var refused = await harness.SendAsync(Heartbeat(3, "g", 1, A));
        harness.Faults = EmptyFaultPlan.Instance;
        clock.Advance(TimeSpan.FromSeconds(1));
        var after = await harness.SendAsync(Heartbeat(3, "g", 1, A));

        CollectionAssert.AreEqual(ErrorAnswer(3, 15), refused);
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "kafka://127.0.0.1:9092/", Scheme = "kafka", Path = "g", Command = "Heartbeat" },
            faults.Points.Single());
        CollectionAssert.AreEqual(ErrorAnswer(3, 25), after);
        Assert.AreEqual(ConsumerGroupChange.MemberRemoved, harness.GroupEvents[^1].Change);
    }

    [TestMethod]
    public async Task ServeAsync_DropRule_ClosesWithNoBytes()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "Heartbeat" });

        var written = await harness.SendAsync(Heartbeat(1, "g", 1, A));

        Assert.IsEmpty(written);
        Assert.Contains("Heartbeat was dropped by fault rule f1; closed with no reply.", harness.Log.Notes);
    }
}
