using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// A <c>GET</c>'s <c>Range</c> is answered <c>206</c> with the bytes it selects, or <c>416</c> when
/// it names none, and several ranges or an <c>If-Range</c> that is not the file's
/// <c>Last-Modified</c> get the whole file with <c>200</c> (ADR-0133, decision 1), as the bytes
/// recorded from pinned upstream curl 8.21.0 show (<c>Fixtures/range-*</c>).
/// </summary>
public sealed partial class HttpRequestAnswererTests
{
    [TestMethod]
    [DataRow("range-206")]
    [DataRow("range-suffix-206")]
    [DataRow("range-unsatisfiable-416")]
    [DataRow("range-multiple-200")]
    [DataRow("range-if-range-206")]
    public async Task ServeAsync_RecordedRangeRequest_SendsTheRecordedResponseAndKeepsTheConnection(string caseName)
    {
        var (connection, _) = await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes(caseName)), TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes(caseName, "response.bin"), connection.WrittenBytes, Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task AnswerAsync_GetWithARange_NotesThe206AndTheBytesServed()
    {
        var log = new RecordingExchangeLog();

        var answer = await Answerer(log: log).AnswerAsync(RangeRequest("GET", ("Range", "bytes=3-")), TestContext.CancellationToken);

        Assert.AreEqual(206, answer.Status.Code);
        var file = (HttpAnswerBody.ContentFile)answer.Body;
        Assert.AreEqual(3, file.Range.FirstByte);
        Assert.AreEqual(14, file.Range.ByteCount);
        StringAssert.StartsWith(log.Notes.Single(), "GET /file.txt: 206, 14 bytes of ");
    }

    [TestMethod]
    public async Task AnswerAsync_UnsatisfiableRange_NotesThe416()
    {
        var log = new RecordingExchangeLog();

        await Answerer(log: log).AnswerAsync(RangeRequest("GET", ("Range", "bytes=17-")), TestContext.CancellationToken);

        StringAssert.StartsWith(log.Notes.Single(), "GET /file.txt: 416, the range is past the end of the 17-byte ");
    }

    [TestMethod]
    [DataRow("HEAD", "bytes=0-9", null, DisplayName = "HEAD has no range")]
    [DataRow("GET", "bytes=0-9", "Mon, 31 Aug 2026 08:30:00 GMT", DisplayName = "An older If-Range date")]
    [DataRow("GET", "bytes=0-9", "\"an-etag\"", DisplayName = "An entity-tag If-Range")]
    [DataRow("GET", "items=0-9", null, DisplayName = "Another unit")]
    public async Task AnswerAsync_RangeTheServerIgnores_IsA200WithTheWholeFile(string method, string range, string? ifRange)
    {
        var fields = ifRange is null ? new[] { ("Range", range) } : [("Range", range), ("If-Range", ifRange)];

        var answer = await Answerer().AnswerAsync(RangeRequest(method, fields), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
        CollectionAssert.AreEqual(FileFields(), answer.Fields.ToArray());
    }

    [TestMethod]
    public async Task AnswerAsync_RangeSentTwice_IsIgnored()
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", ("Range", "bytes=0-1"), ("range", "bytes=2-3")), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
    }

    [TestMethod]
    public async Task AnswerAsync_IfRangeWithSpacesAroundTheLastModified_ServesTheRange()
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", ("Range", "bytes=0-9"), ("If-Range", $" {FileDate} ")), TestContext.CancellationToken);

        Assert.AreEqual(206, answer.Status.Code);
    }

    private static HttpRequest RangeRequest(string method, params (string Name, string Value)[] fields) =>
        new(method, "/file.txt", "example.test", [.. fields.Select(field => Field(field.Name, field.Value))], new UnitTestRequestBody(isReadable: true, isAnnounced: false));
}
