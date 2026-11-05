namespace Surl.MessageBus;

[TestClass]
public sealed class MessageBusLogTests
{
    // A record of a one-byte payload with no key and no headers counts 17 bytes.
    private const long OneByteRecord = MessageBus.LogRecordOverheadBytes + 1;

    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void Publish_OnATopic_AppendsToItsLogAtTheNextOffset()
    {
        var bus = new MessageBus(clock);

        var first = bus.Publish(Message("t", "a"));
        bus.Publish(Message("other", "x"));
        var second = bus.Publish(Message("t", "b"));

        Assert.AreEqual(0L, first.LogOffset);
        Assert.AreEqual(1L, second.LogOffset);
        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 0, 2), bus.GetLogOffsets("t"));
        CollectionAssert.AreEqual(new[] { "other", "t" }, bus.GetLogTopics().ToArray());
    }

    [TestMethod]
    public void Publish_Retained_IsLoggedToo_AndAnEmptyRetainedPayloadIsLoggedAsARecord()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Message("t", "a") with { Retain = true });
        bus.Publish(new BusMessage("t", ReadOnlyMemory<byte>.Empty) { Retain = true });

        var read = bus.ReadLog("t", 0, long.MaxValue);

        CollectionAssert.AreEqual(new[] { 1, 0 }, read.Records.Select(record => record.Message.Payload.Length).ToArray());
    }

    [TestMethod]
    public void ReadLog_FromAnOffset_ReturnsTheRecordsFromThereInOrderWithTimestampKeyAndHeaders()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Message("t", "a"));
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        bus.Publish(Message("t", "b") with { Key = "k"u8.ToArray(), Headers = [new("h", "v")] });
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        bus.Publish(Message("t", "c"));

        var read = bus.ReadLog("t", 1, long.MaxValue);

        Assert.AreEqual(MessageBusOutcome.Succeeded, read.Outcome);
        Assert.AreEqual(0L, read.LogStartOffset);
        Assert.AreEqual(3L, read.HighWatermark);
        CollectionAssert.AreEqual(new[] { 1L, 2L }, read.Records.Select(record => record.Offset).ToArray());
        CollectionAssert.AreEqual(new[] { "b", "c" }, read.Records.Select(record => Text(record.Message)).ToArray());
        var keyed = read.Records[0].Message;
        Assert.AreEqual(new DateTimeOffset(2026, 11, 4, 8, 0, 1, TimeSpan.Zero), keyed.PublishedAt);
        CollectionAssert.AreEqual("k"u8.ToArray(), keyed.Key!.Value.ToArray());
        CollectionAssert.AreEqual(new[] { new BusMessageHeader("h", "v") }, keyed.Headers.ToArray());
    }

    [TestMethod]
    public void ReadLog_AtTheHighWatermark_ReturnsNoRecords()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Message("t", "a"));

        var read = bus.ReadLog("t", 1, long.MaxValue);

        Assert.AreEqual(MessageBusOutcome.Succeeded, read.Outcome);
        Assert.IsEmpty(read.Records);
        Assert.AreEqual(1L, read.HighWatermark);
    }

    [TestMethod]
    public void ReadLog_ATopicNothingWasPublishedOn_IsAnEmptyLogAtOffsetZero()
    {
        var bus = new MessageBus(clock);

        Assert.AreEqual(new BusLogReadResult(MessageBusOutcome.Succeeded, 0, 0, []).Outcome, bus.ReadLog("t", 0, 0).Outcome);
        Assert.IsEmpty(bus.ReadLog("t", 0, 0).Records);
        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 0, 0), bus.GetLogOffsets("t"));
        Assert.IsEmpty(bus.GetLogTopics());
    }

    [TestMethod]
    public void ReadLog_PastTheHighWatermark_ReturnsOffsetOutOfRangeAndNoRecords()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Message("t", "a"));

        var read = bus.ReadLog("t", 2, long.MaxValue);

        Assert.AreEqual(MessageBusOutcome.OffsetOutOfRange, read.Outcome);
        Assert.IsEmpty(read.Records);
        Assert.AreEqual(0L, read.LogStartOffset);
        Assert.AreEqual(1L, read.HighWatermark);
    }

    [TestMethod]
    public void ReadLog_MaxBytes_StopsBeforeTheRecordThatWouldPassIt_ButAlwaysReadsTheFirst()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Message("t", "a"));
        bus.Publish(Message("t", "b"));
        bus.Publish(Message("t", "c"));

        var none = bus.ReadLog("t", 0, 0);
        var two = bus.ReadLog("t", 0, (2 * OneByteRecord) + 1);

        Assert.HasCount(1, none.Records);
        Assert.HasCount(2, two.Records);
    }

    [TestMethod]
    public void Publish_PastMaxLogBytesPerTopic_DropsTheOldestAndRaisesTheLogStartOffset()
    {
        var bus = new MessageBus(clock, maxLogBytesPerTopic: 2 * OneByteRecord);
        bus.Publish(Message("t", "a"));
        bus.Publish(Message("t", "b"));

        var result = bus.Publish(Message("t", "c"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.AreEqual(2L, result.LogOffset);
        var read = bus.ReadLog("t", 1, long.MaxValue);
        Assert.AreEqual(1L, read.LogStartOffset);
        Assert.AreEqual(3L, read.HighWatermark);
        CollectionAssert.AreEqual(new[] { "b", "c" }, read.Records.Select(record => Text(record.Message)).ToArray());
    }

    [TestMethod]
    public void Publish_ManyPastMaxLogBytesPerTopic_KeepsOffsetsContiguousAcrossCompaction()
    {
        var bus = new MessageBus(clock, maxLogBytesPerTopic: 2 * OneByteRecord);
        for (var index = 0; index < 10; index++)
        {
            bus.Publish(Message("t", index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var read = bus.ReadLog("t", 8, long.MaxValue);

        Assert.AreEqual(8L, read.LogStartOffset);
        CollectionAssert.AreEqual(new[] { 8L, 9L }, read.Records.Select(record => record.Offset).ToArray());
        CollectionAssert.AreEqual(new[] { "8", "9" }, read.Records.Select(record => Text(record.Message)).ToArray());
    }

    [TestMethod]
    public void Publish_ARecordLargerThanMaxLogBytesPerTopic_DropsEveryOlderOneAndKeepsIt()
    {
        var bus = new MessageBus(clock, maxLogBytesPerTopic: OneByteRecord);
        bus.Publish(Message("t", "a"));

        bus.Publish(Message("t", "large"));

        var read = bus.ReadLog("t", 1, long.MaxValue);
        Assert.AreEqual(1L, read.LogStartOffset);
        CollectionAssert.AreEqual(new[] { "large" }, read.Records.Select(record => Text(record.Message)).ToArray());
    }

    [TestMethod]
    public void ReadLog_BelowTheLogStartOffset_ReturnsOffsetOutOfRangeAndNoRecords()
    {
        var bus = new MessageBus(clock, maxLogBytesPerTopic: OneByteRecord);
        bus.Publish(Message("t", "a"));
        bus.Publish(Message("t", "b"));

        var read = bus.ReadLog("t", 0, long.MaxValue);

        Assert.AreEqual(MessageBusOutcome.OffsetOutOfRange, read.Outcome);
        Assert.IsEmpty(read.Records);
        Assert.AreEqual(1L, read.LogStartOffset);
        Assert.AreEqual(2L, read.HighWatermark);
    }

    [TestMethod]
    public void Publish_PastMaxLogBytes_ReturnsStoreFullAndStoresAndDeliversNothing()
    {
        var bus = new MessageBus(clock, maxLogBytes: 2 * OneByteRecord);
        bus.Publish(Message("a", "1"));
        bus.Publish(Message("b", "2"));
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("#", received.Add);

        var onANewTopic = bus.Publish(Message("c", "3"));
        var onAnOldTopic = bus.Publish(Message("a", "4") with { Retain = true });

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.StoreFull, 0), onANewTopic);
        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.StoreFull, 0), onAnOldTopic);
        Assert.IsNull(onANewTopic.LogOffset);
        Assert.IsEmpty(received);
        Assert.AreEqual(1L, bus.GetLogOffsets("a").HighWatermark);
        CollectionAssert.AreEqual(new[] { "a", "b" }, bus.GetLogTopics().ToArray());
    }

    [TestMethod]
    public void Publish_RetentionMakingRoomUnderMaxLogBytes_IsTaken()
    {
        var bus = new MessageBus(clock, maxLogBytesPerTopic: OneByteRecord, maxLogBytes: 2 * OneByteRecord);
        bus.Publish(Message("a", "1"));
        bus.Publish(Message("b", "2"));

        var result = bus.Publish(Message("a", "3"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 1, 2), bus.GetLogOffsets("a"));
    }

    [TestMethod]
    public void Publish_AKafkaStyleRecord_IsDeliveredToLiveSubscribersAndReadableFromTheLog()
    {
        var bus = new MessageBus(clock);
        using var mqtt = bus.Connect();
        var received = new List<BusMessage>();
        mqtt.Subscribe("orders/new", received.Add);
        var topic = KafkaTopicNames.ToCanonicalTopic("orders.new").Name!;
        var record = new BusMessage(topic, "hi"u8.ToArray())
        {
            QualityOfService = BusQualityOfService.AtLeastOnce,
            Key = "k"u8.ToArray(),
            Headers = [new("h", "v")],
        };

        var produced = bus.Publish(record);

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, 1) { LogOffset = 0 }, produced);
        Assert.AreEqual("hi", Text(received.Single()));
        Assert.AreEqual("hi", Text(bus.ReadLog(topic, 0, long.MaxValue).Records.Single().Message));
    }

    [TestMethod]
    public void Publish_AlreadyExpired_IsNotLogged()
    {
        var bus = new MessageBus(clock);

        var result = bus.Publish(Message("t", "a") with { ExpiresAt = clock.UtcNow });

        Assert.IsNull(result.LogOffset);
        Assert.IsEmpty(bus.GetLogTopics());
    }

    [TestMethod]
    public void ReadLog_AndGetLogOffsets_OnANonCanonicalTopic_ReturnInvalidTopic()
    {
        var bus = new MessageBus(clock);

        Assert.AreEqual(MessageBusOutcome.InvalidTopic, bus.ReadLog("a/#", 0, 0).Outcome);
        Assert.IsEmpty(bus.ReadLog("a/#", 0, 0).Records);
        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.InvalidTopic, 0, 0), bus.GetLogOffsets("a/+"));
    }

    [TestMethod]
    public void LogRecordBytes_CountsPayloadKeyHeadersAndTheOverhead()
    {
        var message = new BusMessage("t", "abc"u8.ToArray()) { Key = "kk"u8.ToArray(), Headers = [new("n", "é")] };

        Assert.AreEqual(MessageBus.LogRecordOverheadBytes + 3 + 2 + 1 + 2, MessageBus.LogRecordBytes(message));
    }

    [TestMethod]
    public void ArgumentChecks_Throw()
    {
        var bus = new MessageBus(clock);

        Assert.ThrowsExactly<ArgumentNullException>(() => bus.ReadLog(null!, 0, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => bus.ReadLog("t", 0, -1));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.GetLogOffsets(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => MessageBus.LogRecordBytes(null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MessageBus(clock, maxLogBytesPerTopic: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MessageBus(clock, maxLogBytes: 0));
    }

    [TestMethod]
    public void CreateLog_NewTopic_MakesItsEmptyLogAndListsIt()
    {
        var bus = new MessageBus(clock);

        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.CreateLog("a/b"));

        CollectionAssert.AreEqual(new[] { "a/b" }, bus.GetLogTopics().ToArray());
        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 0, 0), bus.GetLogOffsets("a/b"));
    }

    [TestMethod]
    public void CreateLog_TopicWithALog_LeavesItsRecords()
    {
        var bus = new MessageBus(clock, maxTopics: 1);
        bus.Publish(Message("t", "a"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.CreateLog("t"));

        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 0, 1), bus.GetLogOffsets("t"));
    }

    [TestMethod]
    public void CreateLog_PastMaxTopics_IsTooManyTopicsAndMakesNothing()
    {
        var bus = new MessageBus(clock, maxTopics: 1);
        bus.CreateLog("a");

        Assert.AreEqual(MessageBusOutcome.TooManyTopics, bus.CreateLog("b"));

        CollectionAssert.AreEqual(new[] { "a" }, bus.GetLogTopics().ToArray());
    }

    [TestMethod]
    public void CreateLog_NotCanonical_IsInvalidTopic()
    {
        var bus = new MessageBus(clock);

        Assert.AreEqual(MessageBusOutcome.InvalidTopic, bus.CreateLog("a/+"));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.CreateLog(null!));
        Assert.IsEmpty(bus.GetLogTopics());
    }

    [TestMethod]
    public void Constructor_Defaults_AreTheAdrsBounds()
    {
        var bus = new MessageBus(clock);

        Assert.AreEqual(16_777_216L, bus.MaxLogBytesPerTopic);
        Assert.AreEqual(268_435_456L, bus.MaxLogBytes);
    }

    private static BusMessage Message(string topic, string payload) =>
        new(topic, System.Text.Encoding.UTF8.GetBytes(payload));

    private static string Text(BusMessage message) => System.Text.Encoding.UTF8.GetString(message.Payload.Span);
}
