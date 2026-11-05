using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaRecordBatches;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// Fetch v4 to v11 from the shared bus's topic logs (BL-520; ADR-0105 decisions 1, 3, 4 and 6),
/// every request and response byte written by hand from the Kafka protocol guide's Fetch schemas
/// and every batch from the "Record Batch" and "Record" layouts.
/// </summary>
[TestClass]
public sealed class KafkaFetchAnswererTests
{
    // ADR-0006's limits with no head timeout, so the only timer on the clock is a Fetch's wait.
    private static readonly ExchangeLimits NoHeadTimeout = new(Timeout.InfiniteTimeSpan, maxRequestHeadBytes: 102400, maxLineBytes: 8192, maxMessageBytes: 1048576, maxUploadBytes: 104857600);

    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private long Now => clock.GetUtcNow().ToUnixTimeMilliseconds();

    [TestMethod]
    public void Constructor_NullBus_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaFetchAnswerer(null!));

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var answerer = new KafkaFetchAnswerer(new Surl.MessageBus.MessageBus(clock));
        var request = new KafkaRequest(new KafkaRequestHeader(1, 4, 1, null), KafkaServedApis.Ranges[1], new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null));

        Assert.AreEqual(KafkaApiKey.Fetch, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    [DataRow((short)4)]
    [DataRow((short)5)]
    [DataRow((short)7)]
    [DataRow((short)9)]
    [DataRow((short)11)]
    public async Task ServeAsync_FromOffsetZero_ReturnsTheStoredRecordsAsOneBatchByteForByte(short version)
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        bus.Publish(new BusMessage("orders", "a"u8.ToArray()) { Key = "k"u8.ToArray(), Headers = [new("h", "1")] });
        bus.Publish(new BusMessage("orders", "b"u8.ToArray()));
        bus.Publish(new BusMessage("orders", "c"u8.ToArray()));

        var (written, _) = await ServeAsync(bus, Fetch(version, "orders", 0, 0));

        var batch = ServedBatch(0, Now, Now, Record(0, "k"u8.ToArray(), "a"u8.ToArray(), ("h"u8.ToArray(), "1"u8.ToArray())), Record(1, "b"), Record(2, "c"));
        CollectionAssert.AreEqual(Response(Answer(version, "orders", 0, 0, 3, 0, batch)), written);
    }

    [TestMethod]
    public async Task ServeAsync_RecordsPublishedAtDifferentTimes_CarryTimestampDeltasAndTheLastAsMaxTimestamp()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var first = Now;
        bus.Publish(new BusMessage("orders", "a"u8.ToArray()));
        clock.Advance(TimeSpan.FromMilliseconds(5));
        bus.Publish(new BusMessage("orders", "b"u8.ToArray()));

        var (written, _) = await ServeAsync(bus, Fetch(4, "orders", 0, 0));

        var batch = ServedBatch(0, first, first + 5, Record(0, "a"), TimedRecord(1, 5, null, "b"u8.ToArray()));
        CollectionAssert.AreEqual(Response(Answer(4, "orders", 0, 0, 2, 0, batch)), written);
    }

    [TestMethod]
    public async Task ServeAsync_FromTheMiddleOfAProducedBatch_ReturnsTheRestOfThatBatchWholeFromTheOffsetAsked()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var produced = Batch([Record(0, "a"), Record(1, "b"), Record(2, "c")]);

        var (written, _) = await ServeAsync(bus, Produce("orders", produced), Fetch(4, "orders", 0, 1));

        var batch = ServedBatch(1, Now, Now, Record(0, "b"), Record(1, "c"));
        var produceAnswer = Response([0, 0, 0, 1, .. String("orders"), 0, 0, 0, 1, .. Int32(0), 0, 0, .. Int64(0), .. Int64(Now), 0, 0, 0, 0]);
        CollectionAssert.AreEqual((byte[])[.. produceAnswer, .. Response(Answer(4, "orders", 0, 0, 3, 0, batch))], written);
    }

    [TestMethod]
    public async Task ServeAsync_PartitionMaxBytesSmallerThanTheFirstBatch_StillReturnsItsFirstRecord()
    {
        var bus = PublishedOn("orders", "a", "b");

        var (written, _) = await ServeAsync(bus, Fetch(4, "orders", 0, 0, partitionMaxBytes: 1));

        CollectionAssert.AreEqual(Response(Answer(4, "orders", 0, 0, 2, 0, ServedBatch(0, Now, Now, Record(0, "a")))), written);
    }

    [TestMethod]
    public async Task ServeAsync_MaxBytesSpentOnTheFirstPartition_LeavesTheNextEmpty()
    {
        var bus = PublishedOn("orders", "a");
        bus.Publish(new BusMessage("other", "b"u8.ToArray()));
        byte[] fields =
        [
            .. Int32(-1), .. Int32(0), .. Int32(0), .. Int32(1), 0,
            .. Int32(2),
            .. String("orders"), .. Int32(1), .. Int32(0), .. Int64(0), .. Int32(1000),
            .. String("other"), .. Int32(1), .. Int32(0), .. Int64(0), .. Int32(1000),
        ];

        var (written, _) = await ServeAsync(bus, Request(KafkaApiKey.Fetch, 4, fields));

        var batch = ServedBatch(0, Now, Now, Record(0, "a"));
        byte[] expected =
        [
            .. Int32(0), .. Int32(2),
            .. String("orders"), .. Int32(1), .. Int32(0), 0, 0, .. Int64(1), .. Int64(1), .. Int32(0), .. Int32(batch.Length), .. batch,
            .. String("other"), .. Int32(1), .. Int32(0), 0, 0, .. Int64(1), .. Int64(1), .. Int32(0), .. Int32(0),
        ];
        CollectionAssert.AreEqual(Response(expected), written);
    }

    [TestMethod]
    public async Task ServeAsync_MaxBytesAboveTheMessageLimit_IsCappedAtIt()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var value = new string('v', 150);
        bus.Publish(new BusMessage("orders", Encoding.UTF8.GetBytes(value)));
        bus.Publish(new BusMessage("orders", Encoding.UTF8.GetBytes(value)));
        var limits = new ExchangeLimits(Timeout.InfiniteTimeSpan, maxRequestHeadBytes: 102400, maxLineBytes: 8192, maxMessageBytes: 300, maxUploadBytes: 104857600);

        var (written, _) = await ServeAsync(bus, limits, Fetch(4, "orders", 0, 0));

        CollectionAssert.AreEqual(Response(Answer(4, "orders", 0, 0, 2, 0, ServedBatch(0, Now, Now, Record(0, value)))), written);
    }

    [TestMethod]
    public async Task ServeAsync_NoMessageLimit_LeavesMaxBytesAsAsked()
    {
        var bus = PublishedOn("orders", "a", "b");
        var limits = new ExchangeLimits(Timeout.InfiniteTimeSpan, maxRequestHeadBytes: 102400, maxLineBytes: 8192, maxMessageBytes: 0, maxUploadBytes: 104857600);

        var (written, _) = await ServeAsync(bus, limits, Fetch(4, "orders", 0, 0));

        CollectionAssert.AreEqual(Response(Answer(4, "orders", 0, 0, 2, 0, ServedBatch(0, Now, Now, Record(0, "a"), Record(1, "b")))), written);
    }

    [TestMethod]
    public async Task ServeAsync_AtTheLogEndWithMinBytesZero_AnswersAtOnceWithNoRecords()
    {
        var bus = PublishedOn("orders", "a");

        var (written, _) = await ServeAsync(bus, Fetch(5, "orders", 0, 1, maxWait: 500));

        CollectionAssert.AreEqual(Response(Answer(5, "orders", 0, 0, 1, 0, [])), written);
    }

    [TestMethod]
    [DataRow(2L)]
    [DataRow(-1L)]
    public async Task ServeAsync_AnOffsetOutsideTheLog_IsOffsetOutOfRange(long offset)
    {
        var bus = PublishedOn("orders", "a");

        var (written, _) = await ServeAsync(bus, Fetch(5, "orders", 0, offset, maxWait: 500, minBytes: 1));

        CollectionAssert.AreEqual(Response(Answer(5, "orders", 0, 1, -1, -1, [])), written);
    }

    [TestMethod]
    public async Task ServeAsync_PastTheEndWithMinBytesOne_WaitsUntilAProduceArrives()
    {
        var bus = PublishedOn("orders", "a");

        var serving = ServeAsync(bus, Fetch(4, "orders", 0, 1, maxWait: 500, minBytes: 1));
        await UntilTimerPendingAsync();
        clock.Advance(TimeSpan.FromMilliseconds(10));
        bus.Publish(new BusMessage("orders", "b"u8.ToArray()));
        var (written, _) = await serving;

        CollectionAssert.AreEqual(Response(Answer(4, "orders", 0, 0, 2, 0, ServedBatch(1, Now, Now, Record(0, "b")))), written);
    }

    [TestMethod]
    public async Task ServeAsync_PastTheEndWithMinBytesOne_AnswersEmptyWhenMaxWaitPasses()
    {
        var bus = PublishedOn("orders", "a");

        var serving = ServeAsync(bus, Fetch(4, "orders", 0, 1, maxWait: 500, minBytes: 1));
        await UntilTimerPendingAsync();
        clock.Advance(TimeSpan.FromMilliseconds(500));
        var (written, _) = await serving;

        CollectionAssert.AreEqual(Response(Answer(4, "orders", 0, 0, 1, 0, [])), written);
    }

    [TestMethod]
    public async Task ServeAsync_ARecordShortOfMinBytes_KeepsWaitingAndAnswersItWhenMaxWaitPasses()
    {
        var bus = PublishedOn("orders", "a");

        var serving = ServeAsync(bus, Fetch(4, "orders", 0, 1, maxWait: 500, minBytes: 10_000));
        await UntilTimerPendingAsync();
        bus.Publish(new BusMessage("orders", "b"u8.ToArray()));
        clock.Advance(TimeSpan.FromMilliseconds(500));
        var (written, _) = await serving;

        CollectionAssert.AreEqual(Response(Answer(4, "orders", 0, 0, 2, 0, ServedBatch(1, Now - 500, Now - 500, Record(0, "b")))), written);
    }

    [TestMethod]
    public async Task ServeAsync_MaxWaitAboveTheCap_WaitsSixtySecondsAtMost()
    {
        var bus = PublishedOn("orders", "a");

        var serving = ServeAsync(bus, Fetch(4, "orders", 0, 1, maxWait: 600_000, minBytes: 1));
        await UntilTimerPendingAsync();
        clock.Advance(TimeSpan.FromMilliseconds(KafkaFetchAnswerer.MaxWaitMilliseconds));
        var (written, _) = await serving;

        CollectionAssert.AreEqual(Response(Answer(4, "orders", 0, 0, 1, 0, [])), written);
    }

    [TestMethod]
    public async Task AnswerAsync_TheExchangeCancelledDuringTheWait_Throws()
    {
        var bus = PublishedOn("orders", "a");
        var request = Fetch(4, "orders", 0, 1, maxWait: 500, minBytes: 1);
        var fields = new KafkaPrimitiveReader(request.AsMemory(4));
        var header = KafkaRequestHeader.Read(fields, 1);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await new KafkaFetchAnswerer(bus).AnswerAsync(
            new KafkaRequest(header, KafkaServedApis.Find((short)KafkaApiKey.Fetch)!, fields, new KafkaConnectionState(null)),
            Context(cancellation.Token, timeProvider: clock)));
    }

    [TestMethod]
    public async Task ServeAsync_AMessagePublishedUnderAnMqttName_IsFetchedAsARecordOfTheMappedTopic()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        bus.Publish(new BusMessage(MqttTopicNames.ToCanonicalTopic("shop/orders").Name!, "hello"u8.ToArray())
        {
            Key = "k"u8.ToArray(),
            Headers = [new("h", "1"), new("h", "2")],
        });

        var (written, _) = await ServeAsync(bus, Fetch(4, "shop.orders", 0, 0));

        var record = Record(0, "k"u8.ToArray(), "hello"u8.ToArray(), ("h"u8.ToArray(), "1"u8.ToArray()), ("h"u8.ToArray(), "2"u8.ToArray()));
        CollectionAssert.AreEqual(Response(Answer(4, "shop.orders", 0, 0, 1, 0, ServedBatch(0, Now, Now, record))), written);
    }

    [TestMethod]
    public async Task ServeAsync_ATopicTheCallerMayNotSubscribeTo_IsTopicAuthorizationFailed()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, configuration: new MessageBusConfiguration
        {
            AccessGrants = [new TopicAccessGrant("orders", TopicAccessGrant.Everyone, TopicAccessRights.Publish)],
        });
        bus.Publish(new BusMessage("orders", "a"u8.ToArray()));

        var (written, _) = await ServeAsync(bus, Fetch(4, "orders", 0, 0, maxWait: 500, minBytes: 1));

        CollectionAssert.AreEqual(Response(Answer(4, "orders", 0, 29, -1, -1, [])), written);
    }

    [TestMethod]
    [DataRow("no way", 0, (short)17)]
    [DataRow("orders", 1, (short)3)]
    [DataRow("unknown", 0, (short)3)]
    public async Task ServeAsync_ATopicOrPartitionThatCannotBeRead_IsAnsweredWithItsError(string topic, int partition, short error)
    {
        var bus = PublishedOn("orders", "a");

        var (written, _) = await ServeAsync(bus, Fetch(4, topic, partition, 0));

        CollectionAssert.AreEqual(Response(Answer(4, topic, partition, error, -1, -1, [])), written);
    }

    [TestMethod]
    public async Task ServeAsync_ARequestContinuingAFetchSession_IsFetchSessionIdNotFound()
    {
        var bus = PublishedOn("orders", "a");

        var (written, _) = await ServeAsync(bus, Fetch(7, "orders", 0, 0, sessionId: 5));

        CollectionAssert.AreEqual(Response([.. Int32(0), 0, 70, .. Int32(0), .. Int32(0)]), written);
    }

    // A bus with one message a value on topic, published now.
    private Surl.MessageBus.MessageBus PublishedOn(string topic, params string[] values)
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        foreach (var value in values)
        {
            bus.Publish(new BusMessage(topic, Encoding.UTF8.GetBytes(value)));
        }

        return bus;
    }

    private async Task UntilTimerPendingAsync()
    {
        while (clock.PendingTimerCount == 0)
        {
            await Task.Yield();
        }
    }

    // A Fetch request: replica_id -1, max_wait_ms, min_bytes, max_bytes, isolation_level 0,
    // (v7) session_id and session_epoch -1, one topic with one partition - (v9)
    // current_leader_epoch 0, fetch_offset, (v5) log_start_offset 0, partition_max_bytes - then
    // (v7) no forgotten topics and (v11) rack_id "".
    private static byte[] Fetch(short version, string topic, int partition, long offset, int partitionMaxBytes = 1_048_576, int maxWait = 0, int minBytes = 0, int maxBytes = 52_428_800, int sessionId = 0)
    {
        byte[] fields =
        [
            .. Int32(-1), .. Int32(maxWait), .. Int32(minBytes), .. Int32(maxBytes), 0,
            .. version >= 7 ? [.. Int32(sessionId), .. Int32(-1)] : Array.Empty<byte>(),
            .. Int32(1), .. String(topic), .. Int32(1), .. Int32(partition),
            .. version >= 9 ? Int32(0) : [],
            .. Int64(offset),
            .. version >= 5 ? Int64(0) : [],
            .. Int32(partitionMaxBytes),
            .. version >= 7 ? Int32(0) : [],
            .. version >= 11 ? String(string.Empty) : [],
        ];
        return Request(KafkaApiKey.Fetch, version, fields);
    }

    // A Fetch response body: throttle_time_ms 0, (v7) error_code 0 and session_id 0, one topic
    // with one partition - high_watermark and last_stable_offset alike, (v5) log_start_offset,
    // no aborted transactions, (v11) preferred_read_replica -1, records.
    private static byte[] Answer(short version, string topic, int partition, short error, long highWatermark, long logStartOffset, byte[] records) =>
    [
        .. Int32(0),
        .. version >= 7 ? [0, 0, .. Int32(0)] : Array.Empty<byte>(),
        .. Int32(1), .. String(topic), .. Int32(1), .. Int32(partition), .. Int16(error),
        .. Int64(highWatermark), .. Int64(highWatermark),
        .. version >= 5 ? Int64(logStartOffset) : [],
        .. Int32(0),
        .. version >= 11 ? Int32(-1) : [],
        .. Int32(records.Length), .. records,
    ];

    // A Produce v3 request with acks -1 and one partition of one topic.
    private static byte[] Produce(string topic, byte[] records) => Request(
        KafkaApiKey.Produce,
        3,
        [0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0x75, 0x30, 0, 0, 0, 1, .. String(topic), 0, 0, 0, 1, .. Int32(0), .. Int32(records.Length), .. records]);

    private Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(Surl.MessageBus.MessageBus bus, params byte[][] requests) =>
        ServeAsync(bus, NoHeadTimeout, requests);

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(Surl.MessageBus.MessageBus bus, ExchangeLimits limits, params byte[][] requests)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(requests.Select(request => (ReadOnlyMemory<byte>)request));
        var broker = new KafkaBroker(new UnitTestKafkaAuthenticationPolicy(true, "PLAIN"), [new KafkaProduceAnswerer(bus), new KafkaFetchAnswerer(bus)]);

        await broker.ServeAsync(connection, Context(TestContext.CancellationToken, log, timeProvider: clock) with { Limits = limits });

        return (connection.WrittenBytes, log);
    }
}
