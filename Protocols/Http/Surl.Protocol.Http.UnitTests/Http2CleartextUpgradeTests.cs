using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.Http2TestClient;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// <c>Upgrade: h2c</c> (RFC 7540 section 3.2; ADR-0081, decision 3; ADR-0091), replayed from the
/// requests the pinned static curl 8.21.0 build sent for <c>--http2 http://</c>.
/// </summary>
[TestClass]
public sealed class Http2CleartextUpgradeTests
{
    // What ADR-0081 measured curl completing the upgrade against, and what the fixture recorded.
    private const string SwitchingProtocolsHead = "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n";

    // The 18 SETTINGS bytes curl sends in HTTP2-Settings and again in its preface.
    private const string CurlSettings = "AAMAAABkAAQAAQAAAAIAAAAA";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_RecordedCurlHttp2Upgrade_Answers101ThenTheFileOnStream1()
    {
        var (connection, log) = await ServeAsync([RecordedFixture.ReadRequestBytes("h2c-upgrade-get-file")], TestContext.CancellationToken);

        AssertUpgradedAndAnsweredTheFile(connection);
        CollectionAssert.Contains(log.Notes.ToArray(), "The client sent GOAWAY NoError, last stream 0.", "The client's preface and frames after the 101 were accepted.");
    }

    [TestMethod]
    public async Task ServeAsync_RecordedCurlHttp2UpgradeOneBytePerRead_Answers101ThenTheFileOnStream1()
    {
        var (connection, _) = await ServeAsync(RecordedFixture.OneBytePerRead(RecordedFixture.ReadRequestBytes("h2c-upgrade-get-file")), TestContext.CancellationToken);

        AssertUpgradedAndAnsweredTheFile(connection);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedCurlHttp2UpgradeWithABody_IgnoresTheUpgradeAndAnswersHttp11()
    {
        var (connection, _) = await ServeAsync([RecordedFixture.ReadRequestBytes("h2c-upgrade-post-body")], TestContext.CancellationToken, peerHalfCloses: false);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 405 Method Not Allowed\r\n");
    }

    [TestMethod]
    [DataRow("!!!!", DisplayName = "not base64url")]
    [DataRow("AAMAAAB", DisplayName = "5 bytes, not whole SETTINGS entries")]
    [DataRow("AAIAAAAC", DisplayName = "ENABLE_PUSH 2, a value SETTINGS refuses")]
    public async Task ServeAsync_MalformedHttp2Settings_IgnoresTheUpgradeAndAnswersHttp11(string settings)
    {
        var (connection, _) = await ServeAsync([UpgradeRequest(settings: settings)], TestContext.CancellationToken);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("1.0", "Upgrade, HTTP2-Settings", "", "", DisplayName = "HTTP/1.0 (RFC 9110 section 7.8)")]
    [DataRow("1.1", "Upgrade", "", "", DisplayName = "Connection does not name HTTP2-Settings")]
    [DataRow("1.1", "HTTP2-Settings", "", "", DisplayName = "Connection does not name Upgrade")]
    [DataRow("1.1", "Upgrade, HTTP2-Settings", "HTTP2-Settings: AAMAAABk\r\n", "", DisplayName = "two HTTP2-Settings fields")]
    [DataRow("1.1", "Upgrade, HTTP2-Settings", "Transfer-Encoding: chunked\r\n", "0\r\n\r\n", DisplayName = "a chunked body")]
    public async Task ServeAsync_UpgradeTheAdrDoesNotAccept_IsAnsweredHttp1(string version, string connectionField, string extraFields, string body)
    {
        var request = Concat(UpgradeRequest(version, connectionField, extraFields), Ascii(body));

        var (connection, _) = await ServeAsync([request], TestContext.CancellationToken);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_UpgradeWithContentLengthZero_IsUpgraded()
    {
        var request = UpgradeRequest(extraFields: "Content-Length: 0\r\n");

        var (connection, _) = await ServeAsync([Concat(request, Start())], TestContext.CancellationToken);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), SwitchingProtocolsHead);
        Assert.AreEqual(FileBody, Responses(connection.WrittenBytes[SwitchingProtocolsHead.Length..])[1].BodyText);
    }

    [TestMethod]
    public async Task ServeAsync_UpgradeOverTls_IsAnsweredHttp11()
    {
        var connection = new InMemoryConnection([UpgradeRequest()], initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);
        var clock = new ManualTimeProvider(Now);

        await Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, TestContext.CancellationToken));

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_Http2SettingsInitialWindowSize_IsAppliedAsTheClientsFirstSettings()
    {
        // INITIAL_WINDOW_SIZE (4) = 5: stream 1 may carry 5 bytes until the client grants more.
        var request = Concat(UpgradeRequest(settings: "AAQAAAAF"), Start());

        var (connection, _) = await ServeAsync([request], TestContext.CancellationToken);

        var written = connection.WrittenBytes[SwitchingProtocolsHead.Length..];
        Assert.AreEqual(FileBody[..5], Responses(written)[1].BodyText);
        Assert.AreEqual(1, Frames(written).OfType<Http2SettingsFrame>().Count(frame => frame.Ack), "Only the client's preface SETTINGS is acknowledged; the 101 acknowledged HTTP2-Settings.");
    }

    [TestMethod]
    public async Task ServeAsync_BytesOtherThanThePrefaceAfterThe101_EndWithGoAwayProtocolError()
    {
        var request = Concat(UpgradeRequest(), Ascii("GET / HTTP/1.1\r\nHost: a\r\n\r\n"));

        var (connection, log) = await ServeAsync([request], TestContext.CancellationToken);

        var written = connection.WrittenBytes[SwitchingProtocolsHead.Length..];
        var goAway = GoAway(written)!;
        Assert.AreEqual(0, goAway.LastStreamId, "Stream 1 was not processed.");
        Assert.AreEqual(Http2ErrorCode.ProtocolError, goAway.ErrorCode);
        Assert.IsFalse(Responses(written).ContainsKey(1));
        CollectionAssert.Contains(log.Notes.ToArray(), "after the 101 the client sent 24 bytes that are not the HTTP/2 connection preface; answered GOAWAY ProtocolError and closed.");
    }

    [TestMethod]
    [DataRow(null, DisplayName = "a request another HTTP version carried")]
    [DataRow("2.0", DisplayName = "a version past HTTP/1.x")]
    public void ClientSettingsOf_RequestThatIsNotHttp11_IgnoresTheUpgrade(string? version)
    {
        var request = new HttpRequest(
            "GET",
            "/file.txt",
            "127.0.0.1",
            [new("Upgrade", "h2c"), new("HTTP2-Settings", CurlSettings), new("Connection", "Upgrade, HTTP2-Settings")],
            new NoRequestBody(),
            version is null ? null : Version.Parse(version));

        Assert.IsNull(Http2CleartextUpgrade.ClientSettingsOf(request));
    }

    private static byte[] UpgradeRequest(string version = "1.1", string connectionField = "Upgrade, HTTP2-Settings", string extraFields = "", string settings = CurlSettings) => Ascii(
        $"GET /file.txt HTTP/{version}\r\nHost: 127.0.0.1:18050\r\nUpgrade: h2c\r\nHTTP2-Settings: {settings}\r\n{extraFields}Connection: {connectionField}\r\n\r\n");

    private static void AssertUpgradedAndAnsweredTheFile(InMemoryConnection connection)
    {
        var written = connection.WrittenBytes;
        Assert.AreEqual(SwitchingProtocolsHead, Latin1(written[..SwitchingProtocolsHead.Length]));
        var frames = written[SwitchingProtocolsHead.Length..];
        CollectionAssert.AreEqual(DefaultServerSettings, frames[..DefaultServerSettings.Length], "The server's SETTINGS follows the 101 at once.");
        Assert.IsTrue(Frames(frames).Any(frame => frame is Http2SettingsFrame { Ack: true }), "The client's preface SETTINGS is acknowledged.");
        var response = Responses(frames)[1];
        Assert.AreEqual("200", response.Status);
        Assert.AreEqual(FileBody, response.BodyText);
        Assert.IsTrue(response.EndedOnData);
        Assert.IsNull(GoAway(frames), "The client closed; the server sends no GOAWAY of its own.");
    }

    // A request with no body: the upgrade decision reads only the request's version and fields.
    private sealed class NoRequestBody : IHttpRequestBody
    {
        public bool IsAnnounced => false;

        public long? AnnouncedLength => null;

        public bool IsReadable => false;

        public Task<HttpRequestBodyDiscardOutcome> DiscardAsync(System.Security.Cryptography.IncrementalHash? bodyHash, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The upgrade decision discarded the request body.");

        public Task<HttpRequestBodyDiscardOutcome> CopyToAsync(Stream destination, CancellationToken cancellationToken) =>
            throw new AssertFailedException("The upgrade decision copied the request body.");
    }
}
