using System.Net;
using System.Text;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.DnsQuery.DnsTestQueries;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// DNS over HTTPS (RFC 8484; ADR-0092 decision 5) over HTTP/1.1, through
/// <see cref="Http1ConnectionServer"/> and the connection seam, from RFC 8484 section 4.1.1's
/// example requests and answers written by hand from RFC 1035 section 4.1.
/// </summary>
[TestClass]
public sealed partial class DnsOverHttpsHandlerTests
{
    /// <summary>RFC 8484 section 4.1.1's first example: <c>www.example.com</c> A, ID 0, RD set.</summary>
    internal const string ExampleGetParameter = "AAABAAABAAAAAAAAA3d3dwdleGFtcGxlA2NvbQAAAQAB";

    /// <summary>RFC 8484 section 4.1.1's third example: a 62-character label that needs base64url's <c>-</c>.</summary>
    internal const string LongLabelGetParameter =
        "AAABAAABAAAAAAAAAWE-NjJjaGFyYWN0ZXJsYWJlbC1tYWtlcy1iYXNlNjR1cmwtZGlzdGluY3QtZnJvbS1zdGFuZGFyZC1iYXNlNjQHZXhhbXBsZQNjb20AAAEAAQ";

    private const string Date = "Wed, 30 Sep 2026 12:00:00 GMT";
    private const ushort TypeAxfr = 252;

    /// <summary>RFC 8484 section 4.1.1's POST body: the same query, 33 bytes.</summary>
    internal static readonly byte[] ExampleQuery =
    [
        0x00, 0x00, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x03, 0x77, 0x77, 0x77, 0x07, 0x65, 0x78, 0x61, 0x6d, 0x70, 0x6c, 0x65, 0x03, 0x63, 0x6f, 0x6d, 0x00,
        0x00, 0x01, 0x00, 0x01,
    ];

    /// <summary>
    /// The answer to <see cref="ExampleQuery"/> from <see cref="Zone"/>: ID 0, QR AA RD, the
    /// question, and <c>www.example.com</c>'s A record at TTL 128 (RFC 1035 section 4.1).
    /// </summary>
    internal static readonly byte[] ExampleAnswer =
    [
        0x00, 0x00, 0x85, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
        0x03, 0x77, 0x77, 0x77, 0x07, 0x65, 0x78, 0x61, 0x6d, 0x70, 0x6c, 0x65, 0x03, 0x63, 0x6f, 0x6d, 0x00,
        0x00, 0x01, 0x00, 0x01,
        0xc0, 0x0c, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x80, 0x00, 0x04, 93, 184, 216, 34,
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsOverHttpsHandler(null!, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DnsOverHttpsHandler(new DnsZoneSet(), null!));
    }

    [TestMethod]
    public async Task AnswerAsync_NullRequest_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Handler().AnswerAsync(null!, TestContext.CancellationToken));

    [TestMethod]
    public async Task AnswerAsync_Rfc8484ExampleGet_Answers200WithTheDnsMessageAndMaxAgeFromItsTtl()
    {
        var written = await SendAsync($"GET /dns-query?dns={ExampleGetParameter} HTTP/1.1\r\nHost: dnsserver.example.net\r\nAccept: application/dns-message\r\nConnection: close\r\n\r\n");

        CollectionAssert.AreEqual(Answer200(ExampleAnswer, maxAge: 128), written);
    }

    [TestMethod]
    public async Task AnswerAsync_Rfc8484ExamplePost_Answers200WithTheDnsMessageAndMaxAgeFromItsTtl()
    {
        var written = await SendAsync(
            "POST /dns-query HTTP/1.1\r\nHost: dnsserver.example.net\r\nAccept: application/dns-message\r\nContent-Type: application/dns-message\r\nContent-Length: 33\r\nConnection: close\r\n\r\n",
            ExampleQuery);

        CollectionAssert.AreEqual(Answer200(ExampleAnswer, maxAge: 128), written);
    }

    [TestMethod]
    public async Task AnswerAsync_Rfc8484LongLabelGet_AnswersNxdomainWithTheSoasNegativeTtlAsMaxAge()
    {
        var written = await SendAsync($"GET /dns-query?dns={LongLabelGetParameter} HTTP/1.1\r\nHost: dnsserver.example.net\r\nConnection: close\r\n\r\n");

        var (head, body) = Split(written);
        StringAssert.StartsWith(head, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(head, "\r\nCache-Control: max-age=300\r\n");
        var message = ReadWhole(body);
        Assert.AreEqual("a.62characterlabel-makes-base64url-distinct-from-standard-base64.example.com", message.Questions.Single().Name.ToString().TrimEnd('.'));
        Assert.AreEqual(DnsResponseCode.NonExistentDomain, message.Header.ResponseCode);
    }

    [TestMethod]
    [DataRow("application/dns-message; charset=binary")]
    [DataRow("Application/DNS-Message")]
    public async Task AnswerAsync_PostWithTheMediaTypeInAnotherCaseOrWithAParameter_Answers200(string contentType)
    {
        var written = await SendAsync(PostHead(contentType, ExampleQuery.Length), ExampleQuery);

        CollectionAssert.AreEqual(Answer200(ExampleAnswer, maxAge: 128), written);
    }

    [TestMethod]
    public async Task AnswerAsync_GetWithOtherParametersBesideDns_IgnoresThem()
    {
        var written = await SendAsync($"GET /dns-query?ct&dns={ExampleGetParameter}&x=y HTTP/1.1\r\nHost: a\r\nConnection: close\r\n\r\n");

        CollectionAssert.AreEqual(Answer200(ExampleAnswer, maxAge: 128), written);
    }

    [TestMethod]
    [DataRow("/dns-query")]
    [DataRow("/dns-query?")]
    [DataRow("/dns-query?dns")]
    [DataRow("/dns-query?dns=")]
    [DataRow("/dns-query?dnsx=" + ExampleGetParameter)]
    [DataRow("/dns-query?dns=" + ExampleGetParameter + "&dns=" + ExampleGetParameter)]
    [DataRow("/dns-query?dns=AAABAAABAAAAAAAAA3d3dwdleGFtcGxlA2NvbQAAAQ==")]
    [DataRow("/dns-query?dns=AAABAAABAAAAAAAAA3d3dwdleGFtcGxlA2NvbQAAAQAB%3D")]
    [DataRow("/dns-query?dns=AAABAAABAAAAAAAAA3d3dwdleGFtcGxlA2NvbQAAA+AB")]
    [DataRow("/dns-query?dns=AAABAAABAAAAAAAAA3d3dwdleGFtcGxlA2NvbQAAAQABA")]
    [DataRow("/dns-query?dns=AAAAAAAA")]
    public async Task AnswerAsync_GetWithABadDnsParameter_Answers400(string target)
    {
        var written = await SendAsync($"GET {target} HTTP/1.1\r\nHost: a\r\nConnection: close\r\n\r\n");

        Assert.AreEqual(EmptyAnswer("400 Bad Request", "close"), Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    public async Task AnswerAsync_GetOfAResponseMessage_Answers400AsTheAnswererDropsIt()
    {
        byte[] response = [0x00, 0x00, 0x81, 0x00, .. ExampleQuery[4..]];

        var written = await SendAsync($"GET /dns-query?dns={Convert.ToBase64String(response).TrimEnd('=').Replace('+', '-').Replace('/', '_')} HTTP/1.1\r\nHost: a\r\nConnection: close\r\n\r\n");

        Assert.AreEqual(EmptyAnswer("400 Bad Request", "close"), Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    [DataRow("Content-Type: text/plain\r\n")]
    [DataRow("Content-Type: application/dns-message-x\r\n")]
    [DataRow("")]
    public async Task AnswerAsync_PostOfAnotherMediaTypeOrNone_Answers415(string contentTypeField)
    {
        var written = await SendAsync(
            $"POST /dns-query HTTP/1.1\r\nHost: a\r\n{contentTypeField}Content-Length: 33\r\nConnection: close\r\n\r\n",
            ExampleQuery);

        StringAssert.StartsWith(Encoding.ASCII.GetString(written), "HTTP/1.1 415 Unsupported Media Type\r\n");
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/dns-query/x?dns=" + ExampleGetParameter)]
    [DataRow("/DNS-QUERY?dns=" + ExampleGetParameter)]
    public async Task AnswerAsync_AnotherPath_Answers404(string target)
    {
        var written = await SendAsync($"GET {target} HTTP/1.1\r\nHost: a\r\nConnection: close\r\n\r\n");

        Assert.AreEqual(EmptyAnswer("404 Not Found", "close"), Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    [DataRow("PUT")]
    [DataRow("HEAD")]
    [DataRow("DELETE")]
    public async Task AnswerAsync_AnotherMethod_Answers405WithAllowGetPost(string method)
    {
        var written = await SendAsync($"{method} /dns-query HTTP/1.1\r\nHost: a\r\nConnection: close\r\n\r\n");

        Assert.AreEqual(EmptyAnswer("405 Method Not Allowed", "close", "Allow: GET, POST\r\n"), Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    public async Task AnswerAsync_PostAnnouncingABodyPast65535Bytes_Answers413AndCloses()
    {
        var written = await SendAsync(PostHead("application/dns-message", 65536), ExampleQuery);

        Assert.AreEqual(EmptyAnswer("413 Content Too Large", "close"), Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    public async Task AnswerAsync_ChunkedPostPast65535Bytes_Answers413AndCloses()
    {
        var chunk = new byte[65536];

        var written = await SendAsync(
            "POST /dns-query HTTP/1.1\r\nHost: a\r\nContent-Type: application/dns-message\r\nTransfer-Encoding: chunked\r\n\r\n10000\r\n",
            [.. chunk, .. "\r\n0\r\n\r\n"u8]);

        Assert.AreEqual(EmptyAnswer("413 Content Too Large", "close"), Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    public async Task AnswerAsync_ChunkedPostPastTheUploadLimit_Answers413AndCloses()
    {
        var written = await SendAsync(
            "POST /dns-query HTTP/1.1\r\nHost: a\r\nContent-Type: application/dns-message\r\nTransfer-Encoding: chunked\r\n\r\n21\r\n",
            [.. ExampleQuery, .. "\r\n0\r\n\r\n"u8],
            ExchangeLimits.Default with { MaxUploadBytes = 20 });

        Assert.AreEqual(EmptyAnswer("413 Content Too Large", "close"), Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    public async Task AnswerAsync_PostWhoseBodyEndsEarly_Answers400AndCloses()
    {
        var written = await SendAsync(PostHead("application/dns-message", 33), ExampleQuery[..10]);

        Assert.AreEqual(EmptyAnswer("400 Bad Request", "close"), Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    public async Task AnswerAsync_PostWithAnUnreadableTransferCoding_Answers400AndCloses()
    {
        var request = new HttpRequest("POST", "/dns-query", "a", [new("Content-Type", "application/dns-message")], new UnreadableBody());

        var answer = await Handler().AnswerAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(400, answer.Status.Code);
        Assert.IsTrue(answer.IsRefusal);
    }

    [TestMethod]
    public async Task AnswerAsync_PostWithNoBody_Answers400AsAnEmptyMessageIsDropped()
    {
        var written = await SendAsync(PostHead("application/dns-message", 0));

        Assert.AreEqual(EmptyAnswer("400 Bad Request", "close"), Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    [DataRow(false, DnsResponseCode.Refused)]
    [DataRow(true, DnsResponseCode.NotImplemented)]
    public async Task AnswerAsync_Axfr_IsRefusedOrNotImplementedWithMaxAgeZero(bool allowZoneTransfer, DnsResponseCode expected)
    {
        var query = Query("example.com", TypeAxfr, id: 0);

        var written = await SendAsync(PostHead("application/dns-message", query.Length), query, allowZoneTransfer: allowZoneTransfer);

        var (head, body) = Split(written);
        StringAssert.StartsWith(head, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(head, "\r\nCache-Control: max-age=0\r\n");
        Assert.AreEqual(expected, ReadWhole(body).Header.ResponseCode);
    }

    [TestMethod]
    public async Task AnswerAsync_TwoGetsOnOneConnection_AnswersBothAndKeepsItOpenBetweenThem()
    {
        var get = $"GET /dns-query?dns={ExampleGetParameter} HTTP/1.1\r\nHost: a\r\n\r\n";

        var written = await SendAsync(get + get.Replace("\r\n\r\n", "\r\nConnection: close\r\n\r\n", StringComparison.Ordinal));

        CollectionAssert.AreEqual(
            (byte[])[.. Answer200(ExampleAnswer, 128, closes: false), .. Answer200(ExampleAnswer, 128)],
            written);
    }

    internal static DnsZoneSet Zone() => DnsTestZones.Load(("example.com.zone", """
        $TTL 3600
        @    SOA ns hostmaster 1 7200 900 1209600 300
        www  128 A 93.184.216.34
        """));

    private static DnsOverHttpsHandler Handler(bool allowZoneTransfer = false) => new(Zone(), new ManualTimeProvider(), allowZoneTransfer);

    private static string PostHead(string contentType, int contentLength) =>
        $"POST /dns-query HTTP/1.1\r\nHost: a\r\nContent-Type: {contentType}\r\nContent-Length: {contentLength}\r\nConnection: close\r\n\r\n";

    // An HTTP/1.1 answer that keeps the connection open carries no Connection field.
    private static byte[] Answer200(byte[] message, uint maxAge, bool closes = true) =>
    [
        .. Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nDate: {Date}\r\nServer: surl\r\nContent-Type: application/dns-message\r\n"
            + $"Cache-Control: max-age={maxAge}\r\nContent-Length: {message.Length}\r\n{(closes ? "Connection: close\r\n" : string.Empty)}\r\n"),
        .. message,
    ];

    private static string EmptyAnswer(string status, string connection, string extraFields = "") =>
        $"HTTP/1.1 {status}\r\nDate: {Date}\r\nServer: surl\r\n{extraFields}Content-Length: 0\r\nConnection: {connection}\r\n\r\n";

    private static (string Head, byte[] Body) Split(byte[] written)
    {
        var end = written.AsSpan().IndexOf("\r\n\r\n"u8) + 4;
        return (Encoding.ASCII.GetString(written[..end]), written[end..]);
    }

    private async Task<byte[]> SendAsync(string head, byte[]? body = null, ExchangeLimits? limits = null, bool allowZoneTransfer = false)
    {
        var connection = new InMemoryConnection([(byte[])[.. Encoding.ASCII.GetBytes(head), .. body ?? []]]);
        var context = Context("http") with { Limits = limits ?? ExchangeLimits.Default };

        await new Http1ConnectionServer(Handler(allowZoneTransfer), context).ServeAsync(connection);

        return connection.WrittenBytes;
    }

    private ExchangeContext Context(string scheme) => new(
        1,
        new ListenUrl(scheme, "127.0.0.1", 443).WithBoundPort(443),
        new IPEndPoint(IPAddress.Loopback, 443),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        new ManualTimeProvider(),
        TestContext.CancellationToken);

    private sealed class UnreadableBody : IHttpRequestBody
    {
        public bool IsAnnounced => true;

        public long? AnnouncedLength => null;

        public bool IsReadable => false;

        public Task<HttpRequestBodyDiscardOutcome> DiscardAsync(System.Security.Cryptography.IncrementalHash? bodyHash, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<HttpRequestBodyDiscardOutcome> CopyToAsync(Stream destination, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
