using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// InitProducerId v0 and v1 (BL-951; ADR-0145 decisions 8 and 10), every byte written by hand from
/// the Kafka protocol guide's InitProducerId schemas: a request is <c>transactional_id
/// transaction_timeout_ms</c>; a response is <c>throttle_time_ms error_code producer_id
/// producer_epoch</c>.
/// </summary>
[TestClass]
public sealed class KafkaInitProducerIdAnswererTests
{
    private static readonly byte[] NullString = [0xFF, 0xFF];

    // 0xFFEEDDCCBBAA9988 big-endian, masked to its low 47 bits: 0x00005DCCBBAA9988.
    private const long FirstProducerId = 0x0000_5DCC_BBAA_9988;

    private static readonly byte[] IdentifierBytes = [0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullProducerStates_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaInitProducerIdAnswerer(null!));

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var answerer = new KafkaInitProducerIdAnswerer(new KafkaProducerStates());
        var request = new KafkaRequest(new KafkaRequestHeader(22, 0, 1, null), KafkaServedApis.Find(22)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.AreEqual(KafkaApiKey.InitProducerId, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    public async Task ServeAsync_TwoCallsWithANullTransactionalId_AnswerConsecutiveIdsFromTheIdentifierStartAndEpochZero()
    {
        var identifiers = new FixedBytesIdentifierRandom(IdentifierBytes);

        var (written, _) = await ServeAsync(new ScriptedFaultPlan(), new RecordingSurlEventPublisher(), identifiers, InitProducerId(0, null), InitProducerId(1, null));

        byte[] expected = [.. Response(Answer(0, FirstProducerId, 0)), .. Response(Answer(0, FirstProducerId + 1, 0))];
        CollectionAssert.AreEqual(expected, written);
        Assert.AreEqual(1, identifiers.Calls);
    }

    [TestMethod]
    [DataRow("tx")]
    [DataRow("")]
    public async Task ServeAsync_ATransactionalIdEvenEmpty_IsInvalidRequestWithMinusOneIdAndEpoch(string transactionalId)
    {
        var identifiers = new FixedBytesIdentifierRandom(IdentifierBytes);

        var (written, _) = await ServeAsync(new ScriptedFaultPlan(), new RecordingSurlEventPublisher(), identifiers, InitProducerId(1, transactionalId));

        CollectionAssert.AreEqual(Response(Answer(42, -1, -1)), written);
        Assert.AreEqual(0, identifiers.Calls);
    }

    [TestMethod]
    public async Task ServeAsync_ErrorRule_AnswersItsCodeWithMinusOneAndDrawsNoId()
    {
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "kafka", Command = "InitProducerId", Reply = "15" });
        var events = new RecordingSurlEventPublisher();
        var identifiers = new FixedBytesIdentifierRandom(IdentifierBytes);

        var (written, log) = await ServeAsync(faults, events, identifiers, InitProducerId(0, null));

        CollectionAssert.AreEqual(Response(Answer(15, -1, -1)), written);
        Assert.AreEqual(0, identifiers.Calls);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "InitProducerId"), events.Events.OfType<FaultAppliedEvent>().Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "kafka://127.0.0.1:9092/", Scheme = "kafka", Command = "InitProducerId" },
            faults.Points.Single());
        Assert.Contains("InitProducerId answered error 15 by fault rule f1.", log.Notes);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("0")]
    [DataRow("128")]
    [DataRow("busy")]
    public async Task ServeAsync_ErrorRuleWhoseReplyIsNotACodeOneTo127_IsNotAppliedAndNoted(string? reply)
    {
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = reply });
        var events = new RecordingSurlEventPublisher();

        var (written, log) = await ServeAsync(faults, events, new FixedBytesIdentifierRandom(IdentifierBytes), InitProducerId(0, null));

        CollectionAssert.AreEqual(Response(Answer(0, FirstProducerId, 0)), written);
        Assert.IsEmpty(events.Events.OfType<FaultAppliedEvent>());
        Assert.Contains("Fault rule f1 not applied to InitProducerId: its reply is not a Kafka error code 1 to 127.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_RuleOfAnotherKind_IsAnsweredAsUsual()
    {
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));

        var (written, _) = await ServeAsync(faults, new RecordingSurlEventPublisher(), new FixedBytesIdentifierRandom(IdentifierBytes), InitProducerId(0, null));

        CollectionAssert.AreEqual(Response(Answer(0, FirstProducerId, 0)), written);
    }

    [TestMethod]
    public async Task ServeAsync_DropRule_AbortsWithNoBytes()
    {
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Command = "InitProducerId" });
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([InitProducerId(0, null)]);
        var context = Context(TestContext.CancellationToken, log) with { Faults = faults, Events = new RecordingSurlEventPublisher() };

        await Broker(new KafkaProducerStates()).ServeAsync(connection, context);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(connection.Aborted);
        Assert.Contains("InitProducerId was dropped by fault rule f1; closed with no reply.", log.Notes);
    }

    private static KafkaBroker Broker(KafkaProducerStates states) =>
        new(new UnitTestKafkaAuthenticationPolicy(true, "PLAIN"), [new KafkaInitProducerIdAnswerer(states)]);

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(
        IFaultPlan faults, ISurlEventPublisher events, IIdentifierRandom identifiers, params byte[][] requests)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(requests.Select(request => (ReadOnlyMemory<byte>)request));
        var context = Context(TestContext.CancellationToken, log) with { Faults = faults, Events = events, Identifiers = identifiers };

        await Broker(new KafkaProducerStates()).ServeAsync(connection, context);

        return (connection.WrittenBytes, log);
    }

    // transactional_id, transaction_timeout_ms 60000.
    private static byte[] InitProducerId(short version, string? transactionalId) =>
        Request(KafkaApiKey.InitProducerId, version, [.. transactionalId is null ? NullString : String(transactionalId), 0, 0, 0xEA, 0x60]);

    // throttle_time_ms 0, error_code, producer_id, producer_epoch.
    private static byte[] Answer(short error, long producerId, short epoch) =>
        [0, 0, 0, 0, .. Int16(error), .. KafkaRecordBatches.Int64(producerId), .. Int16(epoch)];
}
