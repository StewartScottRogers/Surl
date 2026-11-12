using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The HTTP long-polling transport, driven with requests written from EIO "HTTP long-polling"
/// ("Request path", "Query parameters", "Headers", "Sending and receiving data") and "Packet encoding"
/// (ADR-0120, decisions 2 and 7, and amendment 2).
/// </summary>
[TestClass]
public sealed partial class SocketIoPollingAnswererTests
{
    private const string TokenPayload = SocketIoPollingClient.TokenPayload;

    private const string BinaryEvent = "451-[\"baz\",{\"_placeholder\":true,\"num\":0}]";

    private const string Code1 = "{\"code\":1,\"message\":\"Session ID unknown\"}";

    private const string Code3 = "{\"code\":3,\"message\":\"Bad request\"}";

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task Open_HandshakeGet_Is200WithTheOpenPacketOfferingTheWebSocketUpgrade()
    {
        var harness = new SocketIoServerHarness();

        var client = await SocketIoPollingClient.OpenAsync(harness);

        Assert.StartsWith(
            "HTTP/1.1 200 OK\r\nDate: " + SocketIoServerHarness.NowHttpDate + "\r\nContent-Length: 118\r\nContent-Type: text/plain; charset=UTF-8\r\n",
            client.OpenResponse);
        Assert.MatchesRegex(PollingOpenPacketPattern(), SocketIoPollingClient.Body(client.OpenResponse));
        Assert.AreEqual(1, harness.Sessions.Count);
    }

    [TestMethod]
    public async Task Open_ListenerHolding1024Sessions_Is503WithNoBody()
    {
        var harness = new SocketIoServerHarness();
        for (var session = 0; session < SocketIoSessions.MaxSessions; session++)
        {
            harness.Sessions.TryReserve();
        }

        var response = await harness.SendAsync("GET", SocketIoPollingClient.PollingTarget);

        Assert.AreEqual("HTTP/1.1 503 Service Unavailable\r\nDate: " + SocketIoServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", response);
    }

    [TestMethod]
    public async Task Post_PacketsJoinedByRecordSeparator_IsOkAndEachIsAnsweredOnTheNextPoll()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);

        var post = await client.PostAsync("40" + TokenPayload + "\u001e42[\"hello\",1]");
        var poll = await client.GetAsync();

        Assert.AreEqual("HTTP/1.1 200 OK\r\nDate: " + SocketIoServerHarness.NowHttpDate + "\r\nContent-Length: 2\r\nContent-Type: text/html\r\nConnection: close\r\n\r\nok", post);
        Assert.StartsWith("HTTP/1.1 200 OK\r\n", poll);
        Assert.Contains("Content-Type: text/plain; charset=UTF-8\r\n", poll);
        Assert.MatchesRegex(ConnectThenEchoPattern(), SocketIoPollingClient.Body(poll));
    }

    [TestMethod]
    public async Task Poll_NothingQueued_IsHeldUntilAPacketIsQueued()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);

        var held = client.GetAsync();
        await Task.Delay(50);
        Assert.IsFalse(held.IsCompleted);
        await client.PostAsync("40" + TokenPayload);

        Assert.StartsWith("40{\"sid\":\"", SocketIoPollingClient.Body(await held.WaitAsync(WaitLimit)));
    }

    [TestMethod]
    public async Task Poll_BinaryEventAndItsAttachment_AreEchoedWithTheAttachmentAsBPrefixedBase64()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);
        await client.ConnectAsync();

        await client.PostAsync(BinaryEvent + "\u001ebAQID");

        Assert.AreEqual(BinaryEvent + "\u001ebAQID", SocketIoPollingClient.Body(await client.GetAsync()));
    }

    [TestMethod]
    public async Task Post_OctetStreamBody_IsOneBinaryPacket()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);
        await client.ConnectAsync();

        await client.PostAsync(BinaryEvent);
        var post = await client.PostAsync("\u0001\u0002\u0003", "application/octet-stream");

        Assert.EndsWith("\r\n\r\nok", post);
        Assert.AreEqual(BinaryEvent + "\u001ebAQID", SocketIoPollingClient.Body(await client.GetAsync()));
    }

    [TestMethod]
    [DataRow("GET")]
    [DataRow("POST")]
    public async Task AnswerAsync_UnknownSid_IsCode1(string method)
    {
        var harness = new SocketIoServerHarness();

        var response = await harness.SendAsync(method, SocketIoPollingClient.PollingTarget + "&sid=lv_VI97HAXpY6yYWAAAC");

        Assert.StartsWith("HTTP/1.1 400 Bad Request\r\n", response);
        Assert.EndsWith("\r\n\r\n" + Code1, response);
    }

    [TestMethod]
    public async Task AnswerAsync_SidReservedForAWebSocket_IsCode3()
    {
        var harness = new SocketIoServerHarness();
        var sessionId = harness.Sessions.TryReserve()!;

        var response = await harness.SendAsync("GET", SocketIoPollingClient.PollingTarget + "&sid=" + sessionId);

        Assert.EndsWith("\r\n\r\n" + Code3, response);
    }

    [TestMethod]
    public async Task AnswerAsync_MethodOtherThanGetOrPost_IsCode3()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);

        var response = await harness.SendAsync("PUT", client.Target);

        Assert.EndsWith("\r\n\r\n" + Code3, response);
        Assert.AreEqual(1, harness.Sessions.Count);
    }

    [TestMethod]
    public async Task Poll_SecondConcurrentGet_IsCode3AndTheSessionIsClosed()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);

        var first = client.GetAsync();
        var second = client.GetAsync();
        string[] bodies = [SocketIoPollingClient.Body(await first.WaitAsync(WaitLimit)), SocketIoPollingClient.Body(await second.WaitAsync(WaitLimit))];

        CollectionAssert.AreEquivalent(new[] { "1", Code3 }, bodies);
        await SocketIoPollingClient.WaitForSessionCountAsync(harness, 0);
        Assert.EndsWith(Code1, await client.GetAsync());
    }

    [TestMethod]
    public async Task Post_SecondConcurrentPost_IsCode3AndTheSessionIsClosed()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);
        var slow = new UnitTestDuplexConnection();
        slow.Send(System.Text.Encoding.ASCII.GetBytes($"POST {client.Target} HTTP/1.1\r\nHost: h\r\nContent-Length: 10\r\nConnection: close\r\n\r\n6"));
        var slowServing = harness.Server.ServeAsync(slow, harness.Context());

        var giveUpAt = DateTime.UtcNow + WaitLimit;
        string response;
        do
        {
            Assert.IsLessThan(giveUpAt, DateTime.UtcNow, "The second post was never refused.");
            response = await client.PostAsync("6");
        }
        while (response.EndsWith("ok", StringComparison.Ordinal));

        Assert.EndsWith("\r\n\r\n" + Code3, response);
        await SocketIoPollingClient.WaitForSessionCountAsync(harness, 0);
        slow.HalfClose();
        await slowServing.WaitAsync(WaitLimit);
    }

    [TestMethod]
    [DataRow("x")]
    [DataRow("0")]
    [DataRow("bAQID")]
    [DataRow("")]
    public async Task Post_BodyThatIsAProtocolError_IsCode3AndTheSessionIsClosed(string body)
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);

        var response = await client.PostAsync(body);

        Assert.EndsWith("\r\n\r\n" + Code3, response);
        await SocketIoPollingClient.WaitForSessionCountAsync(harness, 0);
    }

    [TestMethod]
    public async Task Post_WithoutABody_IsCode3AndTheSessionIsClosed()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);

        var response = await harness.SendAsync("POST", client.Target);

        Assert.EndsWith("\r\n\r\n" + Code3, response);
        await SocketIoPollingClient.WaitForSessionCountAsync(harness, 0);
    }

    [TestMethod]
    [DataRow("Transfer-Encoding: chunked\r\n\r\n5\r\nab")]
    [DataRow("Transfer-Encoding: gzip, chunked\r\n\r\n")]
    public async Task Post_BodyNotReadWhole_IsCode3AndTheSessionIsClosed(string fieldsAndBody)
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);

        var response = await harness.SendRawAsync($"POST {client.Target} HTTP/1.1\r\nHost: h\r\n{fieldsAndBody}");

        Assert.Contains(Code3, response);
        await SocketIoPollingClient.WaitForSessionCountAsync(harness, 0);
    }

    [TestMethod]
    public async Task Post_EngineIoClose_IsOkAndEndsTheSession()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);

        var response = await client.PostAsync("1");

        Assert.EndsWith("\r\n\r\nok", response);
        await SocketIoPollingClient.WaitForSessionCountAsync(harness, 0);
    }

    [TestMethod]
    [DataRow("Content-Length: 11\r\n\r\n42[\"hello\"]")]
    [DataRow("Transfer-Encoding: chunked\r\n\r\nb\r\n42[\"hello\"]\r\n0\r\n\r\n")]
    public async Task Post_BodyPastMaxPayload_Is413AndTheSessionIsClosed(string fieldsAndBody)
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, maxMessageBytes: 10, 104857600);

        var response = await harness.SendRawAsync($"POST {client.Target} HTTP/1.1\r\nHost: h\r\n{fieldsAndBody}", harness.Context(limits));

        Assert.StartsWith("HTTP/1.1 413 Content Too Large\r\n", response);
        await SocketIoPollingClient.WaitForSessionCountAsync(harness, 0);
    }

    [TestMethod]
    public async Task Post_BodyPastTheUploadLimitWithNoMessageLimit_Is413()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, maxMessageBytes: 0, maxUploadBytes: 4);

        var response = await harness.SendRawAsync(
            $"POST {client.Target} HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\n\r\nb\r\n42[\"hello\"]\r\n0\r\n\r\n",
            harness.Context(limits));

        Assert.StartsWith("HTTP/1.1 413 Content Too Large\r\n", response);
    }

    [TestMethod]
    public async Task Post_AttachmentsPastMaxPayload_Is413()
    {
        var harness = new SocketIoServerHarness();
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, maxMessageBytes: 60, 104857600);
        var client = await SocketIoPollingClient.OpenAsync(harness, harness.Context(limits));
        await client.ConnectAsync();
        await client.PostAsync(BinaryEvent);

        var response = await client.PostAsync(new string('x', 30), "application/octet-stream");

        Assert.StartsWith("HTTP/1.1 413 Content Too Large\r\n", response);
    }

    [TestMethod]
    public async Task Poll_Heartbeat_AnswersTheHeldPollWithAPingThenTheEndWithAClose()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoPollingClient.OpenAsync(harness);

        var held = client.GetAsync();
        await harness.Clock.AdvanceWhenWaitingAsync(SocketIoSession.PingInterval);
        Assert.AreEqual("2", SocketIoPollingClient.Body(await held.WaitAsync(WaitLimit)));
        await client.PostAsync("3");
        held = client.GetAsync();
        await harness.Clock.AdvanceWhenWaitingAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual("1", SocketIoPollingClient.Body(await held.WaitAsync(WaitLimit)));
        await SocketIoPollingClient.WaitForSessionCountAsync(harness, 0);
    }

    [TestMethod]
    public async Task Open_ServerShuttingDown_EndsTheSession()
    {
        var harness = new SocketIoServerHarness();
        using var shutdown = new CancellationTokenSource();
        await SocketIoPollingClient.OpenAsync(harness, harness.Context(shutdownToken: shutdown.Token));

        await shutdown.CancelAsync();

        await SocketIoPollingClient.WaitForSessionCountAsync(harness, 0);
    }

    [GeneratedRegex("^0\\{\"sid\":\"[A-Za-z0-9_-]{20}\",\"upgrades\":\\[\"websocket\"\\],\"pingInterval\":25000,\"pingTimeout\":20000,\"maxPayload\":1048576\\}$")]
    private static partial Regex PollingOpenPacketPattern();

    [GeneratedRegex("^40\\{\"sid\":\"[A-Za-z0-9_-]{20}\"\\}\u001e42\\[\"hello\",1\\]$")]
    private static partial Regex ConnectThenEchoPattern();
}
