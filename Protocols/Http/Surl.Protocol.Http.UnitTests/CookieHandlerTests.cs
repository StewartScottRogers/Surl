using System.Text;
using Surl.Content;
using Surl.Cookies;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The cookies the HTTP server sets and publishes (ADR-0189 decisions 2, 3 and 5). The expected <c>Set-Cookie</c> bytes and
/// the <c>Cookie</c> request header are those measured from upstream curl 8.21.0 in the ADR.
/// </summary>
[TestClass]
public sealed class CookieHandlerTests
{
    private const string MeasuredFirst = "sid=abc123; Domain=127.0.0.1; Path=/; Expires=Wed, 09 Jun 2027 10:18:14 GMT; Max-Age=3600; Secure; HttpOnly; SameSite=Lax";

    private static readonly CookieDefinition Session = new("sid", "abc123", Domain: "127.0.0.1", Path: "/", Expires: "Wed, 09 Jun 2027 10:18:14 GMT", MaxAge: 3600, Secure: true, HttpOnly: true, SameSite: "Lax");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_ConfiguredCookies_AddEachSetCookieFieldToTheGetReply()
    {
        var (written, _) = await ServeAsync([Session, new CookieDefinition("sess", "xyz", Path: "/")]);

        StringAssert.Contains(written, "Set-Cookie: " + MeasuredFirst + "\r\nSet-Cookie: sess=xyz; Path=/\r\n");
        StringAssert.StartsWith(written, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_RecordedCookieHeader_PublishesEachPairInOrder()
    {
        var (_, events) = await ServeAsync([]);

        CollectionAssert.AreEqual(
            new[]
            {
                new CookieReceivedEvent(1, "http", "sess", "xyz"),
                new CookieReceivedEvent(1, "http", "sid", "abc123"),
            },
            events.Events.OfType<CookieReceivedEvent>().ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NoCookieConfigured_ReplyHasNoSetCookieAndIsUnchanged()
    {
        var (withNone, _) = await ServeAsync([]);
        var (plain, _) = await ServeAsync(null);

        Assert.DoesNotContain("Set-Cookie", withNone, StringComparison.Ordinal);
        Assert.AreEqual(plain, withNone);
    }

    [TestMethod]
    [DataRow("/file.txt", "/file.txt", true)]
    [DataRow("/file.txt", "/file.txt?a=1", true)]
    [DataRow("/file.txt", "http://127.0.0.1:18050/file.txt#top", true)]
    [DataRow("/file.txt", "http://127.0.0.1:18050", false)]
    [DataRow("/a", "/a/b", true)]
    [DataRow("/a/", "/a/b", true)]
    [DataRow("/a", "/ab", false)]
    [DataRow("/a", "/a", true)]
    [DataRow("/file.txt/x", "/file.txt", false)]
    [DataRow(null, "/anything", true)]
    public async Task AnswerAsync_OnPath_SetsTheCookieOnlyOnMatchingPaths(string? onPath, string target, bool isSet)
    {
        var handler = Handler([new CookieDefinition("a", "1", OnPath: onPath)], new RecordingSurlEventPublisher());

        var answer = await handler.AnswerAsync(Request(target, []), TestContext.CancellationToken);

        Assert.AreEqual(isSet, answer.Fields.Any(field => field.Key == "Set-Cookie"));
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:18050/", true)]
    [DataRow("http://127.0.0.1:0/", true)]
    [DataRow("HTTP://127.0.0.1:18050", true)]
    [DataRow("http://127.0.0.1:9/", false)]
    [DataRow("https://127.0.0.1:18050/", false)]
    [DataRow("http://localhost:18050/", false)]
    [DataRow("not a url", false)]
    public async Task AnswerAsync_OnListener_SetsTheCookieOnlyOnThatListener(string listener, bool isSet)
    {
        var handler = Handler([new CookieDefinition("a", "1", OnListener: listener)], new RecordingSurlEventPublisher());

        var answer = await handler.AnswerAsync(Request("/file.txt", []), TestContext.CancellationToken);

        Assert.AreEqual(isSet, answer.Fields.Any(field => field.Key == "Set-Cookie"));
    }

    [TestMethod]
    public async Task AnswerAsync_ScopingKeys_AreNeverWritten()
    {
        var handler = Handler([new CookieDefinition("a", "1", OnPath: "/", OnListener: "http://127.0.0.1:18050/")], new RecordingSurlEventPublisher());

        var answer = await handler.AnswerAsync(Request("/file.txt", []), TestContext.CancellationToken);

        Assert.AreEqual("a=1", answer.Fields.Single(field => field.Key == "Set-Cookie").Value);
    }

    [TestMethod]
    public async Task AnswerAsync_SeveralCookieHeaders_PublishesEveryPair()
    {
        var events = new RecordingSurlEventPublisher();
        var handler = Handler([], events);

        await handler.AnswerAsync(Request("/", [new("cookie", "a=1"), new("Host", "x"), new("Cookie", "b=2; c=3")]), TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, events.Events.OfType<CookieReceivedEvent>().Select(cookie => cookie.Name).ToArray());
    }

    [TestMethod]
    public void CookieReceivedEvent_LongValue_IsCutToTheEventByteBound()
    {
        var received = new CookieReceivedEvent(1, "http", "a", new string('x', 300));

        Assert.AreEqual(CookieReceivedEvent.MaxValueBytes, Encoding.UTF8.GetByteCount(received.Value));
    }

    private CookieHandler Handler(IReadOnlyList<CookieDefinition> cookies, RecordingSurlEventPublisher events) =>
        new(new FixedHandler(), cookies, HttpServerHarness.Context(new RecordingExchangeLog(), new ManualTimeProvider(HttpServerHarness.Now), TestContext.CancellationToken) with { Events = events });

    private static HttpRequest Request(string target, IReadOnlyList<KeyValuePair<string, string>> fields) =>
        new("GET", target, "127.0.0.1", fields, new UnreadRequestBody());

    // cookies null builds the server through the constructor that takes none.
    private async Task<(string Written, RecordingSurlEventPublisher Events)> ServeAsync(IReadOnlyList<CookieDefinition>? cookies)
    {
        var store = new ContentStore(HttpServerHarness.Root, HttpServerHarness.StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);
        var server = cookies is null
            ? new HttpProtocolServer(store, new AnonymousAuthenticationPolicy())
            : new HttpProtocolServer(store, new AnonymousAuthenticationPolicy(), false, cookies);
        var events = new RecordingSurlEventPublisher();
        var request = Encoding.ASCII.GetBytes("GET /file.txt HTTP/1.1\r\nHost: 127.0.0.1:18050\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nCookie: sess=xyz; sid=abc123\r\n\r\n");
        var connection = new InMemoryConnection(RecordedFixture.Whole(request));

        await server.ServeAsync(connection, HttpServerHarness.Context(new RecordingExchangeLog(), new ManualTimeProvider(HttpServerHarness.Now), TestContext.CancellationToken) with { Events = events });

        return (Encoding.Latin1.GetString(connection.WrittenBytes), events);
    }

    private sealed class FixedHandler : IHttpRequestHandler
    {
        public Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpAnswer(HttpStatus.NotFound, [new("Server", "surl")], HttpAnswerBody.None, false, false));
    }

    private sealed class UnreadRequestBody : IHttpRequestBody
    {
        public bool IsAnnounced => false;

        public long? AnnouncedLength => null;

        public bool IsReadable => false;

        public Task<HttpRequestBodyDiscardOutcome> DiscardAsync(System.Security.Cryptography.IncrementalHash? bodyHash, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The cookie handler discarded the request body.");

        public Task<HttpRequestBodyDiscardOutcome> CopyToAsync(Stream destination, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The cookie handler copied the request body.");
    }
}