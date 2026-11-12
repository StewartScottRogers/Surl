using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaGroupHarness;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// FindCoordinator v0 to v2 (BL-948; ADR-0145 decisions 2, 6 and 10), every byte written by hand
/// from the Kafka protocol guide's FindCoordinator schemas: a request is <c>key key_type</c> (v1+);
/// a response is <c>throttle_time_ms</c> (v1+) <c>error_code error_message</c> (v1+) <c>node_id host port</c>.
/// </summary>
[TestClass]
public sealed class KafkaFindCoordinatorAnswererTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var answerer = new KafkaFindCoordinatorAnswerer(new Surl.MessageBus.MessageBus(new ManualTimeProvider()));
        var request = new KafkaRequest(new KafkaRequestHeader(10, 0, 1, null), KafkaServedApis.Find(10)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaFindCoordinatorAnswerer(null!));
        Assert.AreEqual(KafkaApiKey.FindCoordinator, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    public async Task ServeAsync_GroupKeyV0_AnswersNodeZeroAtTheAdvertisedHostAndPort()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(FindCoordinator(0, "g"));

        CollectionAssert.AreEqual(Response([.. Int16(0), .. Int32(0), .. String("127.0.0.1"), .. Int32(9092)]), written);
    }

    [TestMethod]
    [DataRow((short)1)]
    [DataRow((short)2)]
    public async Task ServeAsync_GroupKeyTypeZero_AnswersThrottleNullMessageAndNodeZero(short version)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(FindCoordinator(version, "g", 0));

        CollectionAssert.AreEqual(Response([.. Int32(0), .. Int16(0), 0xFF, 0xFF, .. Int32(0), .. String("127.0.0.1"), .. Int32(9092)]), written);
    }

    [TestMethod]
    public async Task ServeAsync_ListenerWithNoBoundPort_AnswersTheListenUrlsPort()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(FindCoordinator(0, "g"), null, new ListenUrl("kafka", "127.0.0.1", 9093));

        CollectionAssert.AreEqual(Response([.. Int16(0), .. Int32(0), .. String("127.0.0.1"), .. Int32(9093)]), written);
    }

    [TestMethod]
    [DataRow((sbyte)1)]
    [DataRow((sbyte)2)]
    public async Task ServeAsync_KeyTypeOtherThanGroup_IsInvalidRequestWithNodeMinusOne(sbyte keyType)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);

        var written = await harness.SendAsync(FindCoordinator(1, "tx", keyType));

        CollectionAssert.AreEqual(Refused(42), written);
    }

    [TestMethod]
    public async Task ServeAsync_DeniedGroup_IsGroupAuthorizationFailed()
    {
        var harness = new KafkaGroupHarness(
            TestContext.CancellationToken,
            busConfiguration: new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("$group/open", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe)] });

        var denied = await harness.SendAsync(FindCoordinator(1, "g"));
        var granted = await harness.SendAsync(FindCoordinator(1, "open"));

        CollectionAssert.AreEqual(Refused(30), denied);
        CollectionAssert.AreEqual(Response([.. Int32(0), .. Int16(0), 0xFF, 0xFF, .. Int32(0), .. String("127.0.0.1"), .. Int32(9092)]), granted);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRule_AnswersItsCodeWithNodeMinusOne()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "FindCoordinator", Reply = "15" });
        harness.Faults = faults;

        var written = await harness.SendAsync(FindCoordinator(1, "g"));

        CollectionAssert.AreEqual(Refused(15), written);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "g"), harness.Events.Events.OfType<FaultAppliedEvent>().Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "kafka://127.0.0.1:9092/", Scheme = "kafka", Path = "g", Command = "FindCoordinator" },
            faults.Points.Single());
        Assert.Contains("FindCoordinator of g answered error 15 by fault rule f1.", harness.Log.Notes);
    }

    [TestMethod]
    [DataRow("busy")]
    [DataRow("0")]
    [DataRow("128")]
    public async Task ServeAsync_ErrorRuleWhoseReplyIsNotACodeOneTo127_IsNotAppliedAndNoted(string reply)
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = reply });

        var written = await harness.SendAsync(FindCoordinator(0, "g"));

        CollectionAssert.AreEqual(Response([.. Int16(0), .. Int32(0), .. String("127.0.0.1"), .. Int32(9092)]), written);
        Assert.Contains("Fault rule f1 not applied to FindCoordinator of g: its reply is not a Kafka error code 1 to 127.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_RuleOfAnotherKind_IsAnsweredAsUsual()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));

        var written = await harness.SendAsync(FindCoordinator(0, "g"));

        CollectionAssert.AreEqual(Response([.. Int16(0), .. Int32(0), .. String("127.0.0.1"), .. Int32(9092)]), written);
    }

    [TestMethod]
    public async Task ServeAsync_DropRule_ClosesWithNoBytes()
    {
        var harness = new KafkaGroupHarness(TestContext.CancellationToken);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "FindCoordinator" });

        var written = await harness.SendAsync(FindCoordinator(0, "g"));

        Assert.IsEmpty(written);
        Assert.Contains("FindCoordinator was dropped by fault rule f1; closed with no reply.", harness.Log.Notes);
    }

    // throttle_time_ms 0, error_code, error_message null, node_id -1, host "", port -1.
    private static byte[] Refused(short error) => Response([.. Int32(0), .. Int16(error), 0xFF, 0xFF, .. Int32(-1), .. String(""), .. Int32(-1)]);
}
