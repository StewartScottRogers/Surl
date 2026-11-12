using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// JoinGroup v0 to v5 (BL-948; ADR-0145 decisions 3, 6, 9 and 10), every byte written by hand from
/// the Kafka protocol guide's JoinGroup schemas (<see cref="KafkaGroupHarness.JoinGroup"/> and
/// <see cref="KafkaGroupHarness.JoinAnswer"/>). The client id is <c>c</c>, so a dynamic member's id
/// is <c>c-</c> and the identifier bytes as a UUID.
/// </summary>
[TestClass]
public sealed class KafkaJoinGroupAnswererTests
{
    private static readonly string A = MemberId("c", 1);
    private static readonly string B = MemberId("c", 2);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var bus = new Surl.MessageBus.MessageBus(new ManualTimeProvider());
        var coordinator = new KafkaGroupCoordinator(new ManualTimeProvider());
        var answerer = new KafkaJoinGroupAnswerer(coordinator, bus);
        var request = new KafkaRequest(new KafkaRequestHeader(11, 0, 1, null), KafkaServedApis.Find(11)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaJoinGroupAnswerer(null!, bus));
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaJoinGroupAnswerer(coordinator, null!));
        Assert.AreEqual(KafkaApiKey.JoinGroup, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    [DataRow((short)0)]
    [DataRow((short)1)]
    [DataRow((short)2)]
    [DataRow((short)3)]
    public async Task ServeAsync_FirstJoinV0ToV3_IsJoinedAtOnceAsLeaderOfGenerationOne(short version)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(JoinGroup(version, "g", 10000, 10000, "", null, Consumer, Protocols()));

        CollectionAssert.AreEqual(JoinAnswer(version, 0, 1, "range", A, A, (A, null, "m-range")), written);
    }

    [TestMethod]
    public async Task ServeAsync_V4EmptyMemberId_IsMemberIdRequiredThenJoinedWithThatId()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var first = await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, "", null, Consumer, Protocols()));
        var second = await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, A, null, Consumer, Protocols()));

        CollectionAssert.AreEqual(JoinRefused(4, 79, A), first);
        CollectionAssert.AreEqual(JoinAnswer(4, 0, 1, "range", A, A, (A, null, "m-range")), second);
    }

    [TestMethod]
    [DataRow((short)3, (short)0)]
    [DataRow((short)4, (short)79)]
    public async Task ServeAsync_NullClientId_GivesAMemberIdStartingWithADash(short version, short error)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        var id = MemberId("", 1);

        var written = await harness.SendAsync(JoinGroup(version, "g", 10000, 10000, "", null, Consumer, Protocols(), hasClientId: false));

        CollectionAssert.AreEqual(error == 0 ? JoinAnswer(version, 0, 1, "range", id, id, (id, null, "m-range")) : JoinRefused(version, 79, id), written);
    }

    [TestMethod]
    public async Task ServeAsync_TwoMembers_AreAnsweredOneGenerationWithTheEarlierMemberLeaderListingBoth()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");

        var (follower, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));
        var leader = await harness.JoinV3Async(A);

        CollectionAssert.AreEqual(JoinAnswer(3, 0, 2, "range", A, A, (A, null, "m-range"), (B, null, "m-range")), leader);
        CollectionAssert.AreEqual(JoinAnswer(3, 0, 2, "range", A, B), await follower);
    }

    [TestMethod]
    public async Task ServeAsync_ProtocolsInDifferentOrders_ChooseTheLeadersFirstThatEveryMemberLists()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("", "sticky", "range", "roundrobin");

        var (follower, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols("roundrobin", "range")));
        var leader = await harness.JoinV3Async(A, "sticky", "range", "roundrobin");

        CollectionAssert.AreEqual(JoinAnswer(3, 0, 2, "range", A, A, (A, null, "m-range"), (B, null, "m-range")), leader);
        CollectionAssert.AreEqual(JoinAnswer(3, 0, 2, "range", A, B), await follower);
    }

    [TestMethod]
    public async Task ServeAsync_StaticRejoinWithAnEmptyMemberId_FencesTheOldId()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        var old = MemberId("i", 1);
        var renewed = MemberId("i", 2);

        var first = await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, "", "i", Consumer, Protocols()));
        var second = await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, "", "i", Consumer, Protocols()));
        var oldJoin = await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, old, "i", Consumer, Protocols()));
        var oldHeartbeat = await harness.SendAsync(Heartbeat(3, "g", 2, old, "i"));

        CollectionAssert.AreEqual(JoinAnswer(5, 0, 1, "range", old, old, (old, "i", "m-range")), first);
        CollectionAssert.AreEqual(JoinAnswer(5, 0, 2, "range", renewed, renewed, (renewed, "i", "m-range")), second);
        CollectionAssert.AreEqual(JoinRefused(5, 82, old), oldJoin);
        CollectionAssert.AreEqual(ErrorAnswer(3, 82), oldHeartbeat);
        Assert.AreEqual(new ConsumerGroupChangedEvent(1, "kafka", "g", ConsumerGroupChange.MemberFenced, old, 1, null), harness.GroupEvents.Single(change => change.Change == ConsumerGroupChange.MemberFenced));
    }

    [TestMethod]
    public async Task ServeAsync_StaticMemberFencedWhileItsJoinIsHeld_HasThatJoinAnsweredFencedInstanceId()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        var fenced = MemberId("i", 2);
        var renewed = MemberId("i", 3);
        await harness.JoinV3Async("");
        var (held, _) = await harness.SendHeldAsync(JoinGroup(5, "g", 10000, 10000, "", "i", Consumer, Protocols()));
        var (renewedJoin, _) = await harness.SendHeldAsync(JoinGroup(5, "g", 10000, 10000, "", "i", Consumer, Protocols()));

        var leader = await harness.JoinV3Async(A);

        CollectionAssert.AreEqual(JoinAnswer(3, 0, 2, "range", A, A, (A, null, "m-range"), (renewed, null, "m-range")), leader);
        CollectionAssert.AreEqual(JoinRefused(5, 82, fenced), await held);
        CollectionAssert.AreEqual(JoinAnswer(5, 0, 2, "range", A, renewed), await renewedJoin);
    }

    [TestMethod]
    public async Task ServeAsync_StaticMemberRejoiningWithItsOwnId_KeepsItsIdAndLeadership()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        var id = MemberId("i", 1);
        await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, "", "i", Consumer, Protocols()));

        var written = await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, id, "i", Consumer, Protocols()));

        CollectionAssert.AreEqual(JoinAnswer(5, 0, 2, "range", id, id, (id, "i", "m-range")), written);
    }

    [TestMethod]
    public async Task ServeAsync_EmptyGroupId_IsInvalidGroupIdBeforeAccess()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, busConfiguration: DenyingEveryGroup());

        var written = await harness.SendAsync(JoinGroup(3, "", 10000, 10000, "", null, Consumer, Protocols()));

        CollectionAssert.AreEqual(JoinRefused(3, 24, ""), written);
    }

    [TestMethod]
    public async Task ServeAsync_DeniedGroup_IsGroupAuthorizationFailedBeforeTheSessionTimeout()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, busConfiguration: DenyingEveryGroup());

        var written = await harness.SendAsync(JoinGroup(3, "g", 1, 10000, "", null, Consumer, Protocols()));

        CollectionAssert.AreEqual(JoinRefused(3, 30, ""), written);
    }

    [TestMethod]
    [DataRow(5999)]
    [DataRow(1800001)]
    public async Task ServeAsync_SessionTimeoutOutsideTheRange_IsInvalidSessionTimeoutBeforeTheProtocols(int sessionTimeoutMs)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(JoinGroup(3, "g", sessionTimeoutMs, 10000, "", null, "", []));

        CollectionAssert.AreEqual(JoinRefused(3, 26, ""), written);
    }

    [TestMethod]
    [DataRow("", true)]
    [DataRow(Consumer, false)]
    public async Task ServeAsync_EmptyProtocolTypeOrList_IsInconsistentGroupProtocolBeforeTheMemberId(string protocolType, bool hasProtocols)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(JoinGroup(3, "g", 10000, 10000, "unknown", null, protocolType, hasProtocols ? Protocols() : []));

        CollectionAssert.AreEqual(JoinRefused(3, 23, "unknown"), written);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ServeAsync_MemberIdTheGroupDoesNotHold_IsUnknownMemberId(bool groupExists)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        if (groupExists)
        {
            await harness.JoinV3Async("");
        }

        var written = await harness.JoinV3Async("unknown");

        CollectionAssert.AreEqual(JoinRefused(3, 25, "unknown"), written);
    }

    [TestMethod]
    public async Task ServeAsync_NewMemberOfAFullGroup_IsGroupMaxSizeReachedCountingPendingMembers()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, clock => new KafkaGroupCoordinator(clock, maxMembersPerGroup: 1));
        await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, "", null, Consumer, Protocols()));

        var written = await harness.SendAsync(JoinGroup(4, "g", 10000, 10000, "", null, Consumer, Protocols()));

        CollectionAssert.AreEqual(JoinRefused(4, 81, ""), written);
    }

    [TestMethod]
    public async Task ServeAsync_NewGroupPastTheMostGroups_IsPolicyViolation()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, clock => new KafkaGroupCoordinator(clock, maxGroups: 1));
        await harness.JoinV3Async("");

        var written = await harness.SendAsync(JoinGroup(3, "h", 10000, 10000, "", null, Consumer, Protocols()));
        var rejoin = await harness.JoinV3Async(A);

        CollectionAssert.AreEqual(JoinRefused(3, 44, ""), written);
        CollectionAssert.AreEqual(JoinAnswer(3, 0, 2, "range", A, A, (A, null, "m-range")), rejoin);
    }

    [TestMethod]
    public async Task ServeAsync_MetadataPastTheStateBound_IsPolicyViolation()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken, clock => new KafkaGroupCoordinator(clock, maxGroupStateBytes: 10));
        await harness.JoinV3Async("");

        var newGroup = await harness.SendAsync(JoinGroup(3, "h", 10000, 10000, "", null, Consumer, Protocols("range", "roundrobin")));
        var rejoin = await harness.JoinV3Async(A, "range", "roundrobin");

        CollectionAssert.AreEqual(JoinRefused(3, 44, ""), newGroup);
        CollectionAssert.AreEqual(JoinRefused(3, 44, A), rejoin);
    }

    [TestMethod]
    [DataRow("connect", "range")]
    [DataRow(Consumer, "roundrobin")]
    public async Task ServeAsync_ProtocolTypeOrListTheMembersDoNotShare_IsInconsistentGroupProtocol(string protocolType, string protocol)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");

        var written = await harness.SendAsync(JoinGroup(3, "g", 10000, 10000, "", null, protocolType, Protocols(protocol)));

        CollectionAssert.AreEqual(JoinRefused(3, 23, ""), written);
    }

    [TestMethod]
    public async Task ServeAsync_OnlyMemberRejoiningWithAnotherProtocolType_IsJoined()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");

        var written = await harness.SendAsync(JoinGroup(3, "g", 10000, 10000, A, null, "connect", Protocols("roundrobin")));

        CollectionAssert.AreEqual(JoinAnswer(3, 0, 2, "roundrobin", A, A, (A, null, "m-roundrobin")), written);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRule_AnswersItsCodeAndJoinsNobody()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "JoinGroup", Reply = "15" });

        var written = await harness.JoinV3Async("");

        CollectionAssert.AreEqual(JoinRefused(3, 15, ""), written);
        Assert.IsEmpty(harness.GroupEvents);
    }

    [TestMethod]
    public async Task ServeAsync_DropRule_ClosesWithNoBytes()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "JoinGroup" });

        var written = await harness.JoinV3Async("");

        Assert.IsEmpty(written);
        Assert.Contains("JoinGroup was dropped by fault rule f1; closed with no reply.", harness.Log.Notes);
    }

    private static MessageBusConfiguration DenyingEveryGroup() =>
        new() { AccessGrants = [new TopicAccessGrant("t", TopicAccessGrant.Everyone, TopicAccessRights.PublishAndSubscribe)] };
}
