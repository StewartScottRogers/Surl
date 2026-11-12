using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaRecordBatches;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9): a matching
/// <see cref="FaultKind.Error"/> rule's error code is answered in each partition of the topic it
/// names, in the Produce or Fetch response, nothing is appended or read for it, and one
/// <see cref="FaultAppliedEvent"/> is published. Every byte is written from the Kafka protocol
/// guide's Produce v3 and Fetch v4 schemas.
/// </summary>
[TestClass]
public sealed class KafkaChosenErrorFaultTests
{
    private static readonly ExchangeLimits NoHeadTimeout = new(Timeout.InfiniteTimeSpan, maxRequestHeadBytes: 102400, maxLineBytes: 8192, maxMessageBytes: 1048576, maxUploadBytes: 104857600);

    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_ProduceToARulesTopic_AnswersTheChosenErrorAndAppendsNothing()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(Rule("Produce", "orders"));

        var (written, log) = await ServeAsync(bus, plan, events, Produce(1, "orders", Batch([Record(0, "a")])));

        CollectionAssert.AreEqual(Response(ProduceAnswer("orders", 7, -1, -1)), written);
        Assert.IsEmpty(bus.ReadLog("orders", 0, long.MaxValue).Records);
        AssertOneFaultApplied(events, "Produce orders");
        CollectionAssert.Contains(log.Notes.ToArray(), "Produce orders answered by fault rule f1 with error code 7.");
        var point = plan.Points.Single();
        Assert.AreEqual(FaultStage.Command, point.Stage);
        Assert.AreEqual(1L, point.ExchangeId);
        Assert.AreEqual("kafka://127.0.0.1:9092/", point.ListenUrl);
        Assert.AreEqual("kafka", point.Scheme);
        Assert.IsNull(point.Account);
        Assert.AreEqual("orders", point.Path);
    }

    [TestMethod]
    public async Task ServeAsync_ProduceToAnotherTopic_IsAppended()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var events = new RecordingSurlEventPublisher();

        var (written, _) = await ServeAsync(bus, new ScriptedFaultPlan(Rule("Produce", "audit")), events, Produce(1, "orders", Batch([Record(0, "a")])));

        CollectionAssert.AreEqual(Response(ProduceAnswer("orders", 0, 0, clock.GetUtcNow().ToUnixTimeMilliseconds())), written);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_ProduceRefusedAsAWhole_AsksThePlanNothing()
    {
        var plan = new ScriptedFaultPlan(Rule("Produce", "orders"));

        var (written, _) = await ServeAsync(new Surl.MessageBus.MessageBus(clock), plan, new RecordingSurlEventPublisher(), Produce(2, "orders", Batch([Record(0, "a")])));

        CollectionAssert.AreEqual(Response(ProduceAnswer("orders", 21, -1, -1)), written);
        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public async Task ServeAsync_FetchFromARulesTopic_AnswersTheChosenErrorWithoutWaiting()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        bus.Publish(new BusMessage("orders", Encoding.UTF8.GetBytes("a")));
        var events = new RecordingSurlEventPublisher();

        var (written, _) = await ServeAsync(bus, new ScriptedFaultPlan(Rule("Fetch", "orders")), events, Fetch("orders", maxWait: 500, minBytes: 1_000_000));

        CollectionAssert.AreEqual(Response(FetchAnswer("orders", 7)), written);
        AssertOneFaultApplied(events, "Fetch orders");
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("128")]
    [DataRow("7 busy")]
    [DataRow(null)]
    public async Task ServeAsync_ARuleWhoseReplyIsNotAnErrorCode_IsNotedAndNotApplied(string? reply)
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var events = new RecordingSurlEventPublisher();

        var (written, log) = await ServeAsync(bus, new ScriptedFaultPlan(Rule("Produce", "orders") with { Reply = reply }), events, Produce(1, "orders", Batch([Record(0, "a")])));

        CollectionAssert.AreEqual(Response(ProduceAnswer("orders", 0, 0, clock.GetUtcNow().ToUnixTimeMilliseconds())), written);
        Assert.IsEmpty(events.Events);
        CollectionAssert.Contains(log.Notes.ToArray(), "Produce orders: fault rule f1 is not an error with a Kafka error code 1 to 127; not applied.");
    }

    [TestMethod]
    public async Task ServeAsync_ADelayRuleAtTheCommandStage_IsNotApplied()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Delay) { Command = "Produce", DelayMilliseconds = 10 });

        var (written, _) = await ServeAsync(new Surl.MessageBus.MessageBus(clock), plan, events, Produce(1, "orders", Batch([Record(0, "a")])));

        CollectionAssert.AreEqual(Response(ProduceAnswer("orders", 0, 0, clock.GetUtcNow().ToUnixTimeMilliseconds())), written);
        Assert.IsEmpty(events.Events);
    }

    private static FaultRule Rule(string command, string path) =>
        new("f1", FaultKind.Error) { Scheme = "kafka", Command = command, Path = path, Reply = "7" };

    private static void AssertOneFaultApplied(RecordingSurlEventPublisher events, string target) =>
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, target), events.Events.OfType<FaultAppliedEvent>().Single());

    // A Produce v3 request: no transactional_id, acks, timeout_ms 30000, one topic, partition 0.
    private static byte[] Produce(short acks, string topic, byte[] records) => Request(
        KafkaApiKey.Produce,
        3,
        [0xFF, 0xFF, .. Int16(acks), 0, 0, 0x75, 0x30, .. Int32(1), .. String(topic), .. Int32(1), .. Int32(0), .. Int32(records.Length), .. records]);

    // A Produce v3 response body: one topic, partition 0, base_offset, log_append_time_ms, throttle 0.
    private static byte[] ProduceAnswer(string topic, short error, long baseOffset, long logAppendTime) =>
        [.. Int32(1), .. String(topic), .. Int32(1), .. Int32(0), .. Int16(error), .. Int64(baseOffset), .. Int64(logAppendTime), .. Int32(0)];

    // A Fetch v4 request: replica_id -1, max_wait_ms, min_bytes, max_bytes, isolation_level 0,
    // one topic with partition 0 at fetch_offset 0 and partition_max_bytes 1 MiB.
    private static byte[] Fetch(string topic, int maxWait, int minBytes) => Request(
        KafkaApiKey.Fetch,
        4,
        [.. Int32(-1), .. Int32(maxWait), .. Int32(minBytes), .. Int32(52_428_800), 0, .. Int32(1), .. String(topic), .. Int32(1), .. Int32(0), .. Int64(0), .. Int32(1_048_576)]);

    // A failed Fetch v4 partition: throttle 0, one topic, partition 0, the error, -1 offsets,
    // no aborted transactions, empty records.
    private static byte[] FetchAnswer(string topic, short error) =>
        [.. Int32(0), .. Int32(1), .. String(topic), .. Int32(1), .. Int32(0), .. Int16(error), .. Int64(-1), .. Int64(-1), .. Int32(0), .. Int32(0)];

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(Surl.MessageBus.MessageBus bus, IFaultPlan plan, ISurlEventPublisher events, params byte[][] requests)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(requests.Select(request => (ReadOnlyMemory<byte>)request));
        var broker = new KafkaBroker(new UnitTestKafkaAuthenticationPolicy(true, "PLAIN"), [new KafkaProduceAnswerer(bus), new KafkaFetchAnswerer(bus)]);
        var context = Context(TestContext.CancellationToken, log, timeProvider: clock) with { Limits = NoHeadTimeout, Faults = plan, Events = events };

        await broker.ServeAsync(connection, context);

        return (connection.WrittenBytes, log);
    }
}
