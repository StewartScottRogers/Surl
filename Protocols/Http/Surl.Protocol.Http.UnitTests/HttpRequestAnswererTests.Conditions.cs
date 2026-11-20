using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// <c>If-Modified-Since</c> and <c>If-Unmodified-Since</c> on a file's <c>GET</c> and <c>HEAD</c>:
/// <c>304</c>, <c>412</c> or the file, as ADR-0184 decisions 4 and 5 say; the requests are the bytes
/// recorded from pinned upstream curl 8.21.0 (<c>Fixtures/time-cond-*</c>).
/// </summary>
public sealed partial class HttpRequestAnswererTests
{
    // The mtime of file.txt in the replays that need a file older than curl's recorded date, 1 January 2025.
    private static readonly DateTimeOffset OlderFileTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const string OlderFileETag = "8dc0a5c9900c000-11";

    private const string OlderFileDate = "Mon, 01 Jan 2024 00:00:00 GMT";

    private const string RecordedDate = "Wed, 01 Jan 2025 00:00:00 GMT";

    private static UnitTestInMemoryContentFileSystem FileSystemWithFileAt(DateTimeOffset modifiedAt) =>
        new UnitTestInMemoryContentFileSystem().AddDirectory(Root).AddFile(Path.Join(Root, "file.txt"), Ascii(FileBody), modifiedAt);

    [TestMethod]
    public async Task ServeAsync_RecordedIfModifiedSinceOfAnUnchangedFile_Sends304WithNoLengthAndNoBody()
    {
        var (connection, _) = await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("time-cond-since-304")), TestContext.CancellationToken, fileSystem: FileSystemWithFileAt(OlderFileTime));

        Assert.AreEqual(
            $"HTTP/1.1 304 Not Modified\r\nDate: {Date}\r\nServer: surl\r\nETag: \"{OlderFileETag}\"\r\nLast-Modified: {OlderFileDate}\r\n\r\n",
            Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedIfUnmodifiedSinceOfAChangedFile_Sends412AndKeepsTheConnection()
    {
        var (connection, _) = await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("time-cond-until-412")), TestContext.CancellationToken);

        Assert.AreEqual($"HTTP/1.1 412 Precondition Failed\r\nDate: {Date}\r\nServer: surl\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: 24\r\n\r\n412 Precondition Failed\n", Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    [DataRow("time-cond-since-200-newer", false, DisplayName = "-z against a file newer than the date")]
    [DataRow("time-cond-until-200", true, DisplayName = "-z - against a file older than the date")]
    public async Task ServeAsync_RecordedConditionTheFileDoesNotDecide_IsServedWith200(string caseName, bool fileIsOlder)
    {
        var fileSystem = fileIsOlder ? FileSystemWithFileAt(OlderFileTime) : StandardFileSystem();

        var (connection, _) = await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes(caseName)), TestContext.CancellationToken, fileSystem: fileSystem);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(Latin1(connection.WrittenBytes), FileBody);
    }

    [TestMethod]
    [DataRow("If-Modified-Since", "Tue, 01 Sep 2026 08:30:00 GMT", 304, DisplayName = "Modified-since the exact mtime")]
    [DataRow("If-Modified-Since", "Tue, 01 Sep 2026 08:30:01 GMT", 304, DisplayName = "Modified-since a later date")]
    [DataRow("If-Modified-Since", "Tue, 01 Sep 2026 08:29:59 GMT", 200, DisplayName = "Modified-since an earlier date")]
    [DataRow("If-Modified-Since", "Tuesday, 01-Sep-26 08:30:00 GMT", 304, DisplayName = "RFC 850 date")]
    [DataRow("If-Modified-Since", "Tue Sep  1 08:30:00 2026", 304, DisplayName = "asctime date")]
    [DataRow("If-Modified-Since", "nonsense", 200, DisplayName = "Invalid date is ignored")]
    [DataRow("If-Modified-Since", "", 200, DisplayName = "Empty date is ignored")]
    [DataRow("If-Modified-Since", "Mon, 01 Sep 2026 08:30:00 GMT", 200, DisplayName = "Wrong weekday is ignored")]
    [DataRow("If-Unmodified-Since", "Tue, 01 Sep 2026 08:30:00 GMT", 200, DisplayName = "Unmodified-since the exact mtime")]
    [DataRow("If-Unmodified-Since", "Tue, 01 Sep 2026 08:29:59 GMT", 412, DisplayName = "Unmodified-since an earlier date")]
    [DataRow("if-unmodified-since", "nonsense", 200, DisplayName = "Invalid unmodified date is ignored")]
    public async Task AnswerAsync_DateConditionOnAGet_IsAnswered304Or412OrTheFile(string name, string value, int expectedStatus)
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", (name, value)), TestContext.CancellationToken);

        Assert.AreEqual(expectedStatus, answer.Status.Code);
        Assert.AreEqual(expectedStatus == 200, answer.Body is HttpAnswerBody.ContentFile);
    }

    [TestMethod]
    public async Task AnswerAsync_NotModifiedGet_CarriesDateServerAndLastModifiedAndNoBody()
    {
        var log = new RecordingExchangeLog();

        var answer = await Answerer(log: log).AnswerAsync(RangeRequest("GET", ("If-Modified-Since", FileDate)), TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { Field("Date", Date), Field("Server", "surl"), Field("ETag", FileETag), Field("Last-Modified", FileDate) }, answer.Fields.ToArray());
        Assert.AreSame(HttpAnswerBody.None, answer.Body);
        Assert.IsFalse(answer.IsRefusal);
        StringAssert.StartsWith(log.Notes.Single(), "GET /file.txt: 304, ");
    }

    [TestMethod]
    public async Task AnswerAsync_PreconditionFailedGet_CarriesDateServerAndZeroLengthAndNoBody()
    {
        var log = new RecordingExchangeLog();

        var answer = await Answerer(log: log).AnswerAsync(RangeRequest("GET", ("If-Unmodified-Since", RecordedDate)), TestContext.CancellationToken);

        Assert.AreEqual(412, answer.Status.Code);
        CollectionAssert.AreEqual(new[] { Field("Date", Date), Field("Server", "surl"), Field("Content-Type", "text/plain; charset=utf-8"), Field("Content-Length", "24") }, answer.Fields.ToArray());
        AssertErrorText(answer, "412 Precondition Failed");
        StringAssert.StartsWith(log.Notes.Single(), "GET /file.txt: 412, ");
    }

    [TestMethod]
    [DataRow("If-Modified-Since", 304)]
    [DataRow("If-Unmodified-Since", 412)]
    public async Task AnswerAsync_DateConditionOnAHead_IsAnsweredAsOnAGetWithoutABody(string name, int expectedStatus)
    {
        var value = name == "If-Modified-Since" ? FileDate : RecordedDate;

        var answer = await Answerer().AnswerAsync(RangeRequest("HEAD", (name, value)), TestContext.CancellationToken);

        Assert.AreEqual(expectedStatus, answer.Status.Code);
        Assert.AreSame(HttpAnswerBody.None, answer.Body);
    }

    [TestMethod]
    public async Task AnswerAsync_DateConditionSentTwice_IsIgnored()
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", ("If-Modified-Since", FileDate), ("If-Modified-Since", FileDate)), TestContext.CancellationToken);

        Assert.AreEqual(200, answer.Status.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_UnmodifiedSinceFailsBesideModifiedSince_Is412()
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", ("If-Modified-Since", FileDate), ("If-Unmodified-Since", RecordedDate)), TestContext.CancellationToken);

        Assert.AreEqual(412, answer.Status.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_NotModifiedBeforeARange_IsA304NotA206()
    {
        var answer = await Answerer().AnswerAsync(RangeRequest("GET", ("Range", "bytes=0-3"), ("If-Modified-Since", FileDate)), TestContext.CancellationToken);

        Assert.AreEqual(304, answer.Status.Code);
    }

    [TestMethod]
    public async Task AnswerAsync_NotModifiedFileWithAFutureMtime_ComparesTheLastModifiedItSends()
    {
        var contentStore = new ContentStore(Root, FileSystemWithFileAt(Now.AddDays(10)), ContentExposureOptions.ServeEverythingInsideTheRoot);
        var answerer = new HttpRequestAnswerer(
            Context(new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken),
            contentStore,
            new AnonymousAuthenticationPolicy().StartHttpConnection(null));

        var answer = await answerer.AnswerAsync(RangeRequest("GET", ("If-Modified-Since", Date)), TestContext.CancellationToken);

        Assert.AreEqual(304, answer.Status.Code);
        Assert.AreEqual(Date, answer.Fields.Single(field => field.Key == "Last-Modified").Value);
    }
}
