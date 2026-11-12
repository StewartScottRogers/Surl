namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The upgrade of a polling session to a WebSocket, driven with packets written from EIO "Upgrade"
/// (ADR-0120, decision 2, and amendment 2).
/// </summary>
[TestClass]
public sealed class SocketIoWebSocketTransportTests
{
    private const string TokenPayload = SocketIoPollingClient.TokenPayload;

    private const string Code3 = "{\"code\":3,\"message\":\"Bad request\"}";

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task ProbeThenServeAsync_TwoProbeThenFive_AnswersThreeProbeAndNoopsThePollsThenMovesTheSession()
    {
        var harness = new SocketIoServerHarness();
        var polling = await SocketIoPollingClient.OpenAsync(harness);
        var held = polling.GetAsync();
        var webSocket = await SocketIoClient.ProbeAsync(harness, polling.SessionId);

        webSocket.SendText("2probe");

        Assert.AreEqual("3probe", (await webSocket.WaitForMessagesAsync(1))[0]);
        Assert.AreEqual("6", SocketIoPollingClient.Body(await held.WaitAsync(WaitLimit)));
        Assert.AreEqual("6", SocketIoPollingClient.Body(await polling.GetAsync()));
        webSocket.SendText("5");
        webSocket.SendText("40" + TokenPayload);
        Assert.StartsWith("40{\"sid\":\"", (await webSocket.WaitForMessagesAsync(2))[1]);
        Assert.EndsWith(Code3, await polling.GetAsync());
        Assert.EndsWith(Code3, await polling.PostAsync("6"));
        Assert.AreEqual(1, harness.Sessions.Count);
        await webSocket.EndAsync();
        await SocketIoPollingClient.WaitForSessionCountAsync(harness, 0);
    }

    [TestMethod]
    public async Task ProbeThenServeAsync_PacketsQueuedDuringTheUpgrade_AreSentOverTheWebSocket()
    {
        var harness = new SocketIoServerHarness();
        var polling = await SocketIoPollingClient.OpenAsync(harness);
        await polling.ConnectAsync();
        var webSocket = await SocketIoClient.ProbeAsync(harness, polling.SessionId);
        webSocket.SendText("2probe");
        await webSocket.WaitForMessagesAsync(1);

        await polling.PostAsync("42[\"queued\"]");
        webSocket.SendText("5");

        Assert.AreEqual("42[\"queued\"]", (await webSocket.WaitForMessagesAsync(2))[1]);
        await webSocket.EndAsync();
    }

    [TestMethod]
    public async Task ProbeThenServeAsync_WebSocketPingWhileProbing_IsAnsweredWithAPong()
    {
        var harness = new SocketIoServerHarness();
        var polling = await SocketIoPollingClient.OpenAsync(harness);
        var webSocket = await SocketIoClient.ProbeAsync(harness, polling.SessionId);

        webSocket.Connection.Send(SocketIoClient.ClientFrame(0x89, [7]));

        var written = await webSocket.Connection.WaitForWrittenAsync(bytes => SocketIoClient.Frames(bytes).Count > 0);
        var pong = SocketIoClient.Frames(written)[0];
        Assert.AreEqual(0xA, pong.Opcode);
        CollectionAssert.AreEqual(new byte[] { 7 }, pong.Payload);
        await webSocket.EndAsync();
    }

    [TestMethod]
    [DataRow("5")]
    [DataRow("2")]
    [DataRow("4binary")]
    public async Task ProbeThenServeAsync_FirstPacketNotTwoProbe_ClosesTheWebSocketWithProtocolErrorAndKeepsPolling(string text)
    {
        var harness = new SocketIoServerHarness();
        var polling = await SocketIoPollingClient.OpenAsync(harness);
        var webSocket = await SocketIoClient.ProbeAsync(harness, polling.SessionId);

        if (text == "4binary")
        {
            webSocket.SendBinary([1]);
        }
        else
        {
            webSocket.SendText(text);
        }

        Assert.AreEqual(1002, await webSocket.WaitForCloseCodeAsync());
        await webSocket.EndAsync();
        Assert.StartsWith("40{\"sid\":\"", await polling.ConnectAsync());
        var again = await SocketIoClient.ProbeAsync(harness, polling.SessionId);
        again.SendText("2probe");
        Assert.AreEqual("3probe", (await again.WaitForMessagesAsync(1))[0]);
        await again.EndAsync();
    }

    [TestMethod]
    public async Task ProbeThenServeAsync_PacketAfterThreeProbeNotFive_ClosesTheWebSocketAndPollsAreHeldAgain()
    {
        var harness = new SocketIoServerHarness();
        var polling = await SocketIoPollingClient.OpenAsync(harness);
        var webSocket = await SocketIoClient.ProbeAsync(harness, polling.SessionId);
        webSocket.SendText("2probe");
        await webSocket.WaitForMessagesAsync(1);

        webSocket.SendText("2probe");

        Assert.AreEqual(1002, await webSocket.WaitForCloseCodeAsync());
        await webSocket.EndAsync();
        Assert.StartsWith("40{\"sid\":\"", await polling.ConnectAsync());
    }

    [TestMethod]
    public async Task ProbeThenServeAsync_WebSocketCloseAfterThreeProbe_IsAnsweredWithANormalClose()
    {
        var harness = new SocketIoServerHarness();
        var polling = await SocketIoPollingClient.OpenAsync(harness);
        var webSocket = await SocketIoClient.ProbeAsync(harness, polling.SessionId);
        webSocket.SendText("2probe");
        await webSocket.WaitForMessagesAsync(1);

        webSocket.Connection.Send(SocketIoClient.ClientFrame(0x88, [0x03, 0xE8]));

        Assert.AreEqual(1000, await webSocket.WaitForCloseCodeAsync());
        await webSocket.EndAsync();
        Assert.AreEqual(1, harness.Sessions.Count);
    }

    [TestMethod]
    public async Task ProbeThenServeAsync_ClientGoneBeforeTwoProbe_WritesNothingAndKeepsTheSession()
    {
        var harness = new SocketIoServerHarness();
        var polling = await SocketIoPollingClient.OpenAsync(harness);
        var webSocket = await SocketIoClient.ProbeAsync(harness, polling.SessionId);
        var written = webSocket.Connection.WrittenBytes.Length;

        await webSocket.EndAsync();

        Assert.AreEqual(written, webSocket.Connection.WrittenBytes.Length);
        Assert.AreEqual(1, harness.Sessions.Count);
    }

    [TestMethod]
    public async Task ProbeThenServeAsync_ConnectionFailsWhileProbing_WritesNothing()
    {
        var harness = new SocketIoServerHarness();
        var polling = await SocketIoPollingClient.OpenAsync(harness);
        var webSocket = await SocketIoClient.ProbeAsync(harness, polling.SessionId);
        var written = webSocket.Connection.WrittenBytes.Length;

        webSocket.Connection.FailNextRead(new IOException("reset"));
        await webSocket.Serving.WaitAsync(WaitLimit);

        Assert.AreEqual(written, webSocket.Connection.WrittenBytes.Length);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("Upgrade abandoned: the connection failed", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ProbeThenServeAsync_SessionEndsWhileProbing_EndsTheWebSocketWithNothingWritten()
    {
        var harness = new SocketIoServerHarness();
        var polling = await SocketIoPollingClient.OpenAsync(harness);
        var webSocket = await SocketIoClient.ProbeAsync(harness, polling.SessionId);
        var written = webSocket.Connection.WrittenBytes.Length;

        await polling.PostAsync("1");
        await webSocket.Serving.WaitAsync(WaitLimit);

        Assert.AreEqual(written, webSocket.Connection.WrittenBytes.Length);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("ended during the upgrade", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task UpgradeAnswer_SessionAlreadyProbing_IsCode3()
    {
        var harness = new SocketIoServerHarness();
        var polling = await SocketIoPollingClient.OpenAsync(harness);
        var webSocket = await SocketIoClient.ProbeAsync(harness, polling.SessionId);

        var response = await harness.SendRawAsync(SocketIoClient.UpgradeRequest(SocketIoClient.WebSocketTarget + "&sid=" + polling.SessionId));

        Assert.EndsWith("\r\n\r\n" + Code3, response);
        await webSocket.EndAsync();
    }

    [TestMethod]
    public async Task UpgradeAnswer_WebSocketSession_IsCode3AndSoAreItsPolls()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectAsync(harness);
        var sessionId = client.OpenPacket()[9..29];
        var target = SocketIoPollingClient.PollingTarget + "&sid=" + sessionId;

        var upgrade = await harness.SendRawAsync(SocketIoClient.UpgradeRequest(SocketIoClient.WebSocketTarget + "&sid=" + sessionId));
        var poll = await harness.SendAsync("GET", target);
        var post = await harness.SendRawAsync($"POST {target} HTTP/1.1\r\nHost: h\r\nContent-Length: 1\r\nConnection: close\r\n\r\n6");

        Assert.EndsWith("\r\n\r\n" + Code3, upgrade);
        Assert.EndsWith("\r\n\r\n" + Code3, poll);
        Assert.EndsWith("\r\n\r\n" + Code3, post);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task UpgradeAnswer_WithSidButNotAnUpgrade_IsCode3()
    {
        var harness = new SocketIoServerHarness();
        var polling = await SocketIoPollingClient.OpenAsync(harness);

        var response = await harness.SendAsync("GET", SocketIoClient.WebSocketTarget + "&sid=" + polling.SessionId);

        Assert.EndsWith("\r\n\r\n" + Code3, response);
    }
}
