using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaRecordBatches;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// Produce v3 to v8 into the shared bus's topic logs (BL-519; ADR-0105 decisions 3, 4, 6 and 7),
/// every request and response byte written by hand from the Kafka protocol guide's Produce
/// schemas and every batch from the "Record Batch" layout.
/// </summary>
[TestClass]
public sealed class KafkaProduceAnswererTests
{
    private static readonly byte[] NullString = [0xFF, 0xFF];
    private static readonly byte[] NoOffset = Int64(-1);

    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private byte[] AppendTime => Int64(clock.GetUtcNow().ToUnixTimeMilliseconds());

    [TestMethod]
    public void Constructor_NullBus_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaProduceAnswerer(null!));

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var answerer = new KafkaProduceAnswerer(new Surl.MessageBus.MessageBus(clock));
        var request = new KafkaRequest(new KafkaRequestHeader(0, 3, 1, null), KafkaServedApis.Ranges[0], new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        Assert.AreEqual(KafkaApiKey.Produce, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    public async Task ServeAsync_ABatchOfThreeThenABatchOfOne_AreAppendedAtOffsetZeroThenThree()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var first = Batch([Record(0, "a"), Record(1, "b"), Record(2, "c")]);
        var second = Batch([Record(0, "d")]);

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, first), Produce(3, 1, "orders", 0, second));

        byte[] expected = [.. Response(Answer("orders", 0, 0, Int64(0), AppendTime)), .. Response(Answer("orders", 0, 0, Int64(3), AppendTime))];
        CollectionAssert.AreEqual(expected, written);
        var log = bus.ReadLog("orders", 0, long.MaxValue);
        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, log.Records.Select(record => Encoding.UTF8.GetString(record.Message.Payload.Span)).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_AProducedRecord_IsDeliveredToABusSubscriberOfTheMappedTopic()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var received = new List<BusMessage>();
        using var subscriber = bus.Connect();
        subscriber.Subscribe(MqttTopicNames.ToCanonicalFilter("shop/orders").Name!, received.Add);
        var batch = Batch([Record(0, "k"u8.ToArray(), "hello"u8.ToArray(), ("h"u8.ToArray(), "1"u8.ToArray())), Record(1, null, null)]);

        await ServeAsync(bus, Produce(3, -1, "shop.orders", 0, batch));

        Assert.HasCount(2, received);
        Assert.AreEqual("shop/orders", received[0].Topic);
        CollectionAssert.AreEqual("hello"u8.ToArray(), received[0].Payload.ToArray());
        CollectionAssert.AreEqual("k"u8.ToArray(), received[0].Key!.Value.ToArray());
        CollectionAssert.AreEqual(new[] { new BusMessageHeader("h", "1") }, received[0].Headers.ToArray());
        Assert.AreEqual(BusQualityOfService.AtLeastOnce, received[0].QualityOfService);
        Assert.IsTrue(received[1].Payload.IsEmpty);
        Assert.IsNull(received[1].Key);
    }

    [TestMethod]
    public async Task ServeAsync_AFlippedByte_IsCorruptMessageAndNothingIsAppended()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var batch = Batch([Record(0, "a"), Record(1, "b")]);
        batch[^1] ^= 0x01;

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, batch));

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 2, NoOffset, NoOffset)), written);
        Assert.IsEmpty(bus.GetLogTopics());
    }

    [TestMethod]
    public async Task ServeAsync_MagicOne_IsUnsupportedForMessageFormat()
    {
        var (written, _) = await ServeAsync(new Surl.MessageBus.MessageBus(clock), Produce(3, -1, "orders", 0, Batch([Record(0, "a")], magic: 1)));

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 43, NoOffset, NoOffset)), written);
    }

    [TestMethod]
    [DynamicData(nameof(KafkaCompressedPayloads.Codecs), typeof(KafkaCompressedPayloads))]
    public async Task ServeAsync_ACompressedBatchOfThree_IsAppendedAsOneBusMessageARecordAtOffsetsOneToThree(short codec, string format)
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        byte[] records = [.. Record(0, "k"u8.ToArray(), "b"u8.ToArray(), ("h"u8.ToArray(), "1"u8.ToArray())), .. Record(1, "c"), .. Record(2, "d")];
        var compressed = CompressedBatch(codec, 3, KafkaCompressedPayloads.Compress(format, records));

        var (written, _) = await ServeAsync(bus, Produce(3, 1, "orders", 0, Batch([Record(0, "a")])), Produce(3, 1, "orders", 0, compressed));

        byte[] expected = [.. Response(Answer("orders", 0, 0, Int64(0), AppendTime)), .. Response(Answer("orders", 0, 0, Int64(1), AppendTime))];
        CollectionAssert.AreEqual(expected, written);
        var log = bus.ReadLog("orders", 0, long.MaxValue).Records;
        CollectionAssert.AreEqual(new long[] { 0, 1, 2, 3 }, log.Select(record => record.Offset).ToArray());
        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, log.Select(record => Encoding.UTF8.GetString(record.Message.Payload.Span)).ToArray());
        CollectionAssert.AreEqual("k"u8.ToArray(), log[1].Message.Key!.Value.ToArray());
        CollectionAssert.AreEqual(new[] { new BusMessageHeader("h", "1") }, log[1].Message.Headers.ToArray());
    }

    [TestMethod]
    [DataRow((short)5, DisplayName = "codec 5")]
    [DataRow((short)6, DisplayName = "codec 6")]
    [DataRow((short)7, DisplayName = "codec 7")]
    public async Task ServeAsync_ACodecFiveToSeven_IsCorruptMessage(short codec)
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, CompressedBatch(codec, 1, Record(0, "a"))));

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 2, NoOffset, NoOffset)), written);
        Assert.IsEmpty(bus.GetLogTopics());
    }

    [TestMethod]
    public async Task ServeAsync_AGzipBatchThatIsNotGzip_IsCorruptMessageAndAppendsNothing()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, CompressedBatch(1, 1, Record(0, "a"))));

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 2, NoOffset, NoOffset)), written);
        Assert.IsEmpty(bus.GetLogTopics());
    }

    [TestMethod]
    public async Task ServeAsync_ACompressedBatchPastFourTimesMaxMessage_IsMessageTooLarge()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), maxRequestHeadBytes: 102400, maxLineBytes: 8192, maxMessageBytes: 200, maxUploadBytes: 104857600);
        var record = Record(0, new string('x', 801));
        var request = Produce(3, -1, "orders", 0, CompressedBatch(1, 1, KafkaCompressedPayloads.Gzip(record)));
        Assert.IsLessThan(200, request.Length - 4);

        var (written, _) = await ServeAsync(bus, limits, request);

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 10, NoOffset, NoOffset)), written);
        Assert.IsEmpty(bus.GetLogTopics());
    }

    [TestMethod]
    public async Task ServeAsync_NullRecords_IsCorruptMessage()
    {
        var (written, _) = await ServeAsync(new Surl.MessageBus.MessageBus(clock), Produce(3, -1, "orders", 0, null));

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 2, NoOffset, NoOffset)), written);
    }

    [TestMethod]
    public async Task ServeAsync_AcksZero_WritesNoResponseAndAppends()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, log) = await ServeAsync(bus, Produce(3, 0, "orders", 0, Batch([Record(0, "a")])));

        Assert.IsEmpty(written);
        Assert.IsEmpty(log.Notes);
        Assert.AreEqual(1, bus.GetLogOffsets("orders").HighWatermark);
    }

    [TestMethod]
    public async Task ServeAsync_AcksZeroWithAFailedPartition_WritesNoResponseAndNotesTheError()
    {
        var (written, log) = await ServeAsync(new Surl.MessageBus.MessageBus(clock), Produce(3, 0, "orders", 1, Batch([Record(0, "a")])));

        Assert.IsEmpty(written);
        Assert.AreEqual("Produce with acks 0: topic orders partition 1 was refused with error 3 (UnknownTopicOrPartition); no response is written.", log.Notes.Single());
    }

    [TestMethod]
    [DataRow((short)2)]
    [DataRow((short)-2)]
    public async Task ServeAsync_AcksOtherThanZeroOneOrMinusOne_IsInvalidRequiredAcksAndNothingIsAppended(short acks)
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, _) = await ServeAsync(bus, Produce(3, acks, "orders", 0, Batch([Record(0, "a")])));

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 21, NoOffset, NoOffset)), written);
        Assert.IsEmpty(bus.GetLogTopics());
    }

    [TestMethod]
    public async Task ServeAsync_ATransactionalId_IsUnsupportedForMessageFormat()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, Batch([Record(0, "a")]), transactionalId: "tx"));

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 43, NoOffset, NoOffset)), written);
        Assert.IsEmpty(bus.GetLogTopics());
    }

    [TestMethod]
    public async Task ServeAsync_ARecordPastTheBusMessageLimit_IsMessageTooLarge()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, maxMessageBytes: 4);
        var batch = Batch([Record(0, "a"), Record(1, "kkk"u8.ToArray(), "vv"u8.ToArray())]);

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, batch));

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 10, NoOffset, NoOffset)), written);
        Assert.IsEmpty(bus.GetLogTopics());
    }

    [TestMethod]
    public async Task ServeAsync_AnUnknownPartition_IsUnknownTopicOrPartition()
    {
        var (written, _) = await ServeAsync(new Surl.MessageBus.MessageBus(clock), Produce(3, -1, "orders", 1, Batch([Record(0, "a")])));

        CollectionAssert.AreEqual(Response(Answer("orders", 1, 3, NoOffset, NoOffset)), written);
    }

    [TestMethod]
    public async Task ServeAsync_AnInvalidTopicName_IsInvalidTopicException()
    {
        var (written, _) = await ServeAsync(new Surl.MessageBus.MessageBus(clock), Produce(3, -1, "no way", 0, Batch([Record(0, "a")])));

        CollectionAssert.AreEqual(Response(Answer("no way", 0, 17, NoOffset, NoOffset)), written);
    }

    [TestMethod]
    public async Task ServeAsync_ATopicTheCallerMayNotPublishTo_IsTopicAuthorizationFailed()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, configuration: new MessageBusConfiguration
        {
            AccessGrants = [new TopicAccessGrant("orders", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe)],
        });

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, Batch([Record(0, "a")])));

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 29, NoOffset, NoOffset)), written);
        Assert.AreEqual(0, bus.GetLogOffsets("orders").HighWatermark);
    }

    [TestMethod]
    public async Task ServeAsync_ANewTopicPastTheBusTopicLimit_IsPolicyViolation()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, maxTopics: 1);
        bus.CreateLog("other");

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, Batch([Record(0, "a")])));

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 44, NoOffset, NoOffset)), written);
    }

    [TestMethod]
    public async Task ServeAsync_ARecordPastTheBusLogBound_IsPolicyViolation()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, maxLogBytes: 20);

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, Batch([Record(0, "hello")])));

        CollectionAssert.AreEqual(Response(Answer("orders", 0, 44, NoOffset, NoOffset)), written);
        Assert.AreEqual(0, bus.GetLogOffsets("orders").HighWatermark);
    }

    [TestMethod]
    public async Task ServeAsync_VersionEight_AnswersTheLogStartOffsetEmptyRecordErrorsAndANullErrorMessage()
    {
        // Each one-byte record counts 17 bytes; a 40-byte topic log keeps two of three.
        var bus = new Surl.MessageBus.MessageBus(clock, maxLogBytesPerTopic: 40);

        var (written, _) = await ServeAsync(bus, Produce(8, -1, "orders", 0, Batch([Record(0, "a"), Record(1, "b"), Record(2, "c")])));

        byte[] partition = [.. Int32(0), 0, 0, .. Int64(0), .. AppendTime, .. Int64(1), 0, 0, 0, 0, .. NullString];
        CollectionAssert.AreEqual(Response([0, 0, 0, 1, .. String("orders"), 0, 0, 0, 1, .. partition, 0, 0, 0, 0]), written);
    }

    [TestMethod]
    public async Task ServeAsync_VersionFiveForAFailedPartition_AnswersMinusOneAsTheLogStartOffset()
    {
        var (written, _) = await ServeAsync(new Surl.MessageBus.MessageBus(clock), Produce(5, -1, "orders", 1, Batch([Record(0, "a")])));

        byte[] partition = [.. Int32(1), 0, 3, .. NoOffset, .. NoOffset, .. NoOffset];
        CollectionAssert.AreEqual(Response([0, 0, 0, 1, .. String("orders"), 0, 0, 0, 1, .. partition, 0, 0, 0, 0]), written);
    }

    [TestMethod]
    public async Task ServeAsync_NullTopicData_AnswersNoTopics()
    {
        var (written, _) = await ServeAsync(new Surl.MessageBus.MessageBus(clock), Request(KafkaApiKey.Produce, 3, [.. NullString, 0xFF, 0xFF, 0, 0, 0x75, 0x30, 0xFF, 0xFF, 0xFF, 0xFF]));

        CollectionAssert.AreEqual(Response([0, 0, 0, 0, 0, 0, 0, 0]), written);
    }

    [TestMethod]
    public async Task ServeAsync_TwoPartitionsOfOneTopic_AreEachAnsweredInTheOrderNamed()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var batch = Batch([Record(0, "a")]);
        byte[] fields =
        [
            .. NullString, 0xFF, 0xFF, 0, 0, 0x75, 0x30,
            0, 0, 0, 1, .. String("orders"),
            0, 0, 0, 2,
            .. Int32(1), .. Int32(batch.Length), .. batch,
            .. Int32(0), .. Int32(batch.Length), .. batch,
        ];

        var (written, _) = await ServeAsync(bus, Request(KafkaApiKey.Produce, 3, fields));

        byte[] partitions = [.. Int32(1), 0, 3, .. NoOffset, .. NoOffset, .. Int32(0), 0, 0, .. Int64(0), .. AppendTime];
        CollectionAssert.AreEqual(Response([0, 0, 0, 1, .. String("orders"), 0, 0, 0, 2, .. partitions, 0, 0, 0, 0]), written);
    }

    [TestMethod]
    public async Task ServeAsync_AnIdempotentBatchThenItsExactRetry_AnswersTheSameBaseOffsetAndTheLogHoldsItOnce()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        bus.CreateLog("orders");
        bus.Publish(new BusMessage("orders", new byte[] { 0x7A }), null);
        var batch = IdempotentBatch(5, 0, 0, Record(0, "a"), Record(1, "b"));

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, batch), Produce(5, -1, "orders", 0, batch));

        byte[] retried = [0, 0, 0, 1, .. String("orders"), 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, .. Int64(1), .. NoOffset, .. Int64(0), 0, 0, 0, 0];
        CollectionAssert.AreEqual(new[] { Response(Answer("orders", 0, 0, Int64(1), AppendTime)), Response(retried) }.SelectMany(bytes => bytes).ToArray(), written);
        CollectionAssert.AreEqual(new[] { "z", "a", "b" }, LoggedValues(bus));
    }

    [TestMethod]
    public async Task ServeAsync_ABatchAfterASequenceGap_IsOutOfOrderSequenceNumberAndNothingIsAppended()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, IdempotentBatch(5, 0, 0, Record(0, "a"))), Produce(3, -1, "orders", 0, IdempotentBatch(5, 0, 2, Record(0, "c"))));

        CollectionAssert.AreEqual(new[] { Response(Answer("orders", 0, 0, Int64(0), AppendTime)), Response(Answer("orders", 0, 45, NoOffset, NoOffset)) }.SelectMany(bytes => bytes).ToArray(), written);
        CollectionAssert.AreEqual(new[] { "a" }, LoggedValues(bus));
    }

    [TestMethod]
    public async Task ServeAsync_ABatchOfAnOlderEpoch_IsInvalidProducerEpoch()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, IdempotentBatch(5, 1, 0, Record(0, "a"))), Produce(3, -1, "orders", 0, IdempotentBatch(5, 0, 1, Record(0, "b"))));

        CollectionAssert.AreEqual(new[] { Response(Answer("orders", 0, 0, Int64(0), AppendTime)), Response(Answer("orders", 0, 47, NoOffset, NoOffset)) }.SelectMany(bytes => bytes).ToArray(), written);
        CollectionAssert.AreEqual(new[] { "a" }, LoggedValues(bus));
    }

    [TestMethod]
    public async Task ServeAsync_ANewerEpoch_IsTakenAtAnySequence()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, IdempotentBatch(5, 0, 0, Record(0, "a"))), Produce(3, -1, "orders", 0, IdempotentBatch(5, 1, 9, Record(0, "b"))));

        CollectionAssert.AreEqual(new[] { Response(Answer("orders", 0, 0, Int64(0), AppendTime)), Response(Answer("orders", 0, 0, Int64(1), AppendTime)) }.SelectMany(bytes => bytes).ToArray(), written);
        CollectionAssert.AreEqual(new[] { "a", "b" }, LoggedValues(bus));
    }

    [TestMethod]
    public async Task ServeAsync_AProducerWithNoStateAtSequenceSeven_IsAccepted()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, IdempotentBatch(5, 0, 7, Record(0, "a"))), Produce(3, -1, "orders", 0, IdempotentBatch(5, 0, 8, Record(0, "b"))));

        CollectionAssert.AreEqual(new[] { Response(Answer("orders", 0, 0, Int64(0), AppendTime)), Response(Answer("orders", 0, 0, Int64(1), AppendTime)) }.SelectMany(bytes => bytes).ToArray(), written);
    }

    [TestMethod]
    public async Task ServeAsync_Sequence2147483647ThenZero_AreBothAccepted()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, IdempotentBatch(5, 0, int.MaxValue, Record(0, "a"))), Produce(3, -1, "orders", 0, IdempotentBatch(5, 0, 0, Record(0, "b"))));

        CollectionAssert.AreEqual(new[] { Response(Answer("orders", 0, 0, Int64(0), AppendTime)), Response(Answer("orders", 0, 0, Int64(1), AppendTime)) }.SelectMany(bytes => bytes).ToArray(), written);
        CollectionAssert.AreEqual(new[] { "a", "b" }, LoggedValues(bus));
    }

    [TestMethod]
    public async Task ServeAsync_ProducerIdMinusOne_IsNeverCheckedAndARepeatIsAppendedAgain()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var batch = IdempotentBatch(-1, -1, 0, Record(0, "a"));

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, batch), Produce(3, -1, "orders", 0, batch));

        CollectionAssert.AreEqual(new[] { Response(Answer("orders", 0, 0, Int64(0), AppendTime)), Response(Answer("orders", 0, 0, Int64(1), AppendTime)) }.SelectMany(bytes => bytes).ToArray(), written);
        CollectionAssert.AreEqual(new[] { "a", "a" }, LoggedValues(bus));
    }

    [TestMethod]
    public async Task ServeAsync_TwoBatchesInOnePartitionThenTheirRetry_AreAppendedOnceAndTheRetryAnswersTheFirstBaseOffset()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        byte[] records = [.. IdempotentBatch(5, 0, 0, Record(0, "a"), Record(1, "b")), .. IdempotentBatch(5, 0, 2, Record(0, "c"))];

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, records), Produce(3, -1, "orders", 0, records), Produce(3, -1, "orders", 0, IdempotentBatch(5, 0, 2, Record(0, "c"))));

        byte[] expected =
        [
            .. Response(Answer("orders", 0, 0, Int64(0), AppendTime)),
            .. Response(Answer("orders", 0, 0, Int64(0), NoOffset)),
            .. Response(Answer("orders", 0, 0, Int64(2), NoOffset)),
        ];
        CollectionAssert.AreEqual(expected, written);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, LoggedValues(bus));
    }

    [TestMethod]
    public async Task ServeAsync_ARetriedBatchFollowedByANewOneInOnePartition_IsOutOfOrderSequenceNumberAndNothingIsAppended()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var first = IdempotentBatch(5, 0, 0, Record(0, "a"));
        byte[] retryAndNew = [.. first, .. IdempotentBatch(5, 0, 1, Record(0, "b"))];

        var (written, _) = await ServeAsync(bus, Produce(3, -1, "orders", 0, first), Produce(3, -1, "orders", 0, retryAndNew));

        CollectionAssert.AreEqual(new[] { Response(Answer("orders", 0, 0, Int64(0), AppendTime)), Response(Answer("orders", 0, 45, NoOffset, NoOffset)) }.SelectMany(bytes => bytes).ToArray(), written);
        CollectionAssert.AreEqual(new[] { "a" }, LoggedValues(bus));
    }

    [TestMethod]
    public async Task ServeAsync_ProducerStatesSharedWithInitProducerId_ChecksTheIdItHandedOut()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        var states = new KafkaProducerStates();
        var connection = new InMemoryConnection([Produce(3, -1, "orders", 0, IdempotentBatch(5, 0, 0, Record(0, "a")))]);
        var broker = new KafkaBroker(new UnitTestKafkaAuthenticationPolicy(true, "PLAIN"), [new KafkaProduceAnswerer(bus, states), new KafkaInitProducerIdAnswerer(states)]);

        await broker.ServeAsync(connection, Context(TestContext.CancellationToken));

        Assert.AreEqual(1, states.Count);
    }

    private static string[] LoggedValues(Surl.MessageBus.MessageBus bus) =>
        [.. bus.ReadLog("orders", 0, long.MaxValue).Records.Select(record => Encoding.UTF8.GetString(record.Message.Payload.Span))];

    // A Produce request: transactional_id, acks, timeout_ms 30000, one topic with one partition.
    private static byte[] Produce(short version, short acks, string topic, int partition, byte[]? records, string? transactionalId = null)
    {
        byte[] recordBytes = records is null ? Int32(-1) : [.. Int32(records.Length), .. records];
        byte[] fields =
        [
            .. transactionalId is null ? NullString : String(transactionalId),
            .. Int16(acks),
            0, 0, 0x75, 0x30,
            0, 0, 0, 1, .. String(topic),
            0, 0, 0, 1, .. Int32(partition), .. recordBytes,
        ];
        return Request(KafkaApiKey.Produce, version, fields);
    }

    // A Produce v3 response body: one topic, one partition, then throttle_time_ms 0.
    private static byte[] Answer(string topic, int partition, short error, byte[] baseOffset, byte[] logAppendTime) =>
        [0, 0, 0, 1, .. String(topic), 0, 0, 0, 1, .. Int32(partition), .. Int16(error), .. baseOffset, .. logAppendTime, 0, 0, 0, 0];

    private Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(Surl.MessageBus.MessageBus bus, params byte[][] requests) =>
        ServeAsync(bus, null, requests);

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(Surl.MessageBus.MessageBus bus, ExchangeLimits? limits, params byte[][] requests)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(requests.Select(request => (ReadOnlyMemory<byte>)request));
        var broker = new KafkaBroker(new UnitTestKafkaAuthenticationPolicy(true, "PLAIN"), [new KafkaProduceAnswerer(bus)]);
        var context = Context(TestContext.CancellationToken, log);

        await broker.ServeAsync(connection, limits is null ? context : context with { Limits = limits });

        return (connection.WrittenBytes, log);
    }
}
