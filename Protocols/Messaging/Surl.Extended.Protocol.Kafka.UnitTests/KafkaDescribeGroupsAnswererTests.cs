using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// DescribeGroups v0 to v4 (BL-949; ADR-0145 decisions 5, 6 and 10), every byte written by hand from the
/// Kafka protocol guide's DescribeGroups schemas: <c>groups include_authorized_operations (v3+)</c>,
/// answered <c>throttle_time_ms (v1+) groups [error_code group_id group_state protocol_type
/// protocol_data members [member_id group_instance_id (v4) client_id client_host member_metadata
/// member_assignment] authorized_operations (v3+)]</c>.
/// </summary>
[TestClass]
public sealed class KafkaDescribeGroupsAnswererTests
{
    private static readonly string A = MemberId("c", 1);
    private static readonly string B = MemberId("c", 2);

    public TestContext TestContext { get; set; } = null!;

    private static async Task<KafkaGroupHarness> StableGroupAsync(CancellationToken cancellationToken)
    {
        var harness = new KafkaGroupHarness(cancellationToken);
        await harness.JoinV3Async("");
        await harness.SendAsync(SyncGroup(3, "g", 1, A, null, (A, "assigned")));
        return harness;
    }

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var bus = new Surl.MessageBus.MessageBus(new ManualTimeProvider());
        var coordinator = new KafkaGroupCoordinator(new ManualTimeProvider());
        var answerer = new KafkaDescribeGroupsAnswerer(coordinator, bus);
        var request = new KafkaRequest(new KafkaRequestHeader(15, 0, 1, null), KafkaServedApis.Find(15)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaDescribeGroupsAnswerer(null!, bus));
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaDescribeGroupsAnswerer(coordinator, null!));
        Assert.AreEqual(KafkaApiKey.DescribeGroups, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    [DataRow((short)0)]
    [DataRow((short)1)]
    [DataRow((short)2)]
    [DataRow((short)3)]
    [DataRow((short)4)]
    public async Task ServeAsync_StableGroup_IsDescribedWithItsMembersMetadataAndAssignments(short version)
    {
        var harness = await StableGroupAsync(TestContext.CancellationToken);

        var written = await harness.SendAsync(DescribeGroups(version, false, "g"));

        CollectionAssert.AreEqual(
            DescribeGroupsAnswer(
                version,
                int.MinValue,
                new DescribedGroup(0, "g", "Stable", "consumer", "range", [new Described(A, null, "c", "/127.0.0.1", "m-range", "assigned")])),
            written);
    }

    [TestMethod]
    public async Task ServeAsync_AskedForAuthorizedOperations_AnswersReadAndDescribe()
    {
        var harness = await StableGroupAsync(TestContext.CancellationToken);

        var written = await harness.SendAsync(DescribeGroups(4, true, "g"));

        CollectionAssert.AreEqual(
            DescribeGroupsAnswer(
                4,
                264,
                new DescribedGroup(0, "g", "Stable", "consumer", "range", [new Described(A, null, "c", "/127.0.0.1", "m-range", "assigned")])),
            written);
    }

    [TestMethod]
    public async Task ServeAsync_GroupCompletingARebalance_HasTheChosenProtocolAndNoAssignments()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");

        var written = await harness.SendAsync(DescribeGroups(0, false, "g"));

        CollectionAssert.AreEqual(
            DescribeGroupsAnswer(0, 0, new DescribedGroup(0, "g", "CompletingRebalance", "consumer", "range", [new Described(A, null, "c", "/127.0.0.1", "m-range", "")])),
            written);
    }

    [TestMethod]
    public async Task ServeAsync_GroupPreparingARebalance_HasNoProtocolMetadataOrAssignmentsAndListsMembersInJoinOrder()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        await harness.SendAsync(SyncGroup(3, "g", 1, A, null, (A, "assigned")));
        var (follower, _) = await harness.SendHeldAsync(JoinGroup(3, "g", 10000, 10000, "", null, Consumer, Protocols()));

        var written = await harness.SendAsync(DescribeGroups(0, false, "g"));
        await harness.JoinV3Async(A);
        await follower;

        CollectionAssert.AreEqual(
            DescribeGroupsAnswer(
                0,
                0,
                new DescribedGroup(
                    0,
                    "g",
                    "PreparingRebalance",
                    "consumer",
                    "",
                    [new Described(A, null, "c", "/127.0.0.1", "", ""), new Described(B, null, "c", "/127.0.0.1", "", "")])),
            written);
    }

    [TestMethod]
    public async Task ServeAsync_StaticMemberAndAJoinWithNoClientId_AreDescribedWithTheInstanceIdAndAnEmptyClientId()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        var staticId = MemberId("i", 1);
        await harness.SendAsync(JoinGroup(5, "g", 10000, 10000, "", "i", Consumer, Protocols(), hasClientId: false));

        var written = await harness.SendAsync(DescribeGroups(4, false, "g"));

        CollectionAssert.AreEqual(
            DescribeGroupsAnswer(
                4,
                int.MinValue,
                new DescribedGroup(0, "g", "CompletingRebalance", "consumer", "range", [new Described(staticId, "i", "", "/127.0.0.1", "m-range", "")])),
            written);
    }

    [TestMethod]
    public async Task ServeAsync_GroupsAsked_AreAnsweredInRequestOrderDeadWhenUnknownAndEmptyWhenKnownOnlyByItsOffsets()
    {
        var harness = await StableGroupAsync(TestContext.CancellationToken);
        harness.Bus.CommitOffset(null, "offsets", "t", 1, -1, null);

        var written = await harness.SendAsync(DescribeGroups(3, false, "nobody", "g", "offsets"));

        CollectionAssert.AreEqual(
            DescribeGroupsAnswer(
                3,
                int.MinValue,
                new DescribedGroup(0, "nobody", "Dead", "", "", []),
                new DescribedGroup(0, "g", "Stable", "consumer", "range", [new Described(A, null, "c", "/127.0.0.1", "m-range", "assigned")]),
                new DescribedGroup(0, "offsets", "Empty", "", "", [])),
            written);
    }

    [TestMethod]
    public async Task ServeAsync_NoGroups_AnswersAnEmptyArray()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(DescribeGroups(2, false));

        CollectionAssert.AreEqual(DescribeGroupsAnswer(2, 0), written);
    }

    [TestMethod]
    public async Task ServeAsync_DeniedGroup_IsGroupAuthorizationFailedWithEveryOtherFieldEmptyWhetherOrNotItExists()
    {
        var harness = new KafkaGroupHarness(
            TestContext.CancellationToken,
            busConfiguration: new MessageBusConfiguration
            {
                AccessGrants =
                [
                    new TopicAccessGrant("t", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                    new TopicAccessGrant("$group/open", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                ],
            });
        harness.Bus.CommitOffset(null, "open", "t", 1, -1, null);

        var written = await harness.SendAsync(DescribeGroups(3, true, "closed", "g", "open"));

        CollectionAssert.AreEqual(
            DescribeGroupsAnswer(
                3,
                264,
                new DescribedGroup(30, "closed", "", "", "", []),
                new DescribedGroup(30, "g", "", "", "", []),
                new DescribedGroup(0, "open", "Empty", "", "", [])),
            written);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRule_AnswersItsCodeForEveryGroupAsked()
    {
        var harness = await StableGroupAsync(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "DescribeGroups", Path = "g", Reply = "15" });

        var written = await harness.SendAsync(DescribeGroups(0, false, "g", "nobody"));

        CollectionAssert.AreEqual(
            DescribeGroupsAnswer(0, 0, new DescribedGroup(15, "g", "", "", "", []), new DescribedGroup(0, "nobody", "Dead", "", "", [])),
            written);
    }

    [TestMethod]
    public async Task ServeAsync_DropRule_ClosesWithNoBytes()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "DescribeGroups" });

        var written = await harness.SendAsync(DescribeGroups(0, false, "g"));

        Assert.IsEmpty(written);
        Assert.Contains("DescribeGroups was dropped by fault rule f1; closed with no reply.", harness.Log.Notes);
    }

    [TestMethod]
    public void KafkaGroupCallerOf_AnEndPointThatIsNotAnIpAddress_NamesItWithASlash()
    {
        var request = new KafkaRequest(new KafkaRequestHeader(15, 0, 1, null), KafkaServedApis.Find(15)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));
        var context = Context(TestContext.CancellationToken) with { RemoteEndPoint = new System.Net.DnsEndPoint("peer.example", 9092) };

        var caller = KafkaGroupCaller.Of(request, context);

        Assert.AreEqual("/" + new System.Net.DnsEndPoint("peer.example", 9092), caller.ClientHost);
    }
}
