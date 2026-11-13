using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>kafka</c> client against a live <c>surl kafka://</c> and <c>kafkas://</c> (BL-523,
/// ADR-0083 decisions 1 to 3): produce a record after a SASL <c>PLAIN</c> login, consume it from offset 0, consume
/// it again from a broker serving compressed batches, and a refused bad password. Each expected value comes from the
/// Kafka protocol guide as <c>Documentation/Conformance/Extended/Kafka.md</c> lists it - a produced record is
/// appended at the next offset, a Fetch from offset 0 returns the log's records in order, a compressed batch decodes
/// to the same records, a refused login is <c>SASL_AUTHENTICATION_FAILED</c> - never from what the port printed beyond
/// the records' values. The produce and the consume are both the port's, so one proves the other; the log is the one
/// <c>surl</c> keeps, which the plain <c>kafka</c> and the TLS <c>kafkas</c> listeners share. Inconclusive when the
/// pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientExchangesRecordsWithSurlOverKafkaTests
{
    private const string Login = "alice:secret";
    private const string Topic = "orders";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Kafka:Guide-Produce-2, Kafka:Guide-Produce-7, Kafka:Guide-Sasl-3, Kafka:Guide-Fetch-2, Kafka:Guide-Tls-1: a record produced after a PLAIN login is fetched from offset 0, value exact")]
    [DataRow("kafka")]
    [DataRow("kafkas")]
    public async Task Produce_ThenConsumeFromOffsetZero_PrintsTheRecordValue(string scheme)
    {
        await using var surl = await StartAsync();

        var produced = await ClientAsync(surl, scheme, Login, $"/{Topic}", "-d", "first record");
        AssertOutput(string.Empty, produced, surl);
        var consumed = await ClientAsync(surl, scheme, Login, $"/{Topic}/0?offset=0");

        AssertOutput("first record", consumed, surl);
    }

    [TestMethod]
    [Description("Kafka:Guide-Produce-2, Kafka:Guide-Fetch-3, Kafka:Guide-Fetch-4: records are appended in order, and a Fetch from offset 1 starts at the second")]
    public async Task Produce_TwoRecords_AreConsumedInOrderAndFromTheOffsetAsked()
    {
        await using var surl = await StartAsync();
        AssertOutput(string.Empty, await ClientAsync(surl, "kafka", Login, $"/{Topic}", "-d", "one"), surl);
        AssertOutput(string.Empty, await ClientAsync(surl, "kafka", Login, $"/{Topic}", "-d", "two"), surl);

        var all = await ClientAsync(surl, "kafka", Login, $"/{Topic}?offset=0");
        var fromOne = await ClientAsync(surl, "kafka", Login, $"/{Topic}?offset=1");

        AssertOutput("one\ntwo", all, surl);
        AssertOutput("two", fromOne, surl);
    }

    [TestMethod]
    [Description("Kafka:Batch-Compression-1, Kafka:Surl-Compression-2: a broker serving batches compressed with each codec still gives the client the record value")]
    [DataRow("gzip")]
    [DataRow("snappy")]
    [DataRow("lz4")]
    [DataRow("zstd")]
    public async Task Consume_FromABrokerServingCompressedBatches_PrintsTheRecordValue(string codec)
    {
        await using var surl = await StartAsync("--kafka-fetch-compression", codec);
        var payload = string.Concat(Enumerable.Repeat("compress me ", 40)).TrimEnd();
        AssertOutput(string.Empty, await ClientAsync(surl, "kafka", Login, $"/{Topic}", "-d", payload), surl);

        var consumed = await ClientAsync(surl, "kafka", Login, $"/{Topic}/0?offset=0");

        AssertOutput(payload, consumed, surl);
    }

    [TestMethod]
    [Description("Kafka:Guide-Sasl-5: a wrong password is refused with SASL_AUTHENTICATION_FAILED, which the client reports as a login failure (exit 67)")]
    [DataRow("kafka")]
    [DataRow("kafkas")]
    public async Task Login_WithAWrongPassword_IsRefused(string scheme)
    {
        await using var surl = await StartAsync();

        var result = await ClientAsync(surl, scheme, "alice:wrong", $"/{Topic}", "-d", "never");

        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(67, result.ExitCode, diagnostics);
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }

    private Task<SurlOnLoopback> StartAsync(params string[] extraOptions) =>
        SurlOnLoopback.StartInMemoryAsync(
            ["kafka", "kafkas"],
            // The port asks Metadata v4 without allow_auto_topic_creation, so the topic is declared (ADR-0105 decision 2).
            ["--user", Login, "--allow-plaintext-auth", "--self-signed", "--topic", Topic, .. extraOptions],
            TestContext.CancellationToken);

    private Task<CurlPortClientRunResult> ClientAsync(SurlOnLoopback surl, string scheme, string login, string pathAndQuery, params string[] options) =>
        PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, ["-sS", "-k", "-m", "30", .. options, $"{scheme}://{login}@127.0.0.1:{surl.BaseUrlOf(scheme).Port}{pathAndQuery}"]);

    private static void AssertOutput(string expected, CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(result.StandardOutput).Replace("\r\n", "\n").Trim(), diagnostics);
    }
}
