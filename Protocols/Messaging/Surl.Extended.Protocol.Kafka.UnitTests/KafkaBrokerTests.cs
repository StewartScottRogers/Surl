using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// The broker's framing, headers and answers to requests outside the served ranges (BL-517;
/// ADR-0105 decisions 1 and 7), every byte written by hand from the Kafka protocol guide.
/// </summary>
[TestClass]
public sealed class KafkaBrokerTests
{
    // Request header v1 for Metadata v4, correlation id 0x01020304, client id "c1"; then one byte
    // standing for the request's own fields.
    private static readonly byte[] MetadataV4 =
    [
        0x00, 0x00, 0x00, 0x0D,
        0x00, 0x03, 0x00, 0x04, 0x01, 0x02, 0x03, 0x04, 0x00, 0x02, 0x63, 0x31, 0x2A,
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullAnswerers_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaBroker(null!));

    [TestMethod]
    public void Constructor_TwoAnswerersForOneApi_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => new KafkaBroker([new UnitTestEchoAnswerer(KafkaApiKey.Metadata), new UnitTestEchoAnswerer(KafkaApiKey.Metadata)]));

    [TestMethod]
    public void Schemes_AreKafkaAndKafkas() =>
        CollectionAssert.AreEqual(new[] { "kafka", "kafkas" }, new KafkaBroker([]).Schemes.ToArray());

    [TestMethod]
    public void ServeAsync_NullArguments_Throw()
    {
        var broker = new KafkaBroker([]);
        var connection = new InMemoryConnection([]);

        Assert.ThrowsExactly<ArgumentNullException>(() => broker.ServeAsync(null!, Context()));
        Assert.ThrowsExactly<ArgumentNullException>(() => broker.ServeAsync(connection, null!));
    }

    [TestMethod]
    public async Task ServeAsync_ServedRequest_EchoesTheCorrelationIdUnderResponseHeaderZero()
    {
        var answerer = new UnitTestEchoAnswerer(KafkaApiKey.Metadata);
        var connection = new InMemoryConnection([MetadataV4]);

        await new KafkaBroker([answerer]).ServeAsync(connection, Context());

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 5, 0x01, 0x02, 0x03, 0x04, 0x2A }, connection.WrittenBytes);
        var request = answerer.Requests.Single();
        Assert.AreEqual(new KafkaRequestHeader(3, 4, 0x01020304, "c1"), request.Header);
        Assert.AreEqual(KafkaApiKey.Metadata, request.Range.ApiKey);
    }

    [TestMethod]
    public async Task ServeAsync_RequestSplitAcrossReads_IsReassembledAndAnswered()
    {
        var connection = new InMemoryConnection(MetadataV4.Select(value => (ReadOnlyMemory<byte>)new[] { value }));

        await new KafkaBroker([new UnitTestEchoAnswerer(KafkaApiKey.Metadata)]).ServeAsync(connection, Context());

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 5, 0x01, 0x02, 0x03, 0x04, 0x2A }, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_TwoRequests_AreAnsweredInOrder()
    {
        byte[] second = [.. MetadataV4];
        second[11] = 0x05;
        second[16] = 0x2B;
        var connection = new InMemoryConnection([MetadataV4, second]);

        await new KafkaBroker([new UnitTestEchoAnswerer(KafkaApiKey.Metadata)]).ServeAsync(connection, Context());

        CollectionAssert.AreEqual(
            new byte[] { 0, 0, 0, 5, 0x01, 0x02, 0x03, 0x04, 0x2A, 0, 0, 0, 5, 0x01, 0x02, 0x03, 0x05, 0x2B },
            connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_FlexibleApiVersionsThree_ReadsHeaderTwoAndAnswersUnderHeaderZero()
    {
        // ApiVersions v3, correlation id 9, client id "c", one tagged field (tag 0, one byte), then 0x2A.
        byte[] request = [0, 0, 0, 0x10, 0x00, 0x12, 0x00, 0x03, 0, 0, 0, 9, 0x00, 0x01, 0x63, 0x01, 0x00, 0x01, 0xEE, 0x2A];
        var answerer = new UnitTestEchoAnswerer(KafkaApiKey.ApiVersions);
        var connection = new InMemoryConnection([request]);

        await new KafkaBroker([answerer]).ServeAsync(connection, Context());

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 5, 0, 0, 0, 9, 0x2A }, connection.WrittenBytes);
        Assert.AreEqual("c", answerer.Requests.Single().Header.ClientId);
    }

    [TestMethod]
    public async Task ServeAsync_NoResponseAnswer_WritesNothingAndReadsTheNextRequest()
    {
        var answerer = new UnitTestEchoAnswerer(KafkaApiKey.Metadata) { Answer = KafkaAnswer.NoResponse };
        var connection = new InMemoryConnection([MetadataV4, MetadataV4]);

        await new KafkaBroker([answerer]).ServeAsync(connection, Context());

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.HasCount(2, answerer.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_CloseAnswer_ClosesWithNoBytesAndNotesWhy()
    {
        var log = new RecordingExchangeLog();
        var answerer = new UnitTestEchoAnswerer(KafkaApiKey.Metadata) { Answer = KafkaAnswer.Close("Refused") };
        var connection = new InMemoryConnection([MetadataV4, MetadataV4]);

        await new KafkaBroker([answerer]).ServeAsync(connection, Context(log: log));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.HasCount(1, answerer.Requests);
        Assert.AreEqual("Refused; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_AnswererFindsTheRequestMalformed_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();
        var answerer = new UnitTestEchoAnswerer(KafkaApiKey.Metadata) { ReadsAnInt64 = true };
        var connection = new InMemoryConnection([MetadataV4]);

        await new KafkaBroker([answerer]).ServeAsync(connection, Context(log: log));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("A request could not be read (A field needs 8 bytes and 1 are left.); closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_UnknownApiKey_ClosesWithNoBytes()
    {
        // api_key 11 (JoinGroup, not served), version 0, correlation id 1, null client id.
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([new byte[] { 0, 0, 0, 0x0A, 0x00, 0x0B, 0x00, 0x00, 0, 0, 0, 1, 0xFF, 0xFF }]);

        await new KafkaBroker([new UnitTestEchoAnswerer(KafkaApiKey.Metadata)]).ServeAsync(connection, Context(log: log));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("API key 11 is not served; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    [DataRow((byte)0x02)]
    [DataRow((byte)0x09)]
    public async Task ServeAsync_ServedApiAtAVersionOutsideItsRange_ClosesWithNoBytes(byte version)
    {
        // Produce at the given version, correlation id 1, null client id.
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([new byte[] { 0, 0, 0, 0x0A, 0x00, 0x00, 0x00, version, 0, 0, 0, 1, 0xFF, 0xFF }]);

        await new KafkaBroker([new UnitTestEchoAnswerer(KafkaApiKey.Produce)]).ServeAsync(connection, Context(log: log));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual($"Produce version {version} is not served (3 to 8 are); closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_ApiVersionsAboveItsRange_AnswersVersionZeroWithUnsupportedVersionAndEveryRange()
    {
        // ApiVersions v4 (flexible: header v2), correlation id 0x0A0B0C0D, client id "c", no tagged fields.
        byte[] request = [0, 0, 0, 0x0C, 0x00, 0x12, 0x00, 0x04, 0x0A, 0x0B, 0x0C, 0x0D, 0x00, 0x01, 0x63, 0x00];
        byte[] expected =
        [
            0x00, 0x00, 0x00, 0x34,
            0x0A, 0x0B, 0x0C, 0x0D,
            0x00, 0x23,
            0x00, 0x00, 0x00, 0x07,
            0x00, 0x00, 0x00, 0x03, 0x00, 0x08,
            0x00, 0x01, 0x00, 0x04, 0x00, 0x0B,
            0x00, 0x02, 0x00, 0x01, 0x00, 0x05,
            0x00, 0x03, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x11, 0x00, 0x00, 0x00, 0x01,
            0x00, 0x12, 0x00, 0x00, 0x00, 0x03,
            0x00, 0x24, 0x00, 0x00, 0x00, 0x01,
        ];
        var connection = new InMemoryConnection([request]);

        await new KafkaBroker([]).ServeAsync(connection, Context());

        CollectionAssert.AreEqual(expected, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_ServedApiWithNoAnswerer_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([MetadataV4]);

        await new KafkaBroker([]).ServeAsync(connection, Context(log: log));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("Metadata is not answered yet; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_RequestShorterThanItsHeader_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([new byte[] { 0, 0, 0, 2, 0x00, 0x03 }]);

        await new KafkaBroker([]).ServeAsync(connection, Context(log: log));

        Assert.IsEmpty(connection.WrittenBytes);
        StringAssert.StartsWith(log.Notes.Single(), "A request could not be read (");
    }

    [TestMethod]
    public async Task ServeAsync_SizePrefixOverTheRequestLimit_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 12 };
        var connection = new InMemoryConnection([MetadataV4]);

        await new KafkaBroker([new UnitTestEchoAnswerer(KafkaApiKey.Metadata)]).ServeAsync(connection, Context(limits: limits, log: log));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("A request's size prefix was 13, outside 1 to the request limit 12; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_ClosedPartOfTheWayThroughARequest_NotesIt()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([MetadataV4.AsMemory(0, 6)]);

        await new KafkaBroker([]).ServeAsync(connection, Context(log: log));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("The client closed the connection part of the way through a request.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_ClosedBeforeAnyRequest_EndsQuietly()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([]);

        await new KafkaBroker([]).ServeAsync(connection, Context(log: log));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_FirstRequestPastTheHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([MetadataV4.AsMemory(0, 6)], peerHalfClosesWhenExhausted: false);

        var serving = new KafkaBroker([]).ServeAsync(connection, Context(clock, log: log));
        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("The first request was not complete within the head timeout; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_InfiniteHeadTimeout_WaitsUntilTheExchangeIsCancelled()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var limits = ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan };
        var connection = new InMemoryConnection([MetadataV4.AsMemory(0, 6)], peerHalfClosesWhenExhausted: false);

        var serving = new KafkaBroker([]).ServeAsync(connection, Context(clock, limits, cancellationToken: idleTimeout.Token));
        clock.Advance(TimeSpan.FromDays(1));
        Assert.IsFalse(serving.IsCompleted);
        await idleTimeout.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledBeforeTheFirstRequest_IsNotTakenForAHeadTimeout()
    {
        var log = new RecordingExchangeLog();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new InMemoryConnection([MetadataV4.AsMemory(0, 6)], peerHalfClosesWhenExhausted: false);

        var serving = new KafkaBroker([]).ServeAsync(connection, Context(log: log, cancellationToken: idleTimeout.Token));
        await idleTimeout.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsEmpty(log.Notes);
    }

    private ExchangeContext Context(
        TimeProvider? timeProvider = null,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null,
        CancellationToken? cancellationToken = null) => new(
            1,
            new ListenUrl("kafka", "127.0.0.1", 9092).WithBoundPort(9092),
            new IPEndPoint(IPAddress.Loopback, 9092),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider ?? new ManualTimeProvider(),
            cancellationToken ?? TestContext.CancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };

    // Answers one API by echoing the request's remaining field bytes, or as told.
    private sealed class UnitTestEchoAnswerer(KafkaApiKey apiKey) : IKafkaApiAnswerer
    {
        public KafkaApiKey ApiKey { get; } = apiKey;

        public KafkaAnswer? Answer { get; init; }

        public bool ReadsAnInt64 { get; init; }

        public List<KafkaRequest> Requests { get; } = [];

        public ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
        {
            Requests.Add(request);
            if (ReadsAnInt64)
            {
                request.Fields.ReadInt64();
            }

            var rest = new byte[request.Fields.Remaining];
            for (var index = 0; index < rest.Length; index++)
            {
                rest[index] = (byte)request.Fields.ReadInt8();
            }

            return ValueTask.FromResult(Answer ?? KafkaAnswer.Respond(rest));
        }
    }
}
