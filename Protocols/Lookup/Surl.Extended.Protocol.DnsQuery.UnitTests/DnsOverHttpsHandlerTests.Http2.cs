using Surl.Http2;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.DnsQuery.DnsHttp2TestFrames;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// DNS over HTTPS over HTTP/2 (RFC 8484 section 5.2; ADR-0092 decision 5), through
/// <see cref="Http2ConnectionServer"/>, with RFC 8484 section 4.1.1's example requests as
/// HTTP/2 frames written by hand from RFC 9113.
/// </summary>
public sealed partial class DnsOverHttpsHandlerTests
{
    private const uint ProtocolError = 0x1;

    [TestMethod]
    public async Task ServeStreamAsync_NullStream_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Handler().ServeStreamAsync(null!, TestContext.CancellationToken));

    [TestMethod]
    public async Task ServeStreamAsync_Rfc8484ExampleGet_AnswersWithHeadersAndDataEndingTheStream()
    {
        var client = new DnsHttp2TestFrames();

        var written = await ServeHttp2Async(client.Request(1, "GET", $"/dns-query?dns={ExampleGetParameter}", endStream: true, ("accept", "application/dns-message")));

        var (fields, body, ended, resetCode) = Response(written, 1);
        Assert.AreEqual(
            $":status: 200\ndate: {Date}\nserver: surl\ncontent-type: application/dns-message\ncache-control: max-age=128\ncontent-length: {ExampleAnswer.Length}",
            Text(fields));
        CollectionAssert.AreEqual(ExampleAnswer, body);
        Assert.IsTrue(ended);
        Assert.IsNull(resetCode);
    }

    [TestMethod]
    public async Task ServeStreamAsync_Rfc8484ExamplePost_AnswersWithTheDnsMessage()
    {
        var client = new DnsHttp2TestFrames();

        var written = await ServeHttp2Async(
            client.Request(1, "POST", "/dns-query", endStream: false, ("accept", "application/dns-message"), ("content-type", "application/dns-message"), ("content-length", "33")),
            Data(1, ExampleQuery[..20], endStream: false),
            Data(1, ExampleQuery[20..], endStream: true));

        var (fields, body, ended, _) = Response(written, 1);
        Assert.AreEqual(":status: 200", Text(fields.Take(1).ToArray()));
        CollectionAssert.AreEqual(ExampleAnswer, body);
        Assert.IsTrue(ended);
    }

    [TestMethod]
    public async Task ServeStreamAsync_AnotherPath_Answers404WithHeadersEndingTheStream()
    {
        var client = new DnsHttp2TestFrames();

        var written = await ServeHttp2Async(client.Request(1, "GET", "/other", endStream: true));

        var (fields, body, ended, _) = Response(written, 1);
        Assert.AreEqual($":status: 404\ndate: {Date}\nserver: surl\ncontent-length: 0", Text(fields));
        Assert.IsEmpty(body);
        Assert.IsTrue(ended);
    }

    [TestMethod]
    public async Task ServeStreamAsync_PostWhoseDataFallsShortOfItsContentLength_ResetsTheStreamWithProtocolError()
    {
        var client = new DnsHttp2TestFrames();

        var written = await ServeHttp2Async(
            client.Request(1, "POST", "/dns-query", endStream: false, ("content-type", "application/dns-message"), ("content-length", "40")),
            Data(1, ExampleQuery, endStream: true));

        var (fields, _, _, resetCode) = Response(written, 1);
        Assert.IsEmpty(fields);
        Assert.AreEqual(ProtocolError, resetCode);
    }

    [TestMethod]
    public async Task ServeStreamAsync_PostWhoseClientClosesBeforeTheBodyEnds_Answers400()
    {
        var client = new DnsHttp2TestFrames();

        var written = await ServeHttp2Async(
            client.Request(1, "POST", "/dns-query", endStream: false, ("content-type", "application/dns-message")),
            Data(1, ExampleQuery[..10], endStream: false));

        Assert.AreEqual(":status: 400", Text(Response(written, 1).Fields.Take(1).ToArray()));
    }

    private async Task<byte[]> ServeHttp2Async(params byte[][] frames) => await ServeHttp2Async(Handler(), frames);

    private async Task<byte[]> ServeHttp2Async(IHttp2StreamHandler handler, params byte[][] frames)
    {
        var connection = new InMemoryConnection([(byte[])[.. Start, .. frames.SelectMany(frame => frame)]]);

        await new Http2ConnectionServer(handler, Context("https"), connection).ServeAsync();

        return connection.WrittenBytes;
    }
}
