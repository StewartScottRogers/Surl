using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ws.WsServerHarness;

namespace Surl.Protocol.Ws;

[TestClass]
public sealed class WsProtocolServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RecordedUpgradeRequest_IsAnswered101ByteForByte_ThenAnEmptyCloseAndAHalfClose()
    {
        var (connection, log) = await ServeAsync([RecordedFixture.ReadRequestBytes("upgrade-101")], TestContext.CancellationToken);

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("WebSocket upgrade accepted for /chat", log.Notes[0]);
    }

    [TestMethod]
    public async Task RecordedUpgradeRequest_OneBytePerRead_IsAnsweredTheSame()
    {
        var (connection, _) = await ServeAsync(RecordedFixture.OneBytePerRead(RecordedFixture.ReadRequestBytes("upgrade-101")), TestContext.CancellationToken);

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public void AcceptKey_IsTheRfc6455AcceptOfTheRecordedKey()
    {
        var recordedKey = RecordedUpgradeRequest().Split("\r\n").Single(line => line.StartsWith("Sec-WebSocket-Key: ", StringComparison.Ordinal))["Sec-WebSocket-Key: ".Length..];

        Assert.AreEqual("szGrtY8MlApNe9oYLpNz0A==", recordedKey);
        Assert.AreEqual("ktpdlwK4CWD8HWwKyLX0kug7bZ8=", WebSocketAcceptKey.Compute(recordedKey));
    }

    [TestMethod]
    public async Task QueryString_IsLeftOutOfThePathLookedUp()
    {
        var (connection, log) = await ServeAsync([RecordedUpgradeRequestWith("GET /chat ", "GET /chat?room=1 ")], TestContext.CancellationToken);

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
        Assert.AreEqual("WebSocket upgrade accepted for /chat", log.Notes[0]);
    }

    [TestMethod]
    public async Task SubprotocolAndExtensionOffers_AreIgnored_AndThe101NamesNeither()
    {
        var request = RecordedUpgradeRequestWith("Connection: Upgrade\r\n", "Connection: Upgrade\r\nSec-WebSocket-Protocol: chat\r\nSec-WebSocket-Extensions: permessage-deflate\r\n");

        var (connection, _) = await ServeAsync([request], TestContext.CancellationToken);

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task TokenLists_AreMatchedWithoutRegardToCase_AnywhereInTheList()
    {
        var request = Latin1Bytes(RecordedUpgradeRequest()
            .Replace("Upgrade: websocket", "Upgrade: h2c, WebSocket", StringComparison.Ordinal)
            .Replace("Connection: Upgrade", "Connection: keep-alive,\tupgrade", StringComparison.Ordinal));

        var (connection, _) = await ServeAsync([request], TestContext.CancellationToken);

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task RecordedHeadRequest_IsAnswered405WithAllowGet_AsRecorded()
    {
        var (connection, log) = await ServeAsync([RecordedFixture.ReadRequestBytes("head-405")], TestContext.CancellationToken);

        Assert.AreEqual(Refusal("405 Method Not Allowed", "Allow: GET"), Latin1(connection.WrittenBytes));
        Assert.IsTrue(Latin1(RecordedFixture.ReadBytes("head-405", "transcript.txt")).Contains("< Allow: GET", StringComparison.Ordinal));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("WebSocket upgrade refused: 405 Method Not Allowed: the method is not GET; closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task RecordedVersion8Request_IsAnswered426WithVersion13_AsRecorded()
    {
        var (connection, log) = await ServeAsync([RecordedFixture.ReadRequestBytes("version-8-426")], TestContext.CancellationToken);

        Assert.AreEqual(Refusal("426 Upgrade Required", "Sec-WebSocket-Version: 13"), Latin1(connection.WrittenBytes));
        Assert.AreEqual("WebSocket upgrade refused: 426 Upgrade Required: Sec-WebSocket-Version is not 13; closed.", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("Sec-WebSocket-Version: 13\r\n", "", DisplayName = "missing")]
    [DataRow("Sec-WebSocket-Version: 13\r\n", "Sec-WebSocket-Version: 13\r\nSec-WebSocket-Version: 13\r\n", DisplayName = "repeated")]
    public async Task VersionNotExactlyOne13_IsAnswered426WithVersion13(string oldText, string newText)
    {
        var (connection, _) = await ServeAsync([RecordedUpgradeRequestWith(oldText, newText)], TestContext.CancellationToken);

        Assert.AreEqual(Refusal("426 Upgrade Required", "Sec-WebSocket-Version: 13"), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("Upgrade: websocket\r\n", "", DisplayName = "missing")]
    [DataRow("Upgrade: websocket\r\n", "Upgrade: h2c\r\n", DisplayName = "another protocol")]
    public async Task UpgradeNotListingWebSocket_IsAnswered426WithUpgradeAndVersion(string oldText, string newText)
    {
        var (connection, log) = await ServeAsync([RecordedUpgradeRequestWith(oldText, newText)], TestContext.CancellationToken);

        Assert.AreEqual(Refusal("426 Upgrade Required", "Upgrade: websocket", "Sec-WebSocket-Version: 13"), Latin1(connection.WrittenBytes));
        Assert.AreEqual("WebSocket upgrade refused: 426 Upgrade Required: Upgrade does not list websocket; closed.", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("Host: 127.0.0.1:18301\r\n", "", "the request does not carry exactly one Host field", DisplayName = "no Host")]
    [DataRow("Host: 127.0.0.1:18301\r\n", "Host: a\r\nHost: b\r\n", "the request does not carry exactly one Host field", DisplayName = "two Hosts")]
    [DataRow(" HTTP/1.1\r\n", " HTTP/1.0\r\n", "the version is not HTTP/1.1", DisplayName = "HTTP/1.0")]
    [DataRow("Connection: Upgrade\r\n", "", "Connection does not list Upgrade", DisplayName = "no Connection")]
    [DataRow("Connection: Upgrade\r\n", "Connection: keep-alive\r\n", "Connection does not list Upgrade", DisplayName = "Connection without Upgrade")]
    [DataRow("Connection: Upgrade\r\n", "Connection: Upgrade\r\nContent-Length: 5\r\n", "the request announces a body", DisplayName = "Content-Length")]
    [DataRow("Connection: Upgrade\r\n", "Connection: Upgrade\r\nTransfer-Encoding: chunked\r\n", "the request announces a body", DisplayName = "chunked")]
    [DataRow("Connection: Upgrade\r\n", "Connection: Upgrade\r\nContent-Length: x\r\n", "the request announces a body", DisplayName = "invalid Content-Length")]
    [DataRow("Sec-WebSocket-Key: szGrtY8MlApNe9oYLpNz0A==\r\n", "", "Sec-WebSocket-Key is not exactly one base64 value of 16 bytes", DisplayName = "no key")]
    [DataRow("Sec-WebSocket-Key: szGrtY8MlApNe9oYLpNz0A==\r\n", "Sec-WebSocket-Key: szGrtY8MlApNe9oYLpNz0A==\r\nSec-WebSocket-Key: szGrtY8MlApNe9oYLpNz0A==\r\n", "Sec-WebSocket-Key is not exactly one base64 value of 16 bytes", DisplayName = "two keys")]
    [DataRow("Sec-WebSocket-Key: szGrtY8MlApNe9oYLpNz0A==\r\n", "Sec-WebSocket-Key: not base64!\r\n", "Sec-WebSocket-Key is not exactly one base64 value of 16 bytes", DisplayName = "not base64")]
    [DataRow("Sec-WebSocket-Key: szGrtY8MlApNe9oYLpNz0A==\r\n", "Sec-WebSocket-Key: AAAAAAAAAAAAAAAAAAAA\r\n", "Sec-WebSocket-Key is not exactly one base64 value of 16 bytes", DisplayName = "15 bytes")]
    [DataRow("Sec-WebSocket-Key: szGrtY8MlApNe9oYLpNz0A==\r\n", "Sec-WebSocket-Key:\r\n", "Sec-WebSocket-Key is not exactly one base64 value of 16 bytes", DisplayName = "empty key")]
    public async Task DefectiveField_IsAnswered400AndClosed(string oldText, string newText, string failedCheck)
    {
        var (connection, log) = await ServeAsync([RecordedUpgradeRequestWith(oldText, newText)], TestContext.CancellationToken);

        Assert.AreEqual(Refusal("400 Bad Request"), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual($"WebSocket upgrade refused: 400 Bad Request: {failedCheck}; closed.", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("/missing", "nothing exists at /missing", DisplayName = "missing")]
    [DataRow("/a/%2e%2e/chat", "the path /a/%2e%2e/chat was refused (DotSegment)", DisplayName = "refused")]
    [DataRow("/sub", "/sub is a directory and directory listings are off", DisplayName = "directory")]
    public async Task PathNotServed_IsAnswered404(string path, string failedCheck)
    {
        var (connection, log) = await ServeAsync([RecordedUpgradeRequestWith("GET /chat ", $"GET {path} ")], TestContext.CancellationToken);

        Assert.AreEqual(Refusal("404 Not Found"), Latin1(connection.WrittenBytes));
        Assert.AreEqual($"WebSocket upgrade refused: 404 Not Found: {failedCheck}; closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task Directory_WithListingsOn_IsUpgraded()
    {
        var (connection, log) = await ServeAsync([RecordedUpgradeRequestWith("GET /chat ", "GET /sub/ ")], TestContext.CancellationToken, listDirectories: true);

        Assert.AreEqual(Recorded101Head + "\u0081\u0000" + "\u0088\u0000", Latin1(connection.WrittenBytes));
        Assert.AreEqual("WebSocket upgrade accepted for /sub/", log.Notes[0]);
    }

    [TestMethod]
    public async Task MethodCheck_ComesBeforeThePathCheck()
    {
        var (connection, _) = await ServeAsync([RecordedUpgradeRequestWith("GET /chat ", "POST /missing ")], TestContext.CancellationToken);

        Assert.AreEqual(Refusal("405 Method Not Allowed", "Allow: GET"), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public void Schemes_IsWs()
    {
        CollectionAssert.AreEqual(new[] { "ws" }, Server().Schemes.ToArray());
    }

    [TestMethod]
    public void Constructor_RefusesNulls()
    {
        var store = new ContentStore(Root, StandardFileSystem(), new ContentExposureOptions());

        Assert.ThrowsExactly<ArgumentNullException>(() => new WsProtocolServer(null!, new AnonymousAuthenticationPolicy()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WsProtocolServer(store, null!));
    }

    [TestMethod]
    public async Task ServeAsync_RefusesNulls()
    {
        var context = Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Server().ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Server().ServeAsync(new InMemoryConnection([]), null!));
    }
}
