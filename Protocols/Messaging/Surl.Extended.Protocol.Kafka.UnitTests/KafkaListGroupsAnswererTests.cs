using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// ListGroups v0 to v2 (BL-949; ADR-0145 decisions 5, 6 and 10), every byte written by hand from the
/// Kafka protocol guide's ListGroups schemas: an empty request, answered <c>throttle_time_ms (v1+)
/// error_code groups [group_id protocol_type]</c>.
/// </summary>
[TestClass]
public sealed class KafkaListGroupsAnswererTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var bus = new Surl.MessageBus.MessageBus(new ManualTimeProvider());
        var coordinator = new KafkaGroupCoordinator(new ManualTimeProvider());
        var answerer = new KafkaListGroupsAnswerer(coordinator, bus);
        var request = new KafkaRequest(new KafkaRequestHeader(16, 0, 1, null), KafkaServedApis.Find(16)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaListGroupsAnswerer(null!, bus));
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaListGroupsAnswerer(coordinator, null!));
        Assert.AreEqual(KafkaApiKey.ListGroups, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    [DataRow((short)0)]
    [DataRow((short)1)]
    [DataRow((short)2)]
    public async Task ServeAsync_GroupsWithMembersOrOffsets_AreListedInOrdinalOrderWithTheirProtocolTypes(short version)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        await harness.JoinV3Async("");
        harness.Bus.CommitOffset(null, "g", "t", 1, -1, null);
        harness.Bus.CommitOffset(null, "b", "t", 1, -1, null);
        harness.Bus.CommitOffset(null, "B", "t", 1, -1, null);

        var written = await harness.SendAsync(ListGroups(version));

        CollectionAssert.AreEqual(ListGroupsAnswer(version, 0, ("B", ""), ("b", ""), ("g", "consumer")), written);
    }

    [TestMethod]
    public async Task ServeAsync_NoGroups_AnswersAnEmptyArray()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(ListGroups(2));

        CollectionAssert.AreEqual(ListGroupsAnswer(2, 0), written);
    }

    [TestMethod]
    public async Task ServeAsync_DeniedGroup_IsLeftOut()
    {
        var harness = new KafkaGroupHarness(
            TestContext.CancellationToken,
            busConfiguration: new MessageBusConfiguration
            {
                AccessGrants =
                [
                    new TopicAccessGrant("t", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                    new TopicAccessGrant("$group/open", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
                    new TopicAccessGrant("$group/hidden", "alice", TopicAccessRights.Subscribe),
                ],
            });
        harness.Bus.CommitOffset(null, "open", "t", 1, -1, null);
        harness.Bus.CommitOffset("alice", "hidden", "t", 1, -1, null);

        var written = await harness.SendAsync(ListGroups(1));

        CollectionAssert.AreEqual(ListGroupsAnswer(1, 0, ("open", "")), written);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRule_AnswersItsCodeWithNoGroups()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Bus.CommitOffset(null, "g", "t", 1, -1, null);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "ListGroups", Reply = "15" });

        var written = await harness.SendAsync(ListGroups(1));

        CollectionAssert.AreEqual(ListGroupsAnswer(1, 15), written);
    }

    [TestMethod]
    public async Task ServeAsync_DropRule_ClosesWithNoBytes()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "ListGroups" });

        var written = await harness.SendAsync(ListGroups(0));

        Assert.IsEmpty(written);
        Assert.Contains("ListGroups was dropped by fault rule f1; closed with no reply.", harness.Log.Notes);
    }
}
