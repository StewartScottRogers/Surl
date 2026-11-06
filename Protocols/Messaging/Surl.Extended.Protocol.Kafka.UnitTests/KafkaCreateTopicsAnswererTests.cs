using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// CreateTopics v2 to v4 over the shared bus (BL-950; ADR-0145 decisions 7 and 10), every byte
/// written by hand from the Kafka protocol guide's CreateTopics schemas: a request is
/// <c>[topics] timeout_ms validate_only</c>, each topic <c>name num_partitions replication_factor
/// [assignments] [configs]</c>; a response is <c>throttle_time_ms [topics]</c>, each topic
/// <c>name error_code error_message</c>.
/// </summary>
[TestClass]
public sealed class KafkaCreateTopicsAnswererTests
{
    private static readonly byte[] NoArray = [0, 0, 0, 0];

    // brokers: one, node 0, host "127.0.0.1", port 9092, rack null; controller_id 0.
    private static readonly byte[] MetadataOneCluster = [0, 0, 0, 1, 0, 0, 0, 0, .. String("127.0.0.1"), 0, 0, 0x23, 0x84, 0xFF, 0xFF, 0, 0, 0, 0];

    // One partition: error 0, partition 0, leader 0, replicas [0], isr [0].
    private static readonly byte[] OnePartition = [0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0];

    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullBus_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaCreateTopicsAnswerer(null!));

    [TestMethod]
    public void ApiKey_IsCreateTopics() =>
        Assert.AreEqual(KafkaApiKey.CreateTopics, new KafkaCreateTopicsAnswerer(new Surl.MessageBus.MessageBus(clock)).ApiKey);

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var answerer = new KafkaCreateTopicsAnswerer(new Surl.MessageBus.MessageBus(clock));
        var request = new KafkaRequest(new KafkaRequestHeader(19, 2, 1, null), KafkaServedApis.Find(19)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    [DataRow((short)2)]
    [DataRow((short)3)]
    public async Task ServeAsync_NewTopic_IsCreatedWithAnEmptyLogAndListedByMetadata(short version)
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, _) = await ServeAsync(bus, Request(KafkaApiKey.CreateTopics, version, CreateTopics(Topic("orders"))), Request(KafkaApiKey.Metadata, 1, [0xFF, 0xFF, 0xFF, 0xFF]));

        byte[] metadata = [.. MetadataOneCluster, 0, 0, 0, 1, 0, 0, .. String("orders"), 0x00, .. OnePartition];
        CollectionAssert.AreEqual(new[] { Response(Answer(("orders", 0))), Response(metadata) }.SelectMany(bytes => bytes).ToArray(), written);
        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 0, 0), bus.GetLogOffsets("orders"));
    }

    [TestMethod]
    public async Task ServeAsync_VersionFourWithMinusOnePartitionsAndReplicationFactor_CreatesTheTopic()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, _) = await ServeAsync(bus, Request(KafkaApiKey.CreateTopics, 4, CreateTopics(Topic("orders", partitions: -1, replicationFactor: -1))));

        CollectionAssert.AreEqual(Response(Answer(("orders", 0))), written);
        CollectionAssert.AreEqual(new[] { "orders" }, bus.GetLogTopics().ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_EachInvalidTopic_IsAnsweredItsCodeWhileTheOthersAreCreated()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        bus.CreateLog("taken");
        byte[] fields = CreateTopics(
            Topic("twice"),
            Topic("no/slash"),
            Topic("taken"),
            Topic("p0", partitions: 0),
            Topic("p2", partitions: 2),
            Topic("r0", replicationFactor: 0),
            Topic("r2", replicationFactor: 2),
            Topic("onone", assignments: Assignments((1, [0]))),
            Topic("nodeone", assignments: Assignments((0, [1]))),
            Topic("nodetwice", assignments: Assignments((0, [0, 0]))),
            Topic("twoparts", assignments: Assignments((0, [0]), (1, [0]))),
            Topic("assigned", partitions: -1, replicationFactor: -1, assignments: Assignments((0, [0]))),
            Topic("twice"),
            Topic("created"));

        var (written, _) = await ServeAsync(bus, Request(KafkaApiKey.CreateTopics, 3, fields));

        var expected = Answer(
            ("twice", 42), ("no/slash", 17), ("taken", 36), ("p0", 37), ("p2", 37), ("r0", 38), ("r2", 38),
            ("onone", 39), ("nodeone", 39), ("nodetwice", 39), ("twoparts", 39), ("assigned", 0), ("twice", 42), ("created", 0));
        CollectionAssert.AreEqual(Response(expected), written);
        CollectionAssert.AreEqual(new[] { "assigned", "created", "taken" }, bus.GetLogTopics().Order(StringComparer.Ordinal).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_TopicTheCallerMayNotPublishTo_IsTopicAuthorizationFailed()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, configuration: new MessageBusConfiguration
        {
            AccessGrants =
            [
                new TopicAccessGrant("open", TopicAccessGrant.Everyone, TopicAccessRights.PublishAndSubscribe),
                new TopicAccessGrant("readonly", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
            ],
        });

        var (written, _) = await ServeAsync(bus, Request(KafkaApiKey.CreateTopics, 2, CreateTopics(Topic("readonly"), Topic("secret"), Topic("open"))));

        CollectionAssert.AreEqual(Response(Answer(("readonly", 29), ("secret", 29), ("open", 0))), written);
        CollectionAssert.AreEqual(new[] { "open" }, bus.GetLogTopics().ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_TopicPastTheBussMostTopics_IsPolicyViolation()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, maxTopics: 2);
        bus.CreateLog("full");

        var (written, _) = await ServeAsync(bus, Request(KafkaApiKey.CreateTopics, 2, CreateTopics(Topic("one"), Topic("two"))));

        CollectionAssert.AreEqual(Response(Answer(("one", 0), ("two", 44))), written);
        CollectionAssert.AreEqual(new[] { "full", "one" }, bus.GetLogTopics().Order(StringComparer.Ordinal).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ValidateOnly_AnswersZeroAndCreatesNothing()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, maxTopics: 1);
        bus.CreateLog("full");

        var (written, _) = await ServeAsync(bus, Request(KafkaApiKey.CreateTopics, 2, CreateTopics(validateOnly: true, Topic("new"), Topic("p2", partitions: 2), Topic("full"))));

        CollectionAssert.AreEqual(Response(Answer(("new", 0), ("p2", 37), ("full", 36))), written);
        CollectionAssert.AreEqual(new[] { "full" }, bus.GetLogTopics().ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_TopicWithConfigs_IsCreatedAndTheIgnoredNamesNoted()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        byte[] configs = [0, 0, 0, 2, .. String("cleanup.policy"), .. String("compact"), .. String("retention.ms"), 0xFF, 0xFF];

        var (written, log) = await ServeAsync(bus, Request(KafkaApiKey.CreateTopics, 2, CreateTopics(Topic("orders", configs: configs))));

        CollectionAssert.AreEqual(Response(Answer(("orders", 0))), written);
        CollectionAssert.AreEqual(new[] { "orders" }, bus.GetLogTopics().ToArray());
        CollectionAssert.AreEqual(new[] { "CreateTopics ignored the configs of orders: cleanup.policy, retention.ms." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRuleOnOneTopic_AnswersItsCodeAndLeavesThatTopicAbsent()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "kafka", Command = "CreateTopics", Path = "bad", Reply = "87" });
        var events = new RecordingSurlEventPublisher();

        var (written, log) = await ServeAsync(bus, faults, events, Request(KafkaApiKey.CreateTopics, 2, CreateTopics(Topic("bad"), Topic("good"))));

        CollectionAssert.AreEqual(Response(Answer(("bad", 87), ("good", 0))), written);
        CollectionAssert.AreEqual(new[] { "good" }, bus.GetLogTopics().ToArray());
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "bad"), events.Events.OfType<FaultAppliedEvent>().Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "kafka://127.0.0.1:9092/", Scheme = "kafka", Path = "bad", Command = "CreateTopics" },
            faults.Points[0]);
        CollectionAssert.AreEqual(new[] { "bad", "good" }, faults.Points.Select(point => point.Path).ToArray());
        Assert.Contains("CreateTopics of bad answered error 87 by fault rule f1.", log.Notes);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("0")]
    [DataRow("128")]
    [DataRow("-1")]
    [DataRow("busy")]
    public async Task ServeAsync_ErrorRuleWhoseReplyIsNotACodeOneTo127_IsNotAppliedAndNoted(string? reply)
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = reply });
        var events = new RecordingSurlEventPublisher();

        var (written, log) = await ServeAsync(bus, faults, events, Request(KafkaApiKey.CreateTopics, 2, CreateTopics(Topic("orders"))));

        CollectionAssert.AreEqual(Response(Answer(("orders", 0))), written);
        Assert.IsEmpty(events.Events.OfType<FaultAppliedEvent>());
        Assert.Contains("Fault rule f1 not applied to CreateTopics of orders: its reply is not a Kafka error code 1 to 127.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_RuleOfAnotherKind_LeavesTheTopicAnsweredAsUsual()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));

        var (written, _) = await ServeAsync(bus, faults, new RecordingSurlEventPublisher(), Request(KafkaApiKey.CreateTopics, 2, CreateTopics(Topic("orders"))));

        CollectionAssert.AreEqual(Response(Answer(("orders", 0))), written);
        CollectionAssert.AreEqual(new[] { "orders" }, bus.GetLogTopics().ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_DropRule_AbortsWithNoBytesAndCreatesNothing()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "CreateTopics", Path = "orders" });
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Request(KafkaApiKey.CreateTopics, 2, CreateTopics(Topic("orders"), Topic("other")))]);
        var context = Context(TestContext.CancellationToken, log) with { Faults = faults, Events = new RecordingSurlEventPublisher() };

        await Broker(bus).ServeAsync(connection, context);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(connection.Aborted);
        Assert.IsEmpty(bus.GetLogTopics());
        CollectionAssert.AreEqual(
            new[] { "CreateTopics dropped the connection, unanswered, by fault rule f1.", "CreateTopics was dropped by fault rule f1; closed with no reply." },
            log.Notes.ToArray());
    }

    private static KafkaBroker Broker(Surl.MessageBus.MessageBus bus) =>
        new(new UnitTestKafkaAuthenticationPolicy(true, "PLAIN"), [new KafkaCreateTopicsAnswerer(bus), new KafkaMetadataAnswerer(bus)]);

    private Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(Surl.MessageBus.MessageBus bus, params byte[][] requests) =>
        ServeAsync(bus, new ScriptedFaultPlan(), new RecordingSurlEventPublisher(), requests);

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(
        Surl.MessageBus.MessageBus bus, IFaultPlan faults, ISurlEventPublisher events, params byte[][] requests)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(requests.Select(request => (ReadOnlyMemory<byte>)request));
        var context = Context(TestContext.CancellationToken, log) with { Faults = faults, Events = events };

        await Broker(bus).ServeAsync(connection, context);

        return (connection.WrittenBytes, log);
    }

    // [topics], timeout_ms 30000, validate_only.
    private static byte[] CreateTopics(params byte[][] topics) => CreateTopics(validateOnly: false, topics);

    private static byte[] CreateTopics(bool validateOnly, params byte[][] topics) =>
        [.. Int32(topics.Length), .. topics.SelectMany(topic => topic), .. Int32(30000), (byte)(validateOnly ? 1 : 0)];

    private static byte[] Topic(string name, int partitions = 1, short replicationFactor = 1, byte[]? assignments = null, byte[]? configs = null) =>
        [.. String(name), .. Int32(partitions), .. Int16(replicationFactor), .. assignments ?? NoArray, .. configs ?? NoArray];

    private static byte[] Assignments(params (int Partition, int[] Nodes)[] assignments) =>
        [
            .. Int32(assignments.Length),
            .. assignments.SelectMany(assignment => (byte[])[.. Int32(assignment.Partition), .. Int32(assignment.Nodes.Length), .. assignment.Nodes.SelectMany(Int32)]),
        ];

    // throttle_time_ms 0, then each topic's name, error_code and a null error_message.
    private static byte[] Answer(params (string Name, short Error)[] topics) =>
        [0, 0, 0, 0, .. Int32(topics.Length), .. topics.SelectMany(topic => (byte[])[.. String(topic.Name), .. Int16(topic.Error), 0xFF, 0xFF])];
}
