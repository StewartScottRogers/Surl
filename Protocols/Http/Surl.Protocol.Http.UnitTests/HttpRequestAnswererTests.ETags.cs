using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The <c>ETag</c> of a file and the <c>If-None-Match</c>, <c>If-Match</c> and entity-tag <c>If-Range</c>
/// conditions that compare it (ADR-0184, decisions 2, 3, 4 and 6). The recorded requests are
/// BL-1446's recordings of upstream curl 8.21.0's <c>--etag-compare</c> (<c>Fixtures/etag-compare-*</c>).
/// </summary>
public sealed partial class HttpRequestAnswererTests
{
    private const string WeakFileETag = "W/" + FileETag;

    private async Task<HttpAnswer> AnswerFileAsync(UnitTestInMemoryContentFileSystem fileSystem, string method, params (string Name, string Value)[] fields)
    {
        var contentStore = new ContentStore(Root, fileSystem, ContentExposureOptions.ServeEverythingInsideTheRoot);
        var session = new AnonymousAuthenticationPolicy().StartHttpConnection(null);
        var answerer = new HttpRequestAnswerer(Context(new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken), contentStore, session);

        return await answerer.AnswerAsync(RangeRequest(method, fields), TestContext.CancellationToken);
    }

    private static string ETagOf(HttpAnswer answer) => answer.Fields.Single(field => field.Key == "ETag").Value;

    [TestMethod]
    [DataRow("GET", null, 200)]
    [DataRow("HEAD", null, 200)]
    [DataRow("GET", "bytes=0-3", 206)]
    public async Task AnswerAsync_FileAnswer_CarriesTheLengthAndTimeETagBeforeLastModified(string method, string? range, int expectedStatus)
    {
        var answer = await Answerer().AnswerAsync(RangeRequest(method, range is null ? [] : [("Range", range)]), TestContext.CancellationToken);

        Assert.AreEqual(expectedStatus, answer.Status.Code);
        Assert.AreEqual(FileETag, ETagOf(answer));
        var names = answer.Fields.Select(field => field.Key).ToList();
        Assert.AreEqual(names.IndexOf("ETag") + 1, names.IndexOf("Last-Modified"));
    }

    [TestMethod]
    public async Task AnswerAsync_MultipleRanges_CarriesTheETagOnTheWholeAnswer()
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", ("Range", "bytes=0-1,4-5")), TestContext.CancellationToken);

        Assert.AreEqual(206, answer.Status.Code);
        Assert.AreEqual(FileETag, ETagOf(answer));
    }

    [TestMethod]
    public async Task AnswerAsync_ETag_IsStableForAnUnchangedFileAndChangesAfterAWrite()
    {
        var fileSystem = StandardFileSystem();
        var first = ETagOf(await AnswerFileAsync(fileSystem, "GET"));
        var again = ETagOf(await AnswerFileAsync(fileSystem, "GET"));
        fileSystem.AddFile(Path.Join(Root, "file.txt"), Ascii(FileBody), FileTime.AddSeconds(1));
        var afterNewTime = ETagOf(await AnswerFileAsync(fileSystem, "GET"));
        fileSystem.AddFile(Path.Join(Root, "file.txt"), Ascii(FileBody + "more"), FileTime.AddSeconds(1));
        var afterNewLength = ETagOf(await AnswerFileAsync(fileSystem, "GET"));

        Assert.AreEqual(first, again);
        Assert.AreEqual(FileETag, first);
        Assert.AreNotEqual(first, afterNewTime);
        Assert.AreNotEqual(afterNewTime, afterNewLength);
        StringAssert.EndsWith(afterNewLength, "-15\"");
    }

    [TestMethod]
    [DataRow("If-None-Match", FileETag, 304, DisplayName = "None-Match the current tag")]
    [DataRow("If-None-Match", "*", 304, DisplayName = "None-Match star")]
    [DataRow("If-None-Match", "\"other\", " + FileETag, 304, DisplayName = "None-Match a list holding the tag")]
    [DataRow("If-None-Match", WeakFileETag, 304, DisplayName = "None-Match compares weakly")]
    [DataRow("If-None-Match", "\"other\"", 200, DisplayName = "None-Match another tag")]
    [DataRow("If-None-Match", "\"abc\"", 200, DisplayName = "None-Match curl's recorded tag")]
    [DataRow("If-None-Match", "nonsense", 200, DisplayName = "None-Match no tag is ignored")]
    [DataRow("If-None-Match", "", 200, DisplayName = "None-Match empty is ignored")]
    [DataRow("If-None-Match", "\"", 200, DisplayName = "None-Match one quote is ignored")]
    [DataRow("If-Match", FileETag, 200, DisplayName = "Match the current tag")]
    [DataRow("If-Match", "*", 200, DisplayName = "Match star")]
    [DataRow("If-Match", "\"other\", " + FileETag, 200, DisplayName = "Match a list holding the tag")]
    [DataRow("If-Match", "\"other\"", 412, DisplayName = "Match another tag")]
    [DataRow("If-Match", WeakFileETag, 412, DisplayName = "Match is strong, a weak tag fails")]
    [DataRow("If-Match", "nonsense", 200, DisplayName = "Match no tag is ignored")]
    public async Task AnswerAsync_EntityTagConditionOnAGet_IsAnswered304Or412OrTheFile(string name, string value, int expectedStatus)
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", (name, value)), TestContext.CancellationToken);

        Assert.AreEqual(expectedStatus, answer.Status.Code);
        Assert.AreEqual(expectedStatus == 200, answer.Body is HttpAnswerBody.ContentFile);
    }

    [TestMethod]
    public async Task AnswerAsync_NoneMatchMatching_Is304WithDateServerETagAndLastModifiedAndNoBody()
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", ("If-None-Match", FileETag)), TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { Field("Date", Date), Field("Server", "surl"), Field("ETag", FileETag), Field("Last-Modified", FileDate) },
            answer.Fields.ToArray());
        Assert.AreSame(HttpAnswerBody.None, answer.Body);
        Assert.IsFalse(answer.IsRefusal);
    }

    [TestMethod]
    public async Task AnswerAsync_MatchFailing_Is412WithDateServerAndZeroLengthAndNoBody()
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", ("If-Match", "\"other\"")), TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { Field("Date", Date), Field("Server", "surl"), Field("Content-Length", "0") }, answer.Fields.ToArray());
        Assert.AreSame(HttpAnswerBody.None, answer.Body);
    }

    [TestMethod]
    [DataRow("If-None-Match", FileETag, 304)]
    [DataRow("If-Match", "\"other\"", 412)]
    public async Task AnswerAsync_EntityTagConditionOnAHead_IsAnsweredAsOnAGetWithoutABody(string name, string value, int expectedStatus)
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("HEAD", (name, value)), TestContext.CancellationToken);

        Assert.AreEqual(expectedStatus, answer.Status.Code);
        Assert.AreSame(HttpAnswerBody.None, answer.Body);
    }

    [TestMethod]
    public async Task AnswerAsync_EntityTagConditionSentTwice_IsIgnored()
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", ("If-None-Match", FileETag), ("If-None-Match", FileETag)), TestContext.CancellationToken);

        Assert.AreEqual(200, answer.Status.Code);
    }

    [TestMethod]
    [DataRow("If-Match", FileETag, "If-Unmodified-Since", "Tue, 01 Sep 2026 08:29:59 GMT", 200, DisplayName = "A passing If-Match shadows If-Unmodified-Since")]
    [DataRow("If-Match", "\"other\"", "If-Unmodified-Since", RecordedDate, 412, DisplayName = "A failing If-Match is 412 whatever the date says")]
    [DataRow("If-None-Match", "\"other\"", "If-Modified-Since", RecordedDate, 200, DisplayName = "A non-matching If-None-Match shadows If-Modified-Since")]
    [DataRow("If-None-Match", FileETag, "If-Modified-Since", "Tue, 01 Sep 2026 08:29:59 GMT", 304, DisplayName = "A matching If-None-Match is 304 whatever the date says")]
    public async Task AnswerAsync_EntityTagAndDateConditionsTogether_FollowTheAdrOrder(string name, string value, string dateName, string dateValue, int expectedStatus)
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", (name, value), (dateName, dateValue)), TestContext.CancellationToken);

        Assert.AreEqual(expectedStatus, answer.Status.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_NoneMatchMatchingBeforeARange_IsA304NotA206()
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", ("Range", "bytes=0-3"), ("If-None-Match", FileETag)), TestContext.CancellationToken);

        Assert.AreEqual(304, answer.Status.Code);
    }

    [TestMethod]
    [DataRow(FileETag, 206, DisplayName = "The current strong tag")]
    [DataRow("  " + FileETag + " ", 206, DisplayName = "The current tag with spaces around it")]
    [DataRow(WeakFileETag, 200, DisplayName = "A weak tag")]
    [DataRow("\"8df0803365eb400-12\"", 200, DisplayName = "A tag of another length")]
    public async Task AnswerAsync_EntityTagIfRange_ServesTheRangeOnlyOnTheCurrentStrongTag(string ifRange, int expectedStatus)
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", ("Range", "bytes=0-3"), ("If-Range", ifRange)), TestContext.CancellationToken);

        Assert.AreEqual(expectedStatus, answer.Status.Code);
        Assert.AreEqual(expectedStatus == 206 ? 4 : FileBody.Length, ((HttpAnswerBody.ContentFile)answer.Body).Range.ByteCount);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedEtagCompareWithATagThatIsNotTheFiles_Sends200WithTheETagCurlSaves()
    {
        var (connection, _) = await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("etag-compare-200")), TestContext.CancellationToken);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), $"HTTP/1.1 200 OK\r\nDate: {Date}\r\nServer: surl\r\nETag: {FileETag}\r\n");
        StringAssert.EndsWith(Latin1(connection.WrittenBytes), FileBody);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedEtagCompareWithTheTagTheServerSent_Sends304WithNoBody()
    {
        var request = Latin1(RecordedFixture.ReadRequestBytes("etag-compare-304")).Replace("\"abc\"", FileETag, StringComparison.Ordinal);

        var (connection, _) = await ServeAsync(RecordedFixture.Whole(Ascii(request)), TestContext.CancellationToken);

        Assert.AreEqual(
            $"HTTP/1.1 304 Not Modified\r\nDate: {Date}\r\nServer: surl\r\nETag: {FileETag}\r\nLast-Modified: {FileDate}\r\n\r\n",
            Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
    }
}
