using System.Text;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The HTTP requests an Engine.IO client sends before its session, written from EIO "Request path",
/// "Query parameters", "Handshake" and "WebSocket" and RFC 6455 section 4.1 (ADR-0120, decisions 1,
/// 2 and 7).
/// </summary>
[TestClass]
public sealed class SocketIoRequestAnswererTests
{
    private const string UpgradeFields = "Upgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: " + SocketIoClient.Key + "\r\nSec-WebSocket-Version: 13";

    [TestMethod]
    [DataRow("/socket.io?EIO=4&transport=websocket")]
    [DataRow("/admin/?EIO=4&transport=websocket")]
    [DataRow("/")]
    public async Task AnswerAsync_PathOtherThanSocketIo_Is404WithNoBody(string target)
    {
        var harness = new SocketIoServerHarness();

        var response = await harness.SendAsync("GET", target, UpgradeFields);

        Assert.AreEqual("HTTP/1.1 404 Not Found\r\nDate: " + SocketIoServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", response);
    }

    [TestMethod]
    [DataRow("GET", "/socket.io/?EIO=4", "{\"code\":0,\"message\":\"Transport unknown\"}")]
    [DataRow("GET", "/socket.io/?EIO=4&transport=polling", "{\"code\":0,\"message\":\"Transport unknown\"}")]
    [DataRow("GET", "/socket.io/?EIO=4&transport=webtransport", "{\"code\":0,\"message\":\"Transport unknown\"}")]
    [DataRow("GET", "/socket.io/?EIO=4&transport=websocket&sid=lv_VI97HAXpY6yYWAAAC", "{\"code\":1,\"message\":\"Session ID unknown\"}")]
    [DataRow("POST", "/socket.io/?EIO=4&transport=websocket", "{\"code\":2,\"message\":\"Bad handshake method\"}")]
    [DataRow("GET", "/socket.io/?transport=websocket", "{\"code\":5,\"message\":\"Unsupported protocol version\"}")]
    [DataRow("GET", "/socket.io/?EIO=3&transport=websocket", "{\"code\":5,\"message\":\"Unsupported protocol version\"}")]
    public async Task AnswerAsync_RefusedQuery_Is400WithItsCodedBody(string method, string target, string body)
    {
        var harness = new SocketIoServerHarness();

        var response = await harness.SendAsync(method, target, UpgradeFields);

        Assert.AreEqual(
            "HTTP/1.1 400 Bad Request\r\nDate: " + SocketIoServerHarness.NowHttpDate + "\r\nContent-Length: " + body.Length
            + "\r\nContent-Type: application/json\r\nConnection: close\r\n\r\n" + body,
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_WebSocketRequestThatIsNotAnUpgrade_IsCode3()
    {
        var harness = new SocketIoServerHarness();

        var response = await harness.SendAsync("GET", SocketIoClient.WebSocketTarget);

        Assert.StartsWith("HTTP/1.1 400 Bad Request\r\n", response);
        Assert.EndsWith("\r\n\r\n{\"code\":3,\"message\":\"Bad request\"}", response);
    }

    [TestMethod]
    public async Task AnswerAsync_SecondWebSocketForALiveSession_IsCode3()
    {
        var harness = new SocketIoServerHarness();
        var sessionId = harness.Sessions.TryReserve()!;

        var response = await harness.SendAsync("GET", SocketIoClient.WebSocketTarget + "&sid=" + sessionId, UpgradeFields);

        Assert.EndsWith("\r\n\r\n{\"code\":3,\"message\":\"Bad request\"}", response);
    }

    [TestMethod]
    public async Task AnswerAsync_UpgradeWithAWrongVersion_IsRefusedAsSurlWebSocketDoes()
    {
        var harness = new SocketIoServerHarness();

        var response = await harness.SendAsync("GET", SocketIoClient.WebSocketTarget, "Upgrade: websocket", "Connection: Upgrade", "Sec-WebSocket-Key: " + SocketIoClient.Key, "Sec-WebSocket-Version: 8");

        Assert.StartsWith("HTTP/1.1 426 Upgrade Required\r\n", response);
        Assert.Contains("Sec-WebSocket-Version: 13\r\n", response);
        Assert.AreEqual(0, harness.Sessions.Count);
    }

    [TestMethod]
    public async Task AnswerAsync_ListenerHolding1024Sessions_Is503WithNoBody()
    {
        var harness = new SocketIoServerHarness();
        for (var session = 0; session < SocketIoSessions.MaxSessions; session++)
        {
            harness.Sessions.TryReserve();
        }

        var response = await harness.SendAsync("GET", SocketIoClient.WebSocketTarget, UpgradeFields);

        Assert.AreEqual("HTTP/1.1 503 Service Unavailable\r\nDate: " + SocketIoServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_WebSocketHandshake_Is101WithTheAcceptKeyThenTheSessionIsForgottenAtItsEnd()
    {
        var harness = new SocketIoServerHarness();

        var client = await SocketIoClient.ConnectAsync(harness);

        Assert.StartsWith(
            "HTTP/1.1 101 Switching Protocols\r\nDate: " + SocketIoServerHarness.NowHttpDate + "\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + SocketIoClient.Accept + "\r\n\r\n",
            Encoding.ASCII.GetString(client.Connection.WrittenBytes));
        Assert.AreEqual(1, harness.Sessions.Count);
        await client.EndAsync();
        Assert.AreEqual(0, harness.Sessions.Count);
    }

    [TestMethod]
    public async Task AnswerAsync_AbsoluteFormTargetWithAnAuthorizationField_IsUpgradedWithoutJudgingIt()
    {
        var harness = new SocketIoServerHarness();
        var connection = new UnitTestDuplexConnection();
        connection.Send(Encoding.ASCII.GetBytes(SocketIoClient.UpgradeRequest("http://127.0.0.1:5000" + SocketIoClient.WebSocketTarget + "&t=OXz8Xk", "Authorization: Bearer nonsense\r\n")));

        var serving = harness.Server.ServeAsync(connection, harness.Context());
        await connection.WaitForWrittenAsync(bytes => bytes.Length > 0);
        connection.HalfClose();
        await serving.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.StartsWith("HTTP/1.1 101 Switching Protocols\r\n", Encoding.ASCII.GetString(connection.WrittenBytes));
        Assert.IsEmpty(harness.Policy.Logins);
    }

    [TestMethod]
    public async Task AnswerAsync_RefusalWithABody_LeavesItUnread()
    {
        var harness = new SocketIoServerHarness();

        var response = await harness.SendRawAsync("POST /socket.io/?EIO=4&transport=polling HTTP/1.1\r\nHost: h\r\nContent-Length: 2\r\n\r\nok");

        Assert.StartsWith("HTTP/1.1 400 Bad Request\r\n", response);
        Assert.Contains("Connection: close\r\n", response);
    }
}
