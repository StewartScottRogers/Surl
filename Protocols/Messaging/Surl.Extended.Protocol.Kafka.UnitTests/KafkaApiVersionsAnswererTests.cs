using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// ApiVersions v0 to v3 (BL-518; ADR-0105 decision 1), every byte written by hand from the Kafka
/// protocol guide's ApiVersions schemas. The answer above v3 is pinned by
/// <see cref="KafkaBrokerTests.ServeAsync_ApiVersionsAboveItsRange_AnswersVersionZeroWithUnsupportedVersionAndEveryRange"/>.
/// </summary>
[TestClass]
public sealed class KafkaApiVersionsAnswererTests
{
    // The ADR's ranges, one api_key, min_version, max_version row each, in API key order.
    private static readonly byte[] Rows =
    [
        0x00, 0x00, 0x00, 0x03, 0x00, 0x08,
        0x00, 0x01, 0x00, 0x04, 0x00, 0x0B,
        0x00, 0x02, 0x00, 0x01, 0x00, 0x05,
        0x00, 0x03, 0x00, 0x00, 0x00, 0x08,
        0x00, 0x11, 0x00, 0x00, 0x00, 0x01,
        0x00, 0x12, 0x00, 0x00, 0x00, 0x03,
        0x00, 0x13, 0x00, 0x02, 0x00, 0x04,
        0x00, 0x24, 0x00, 0x00, 0x00, 0x01,
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ApiKey_IsApiVersions() =>
        Assert.AreEqual(KafkaApiKey.ApiVersions, new KafkaApiVersionsAnswerer().ApiKey);

    [TestMethod]
    public async Task ServeAsync_ApiVersionsZero_ListsCreateTopicsTwoToFour()
    {
        var (written, _) = await ServeAsync(Refusing(), EmptyBusMetadata(), TestContext.CancellationToken, Request(KafkaApiKey.ApiVersions, 0));

        byte[] createTopicsRow = [0x00, 0x13, 0x00, 0x02, 0x00, 0x04];
        Assert.IsTrue(written.Skip(4 + 4 + 6).Chunk(6).Any(row => row.SequenceEqual(createTopicsRow)));
    }

    [TestMethod]
    public async Task AnswerAsync_NullArguments_Throw()
    {
        var answerer = new KafkaApiVersionsAnswerer();
        var request = new KafkaRequest(new KafkaRequestHeader(18, 0, 1, null), KafkaServedApis.Ranges[5], new KafkaPrimitiveReader(Array.Empty<byte>()), new KafkaConnectionState(null), new InMemoryConnection([]));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(null!, Context(TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await answerer.AnswerAsync(request, null!));
    }

    [TestMethod]
    public async Task ServeAsync_ApiVersionsZero_ListsExactlyTheAdrsRanges()
    {
        var (written, _) = await ServeAsync(Refusing(), EmptyBusMetadata(), TestContext.CancellationToken, Request(KafkaApiKey.ApiVersions, 0));

        CollectionAssert.AreEqual(Response([0x00, 0x00, 0x00, 0x00, 0x00, 0x08, .. Rows]), written);
    }

    [TestMethod]
    [DataRow((short)1)]
    [DataRow((short)2)]
    public async Task ServeAsync_ApiVersionsOneAndTwo_AddThrottleTimeZero(short version)
    {
        var (written, _) = await ServeAsync(Refusing(), EmptyBusMetadata(), TestContext.CancellationToken, Request(KafkaApiKey.ApiVersions, version));

        CollectionAssert.AreEqual(Response([0x00, 0x00, 0x00, 0x00, 0x00, 0x08, .. Rows, 0x00, 0x00, 0x00, 0x00]), written);
    }

    [TestMethod]
    public async Task ServeAsync_ApiVersionsThree_AnswersTheCompactFormUnderResponseHeaderZero()
    {
        // Request header v2: api_key 18, version 3, correlation id 7, client id "c", no tagged
        // fields; then client_software_name "kc", client_software_version "1", no tagged fields.
        byte[] request = [0, 0, 0, 0x12, 0x00, 0x12, 0x00, 0x03, 0, 0, 0, 7, 0x00, 0x01, 0x63, 0x00, 0x03, 0x6B, 0x63, 0x02, 0x31, 0x00];
        byte[] compactRows = [.. Rows.Chunk(6).SelectMany(row => row.Append((byte)0x00))];

        var (written, _) = await ServeAsync(Refusing(), EmptyBusMetadata(), TestContext.CancellationToken, request);

        CollectionAssert.AreEqual(Response([0x00, 0x00, 0x09, .. compactRows, 0x00, 0x00, 0x00, 0x00, 0x00]), written);
    }

    [TestMethod]
    public async Task ServeAsync_ApiVersionsBeforeALogin_IsAnswered()
    {
        var policy = Refusing();

        var (written, log) = await ServeAsync(policy, EmptyBusMetadata(), TestContext.CancellationToken, Request(KafkaApiKey.ApiVersions, 0), Request(KafkaApiKey.ApiVersions, 0));

        Assert.HasCount(2 * (4 + 4 + 6 + Rows.Length), written);
        Assert.AreEqual(1, policy.AnonymousChecks);
        Assert.IsEmpty(log.Notes);
    }

    private static UnitTestKafkaAuthenticationPolicy Refusing() => new(false, "PLAIN");
}
