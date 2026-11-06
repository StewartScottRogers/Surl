using System.Globalization;
using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// <see cref="HttpRequestAnswerer"/> decides an answer with no connection at all: the status,
/// fields and body it returns are what every HTTP version's writer sends (ADR-0081, decision 8).
/// </summary>
[TestClass]
public sealed partial class HttpRequestAnswererTests
{
    private const string BasicChallenge = "Basic realm=\"surl\", charset=\"UTF-8\"";

    private static readonly string Date = Now.ToString("r", CultureInfo.InvariantCulture);

    private static readonly string FileDate = FileTime.ToString("r", CultureInfo.InvariantCulture);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_GetOfAContentStoreFile_IsA200WithTheFileAsItsBody()
    {
        var answer = await Answerer().AnswerAsync(Request("GET", "/file.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
        CollectionAssert.AreEqual(FileFields(), answer.Fields.ToArray());
        var file = (HttpAnswerBody.ContentFile)answer.Body;
        Assert.AreEqual(Path.Join(Root, "file.txt"), file.Mapping.Location);
        Assert.AreEqual(0, file.Range.FirstByte);
        Assert.AreEqual(FileBody.Length, file.Range.ByteCount);
        Assert.IsFalse(answer.IsRefusal);
        Assert.IsFalse(answer.LeavesRequestBodyUnread);
    }

    [TestMethod]
    public async Task AnswerAsync_HeadOfAContentStoreFile_IsA200WithTheFileFieldsAndNoBody()
    {
        var answer = await Answerer().AnswerAsync(Request("HEAD", "/file.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
        CollectionAssert.AreEqual(FileFields(), answer.Fields.ToArray());
        Assert.AreSame(HttpAnswerBody.None, answer.Body);
        Assert.IsFalse(answer.IsRefusal);
    }

    [TestMethod]
    [DataRow("page.HTML", "text/html")]
    [DataRow("photo.png", "image/png")]
    [DataRow("data.json", "application/json")]
    [DataRow("archive.unknownext", "application/octet-stream")]
    [DataRow("noextension", "application/octet-stream")]
    public async Task AnswerAsync_GetOfAFile_NamesTheContentTypeContentMediaTypesNamesFromItsExtension(string fileName, string contentType)
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, fileName), "x"u8.ToArray(), FileTime);
        var contentStore = new ContentStore(Root, fileSystem, ContentExposureOptions.ServeEverythingInsideTheRoot);
        var answerer = new HttpRequestAnswerer(
            Context(new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken),
            contentStore,
            new AnonymousAuthenticationPolicy().StartHttpConnection(null));

        var answer = await answerer.AnswerAsync(Request("GET", "/" + fileName), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
        Assert.AreEqual(contentType, answer.Fields.Single(field => field.Key == "Content-Type").Value);
        Assert.AreEqual(ContentMediaTypes.Of(fileName), contentType);
    }

    [TestMethod]
    public async Task AnswerAsync_ChallengedRequest_IsAnEmpty401WithEachChallengeAfterContentLength()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, [BasicChallenge, "Bearer"], null));

        var answer = await Answerer(policy).AnswerAsync(Request("GET", "/file.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Unauthorized, answer.Status);
        CollectionAssert.AreEqual(
            new[] { Field("Date", Date), Field("Server", "surl"), Field("Content-Length", "0"), Field("WWW-Authenticate", BasicChallenge), Field("WWW-Authenticate", "Bearer") },
            answer.Fields.ToArray());
        Assert.AreSame(HttpAnswerBody.None, answer.Body);
        Assert.IsFalse(answer.IsRefusal);
        Assert.AreEqual("GET", policy.JudgedRequests.Single().Method);
    }

    [TestMethod]
    public async Task AnswerAsync_PathWhereNothingExists_IsAnEmpty404()
    {
        var log = new RecordingExchangeLog();

        var answer = await Answerer(log: log).AnswerAsync(Request("GET", "/missing.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.NotFound, answer.Status);
        CollectionAssert.AreEqual(new[] { Field("Date", Date), Field("Server", "surl"), Field("Content-Length", "0") }, answer.Fields.ToArray());
        Assert.AreSame(HttpAnswerBody.None, answer.Body);
        Assert.IsFalse(answer.IsRefusal);
        Assert.IsFalse(answer.LeavesRequestBodyUnread);
        StringAssert.StartsWith(log.Notes.Single(), "GET /missing.txt: 404, nothing exists at ");
    }

    [TestMethod]
    public async Task AnswerAsync_BodyItCannotRead_IsAnsweredAndSaysTheBodyWasLeftUnread()
    {
        var body = new UnitTestRequestBody(isReadable: false);

        var answer = await Answerer().AnswerAsync(Request("GET", "/file.txt", body), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
        Assert.IsTrue(answer.LeavesRequestBodyUnread);
        Assert.AreEqual(0, body.DiscardCount);
    }

    [TestMethod]
    public async Task AnswerAsync_RequestFields_AreShownToTheAuthenticationSession()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));
        var request = Request("GET", "/file.txt") with { Fields = [Field("Host", "example.test")] };

        await Answerer(policy).AnswerAsync(request, TestContext.CancellationToken);

        Assert.AreEqual("example.test", request.Authority);
        CollectionAssert.AreEqual(new[] { Field("Host", "example.test") }, policy.JudgedRequests.Single().Fields.ToArray());
    }

    private static KeyValuePair<string, string>[] FileFields() =>
    [
        Field("Date", Date),
        Field("Server", "surl"),
        Field("Last-Modified", FileDate),
        Field("Content-Type", "text/plain"),
        Field("Content-Length", FileBody.Length.ToString(CultureInfo.InvariantCulture)),
    ];

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

    private static HttpRequest Request(string method, string target, IHttpRequestBody? body = null) =>
        new(method, target, "example.test", [], body ?? new UnitTestRequestBody(isReadable: true, isAnnounced: false));

    private HttpRequestAnswerer Answerer(IAuthenticationPolicy? policy = null, RecordingExchangeLog? log = null)
    {
        var contentStore = new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);
        var session = (policy ?? new AnonymousAuthenticationPolicy()).StartHttpConnection(null);

        return new HttpRequestAnswerer(Context(log ?? new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken), contentStore, session);
    }

    // A body that is announced and never arrives: the answerer must not read one it cannot.
    private sealed class UnitTestRequestBody(bool isReadable, bool isAnnounced = true) : IHttpRequestBody
    {
        public int DiscardCount { get; private set; }

        public bool IsAnnounced => isAnnounced;

        public long? AnnouncedLength => null;

        public bool IsReadable => isReadable;

        public Task<HttpRequestBodyDiscardOutcome> DiscardAsync(IncrementalHash? bodyHash, CancellationToken cancellationToken)
        {
            DiscardCount++;

            return Task.FromResult(HttpRequestBodyDiscardOutcome.Discarded);
        }

        // The HTTP server's answerer never keeps a body, so it never copies one.
        public Task<HttpRequestBodyDiscardOutcome> CopyToAsync(Stream destination, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The answerer copied a request body.");
    }
}
