using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaRecordBatches;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// ListOffsets v1 to v5 from the shared bus's topic logs (BL-520; ADR-0105 decision 3), every
/// request and response byte written by hand from the Kafka protocol guide's ListOffsets schemas.
/// </summary>
[TestClass]
public sealed class KafkaListOffsetsAnswererTests
{
    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private long Now => clock.GetUtcNow().ToUnixTimeMilliseconds();

    [TestMethod]
    public void Constructor_NullBus_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaListOffsetsAnswerer(null!));

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var answerer = new KafkaListOffsetsAnswerer(new Surl.MessageBus.MessageBus(clock));
        var request = new KafkaRequest(new KafkaRequestHeader(2, 1, 1, null), KafkaServedApis.Find(2)!, new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null));

        Assert.AreEqual(KafkaApiKey.ListOffsets, answerer.ApiKey);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    [DataRow((short)1)]
    [DataRow((short)2)]
    [DataRow((short)4)]
    [DataRow((short)5)]
    public async Task ServeAsync_EarliestAndLatest_AreTheLogStartAndEndOffsets(short version)
    {
        var bus = new Surl.MessageBus.MessageBus(clock, maxLogBytesPerTopic: 2 * (MessageBus.MessageBus.LogRecordOverheadBytes + 1));
        bus.Publish(new BusMessage("orders", "a"u8.ToArray()));
        bus.Publish(new BusMessage("orders", "b"u8.ToArray()));
        bus.Publish(new BusMessage("orders", "c"u8.ToArray()));

        var (written, _) = await ServeAsync(bus, ListOffsets(version, "orders", 0, -2), ListOffsets(version, "orders", 0, -1));

        byte[] expected = [.. Response(Answer(version, "orders", 0, 0, -1, 1, 0)), .. Response(Answer(version, "orders", 0, 0, -1, 3, 0))];
        CollectionAssert.AreEqual(expected, written);
    }

    [TestMethod]
    public async Task ServeAsync_ATimestampBelowMinusTwo_IsAnsweredAsEarliest()
    {
        var bus = PublishedTenMillisecondsApart();

        var (written, _) = await ServeAsync(bus, ListOffsets(1, "orders", 0, -7));

        CollectionAssert.AreEqual(Response(Answer(1, "orders", 0, 0, -1, 0, 0)), written);
    }

    [TestMethod]
    public async Task ServeAsync_ATimestamp_IsTheFirstRecordPublishedAtOrAfterIt()
    {
        var bus = PublishedTenMillisecondsApart();

        var (written, _) = await ServeAsync(bus, ListOffsets(1, "orders", 0, Now - 5), ListOffsets(1, "orders", 0, Now + 1));

        byte[] expected = [.. Response(Answer(1, "orders", 0, 0, Now, 1, 0)), .. Response(Answer(1, "orders", 0, 0, -1, -1, 0))];
        CollectionAssert.AreEqual(expected, written);
    }

    [TestMethod]
    [DataRow("no way", 0, (short)17)]
    [DataRow("orders", 1, (short)3)]
    [DataRow("unknown", 0, (short)3)]
    public async Task ServeAsync_ATopicOrPartitionThatCannotBeRead_IsAnsweredWithItsError(string topic, int partition, short error)
    {
        var bus = PublishedTenMillisecondsApart();

        var (written, _) = await ServeAsync(bus, ListOffsets(4, topic, partition, -1));

        CollectionAssert.AreEqual(Response(Answer(4, topic, partition, error, -1, -1, -1)), written);
    }

    [TestMethod]
    public async Task ServeAsync_ATopicTheCallerMayNotSubscribeTo_IsTopicAuthorizationFailed()
    {
        var bus = new Surl.MessageBus.MessageBus(clock, configuration: new MessageBusConfiguration
        {
            AccessGrants = [new TopicAccessGrant("orders", TopicAccessGrant.Everyone, TopicAccessRights.Publish)],
        });
        bus.Publish(new BusMessage("orders", "a"u8.ToArray()));

        var (written, _) = await ServeAsync(bus, ListOffsets(1, "orders", 0, -1));

        CollectionAssert.AreEqual(Response(Answer(1, "orders", 0, 29, -1, -1, 0)), written);
    }

    // Two messages on "orders": the first ten milliseconds before now, the second now.
    private Surl.MessageBus.MessageBus PublishedTenMillisecondsApart()
    {
        var bus = new Surl.MessageBus.MessageBus(clock);
        bus.Publish(new BusMessage("orders", "a"u8.ToArray()));
        clock.Advance(TimeSpan.FromMilliseconds(10));
        bus.Publish(new BusMessage("orders", "b"u8.ToArray()));
        return bus;
    }

    // A ListOffsets request: replica_id -1, (v2) isolation_level 0, one topic with one partition -
    // (v4) current_leader_epoch 0, timestamp.
    private static byte[] ListOffsets(short version, string topic, int partition, long timestamp)
    {
        byte[] fields =
        [
            .. Int32(-1),
            .. version >= 2 ? [0] : Array.Empty<byte>(),
            .. Int32(1), .. String(topic), .. Int32(1), .. Int32(partition),
            .. version >= 4 ? Int32(0) : [],
            .. Int64(timestamp),
        ];
        return Request(KafkaApiKey.ListOffsets, version, fields);
    }

    // A ListOffsets response body: (v2) throttle_time_ms 0, one topic with one partition -
    // error_code, timestamp, offset, (v4) leader_epoch.
    private static byte[] Answer(short version, string topic, int partition, short error, long timestamp, long offset, int leaderEpoch) =>
    [
        .. version >= 2 ? Int32(0) : Array.Empty<byte>(),
        .. Int32(1), .. String(topic), .. Int32(1), .. Int32(partition), .. Int16(error),
        .. Int64(timestamp), .. Int64(offset),
        .. version >= 4 ? Int32(leaderEpoch) : [],
    ];

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(Surl.MessageBus.MessageBus bus, params byte[][] requests)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(requests.Select(request => (ReadOnlyMemory<byte>)request));
        var broker = new KafkaBroker(new UnitTestKafkaAuthenticationPolicy(true, "PLAIN"), [new KafkaListOffsetsAnswerer(bus)]);

        await broker.ServeAsync(connection, Context(TestContext.CancellationToken, log, timeProvider: clock));

        return (connection.WrittenBytes, log);
    }
}
