using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A hub connection over the WebSocket transport, driven with frames written from RFC 6455 and hub
/// records written from HubProtocol.md's "Overview", "Invocations", "Streaming", "Cancellation",
/// "Completion and results", "Errors" and "Ping" (ADR-0119, decisions 1, 3, 4 and 7).
/// </summary>
[TestClass]
public sealed class SignalRHubSessionTests
{
    private const string ProtocolErrorClose = "{\"type\":7,\"error\":\"Connection closed with an error.\"}\u001e";

    private const string FarewellClose = "{\"type\":7}\u001e";

    private static readonly TimeSpan HundredMilliseconds = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan FifteenSeconds = TimeSpan.FromSeconds(15);

    [TestMethod]
    public async Task RunAsync_Upgrade_IsAnswered101WithTheAcceptKey()
    {
        var harness = new SignalRServerHarness();

        var client = await SignalRHubClient.ConnectAsync(harness, completesHandshake: false);

        Assert.StartsWith(
            "HTTP/1.1 101 Switching Protocols\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + SignalRHubClient.Accept + "\r\n\r\n",
            Encoding.ASCII.GetString(client.Connection.WrittenBytes));
        Assert.AreEqual(1, harness.Connections.Count);
        await client.EndAsync();
        Assert.AreEqual(0, harness.Connections.Count);
    }

    [TestMethod]
    public async Task RunAsync_Handshake_IsAnsweredWithAnEmptyObject()
    {
        var harness = new SignalRServerHarness();

        var client = await SignalRHubClient.ConnectAsync(harness);

        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse }, client.Records());
        await client.EndAsync();
        Assert.IsFalse(client.Connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow("{\"protocol\":\"protobuf\",\"version\":1}", "Requested protocol is not available.")]
    [DataRow("{\"protocol\":\"json\",\"version\":2}", "Requested protocol is not available.")]
    [DataRow("not json", "The handshake request is invalid.")]
    public async Task RunAsync_HandshakeRefused_AnswersTheHandshakeErrorThenClosesNormally(string request, string error)
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness, completesHandshake: false);

        client.SendRecords(request);

        Assert.AreEqual(1000, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(new[] { "{\"error\":\"" + error + "\"}\u001e" }, client.Records());
        await client.EndAsync();
        Assert.IsTrue(client.Connection.WritesCompleted);
    }

    [TestMethod]
    public async Task RunAsync_HandshakeFollowedByARecordInTheSameMessage_AnswersBoth()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness, completesHandshake: false);

        client.SendText(SignalRHubClient.HandshakeRequest + "{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[\"hi\"]}\u001e");

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, "{\"type\":3,\"invocationId\":\"1\",\"result\":\"hi\"}\u001e" },
            await client.WaitForRecordsAsync(2));
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_NoHandshakeWithinTheHeadTimeout_ClosesWithPolicyViolation()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness, completesHandshake: false);

        await harness.Clock.AdvanceWhenWaitingAsync(ExchangeLimits.Default.HeadTimeout);

        Assert.AreEqual(1008, await client.WaitForCloseCodeAsync());
        Assert.IsEmpty(client.Records());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_HeadTimeoutPastTheLongestTimer_WaitsForTheHandshakeWithoutADeadline()
    {
        var harness = new SignalRServerHarness();
        var limits = new ExchangeLimits(TimeSpan.FromDays(100), 102400, 8192, maxMessageBytes: 0, 104857600);

        var client = await SignalRHubClient.ConnectAsync(harness, harness.Context(limits));
        client.SendRecords("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[1]}");

        Assert.AreEqual("{\"type\":3,\"invocationId\":\"1\",\"result\":1}\u001e", (await client.WaitForRecordsAsync(2))[1]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_EchoInvocation_CompletesWithItsArgumentByteForByte()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords("{\"type\":1,\"invocationId\":\"123\",\"target\":\"Echo\",\"arguments\":[{ \"a\" : [1, 2.50] }]}");

        Assert.AreEqual("{\"type\":3,\"invocationId\":\"123\",\"result\":{ \"a\" : [1, 2.50] }}\u001e", (await client.WaitForRecordsAsync(2))[1]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_NonBlockingInvocation_GetsNoCompletion()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords(
            "{\"type\":1,\"target\":\"Echo\",\"arguments\":[\"unanswered\"]}",
            "{\"type\":1,\"target\":\"Fail\",\"arguments\":[]}",
            "{\"type\":1,\"invocationId\":\"2\",\"target\":\"Echo\",\"arguments\":[\"answered\"]}");

        await client.WaitForRecordsAsync(2);
        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, "{\"type\":3,\"invocationId\":\"2\",\"result\":\"answered\"}\u001e" },
            client.Records());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_FailingMethod_CompletesWithError()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Fail\",\"arguments\":[]}");

        Assert.AreEqual(
            "{\"type\":3,\"invocationId\":\"1\",\"error\":\"An unexpected error occurred invoking 'Fail' on the server.\"}\u001e",
            (await client.WaitForRecordsAsync(2))[1]);
        await client.EndAsync();
    }

    [TestMethod]
    [DataRow("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Nope\",\"arguments\":[]}", "Unknown hub method.")]
    [DataRow("{\"type\":1,\"invocationId\":\"1\",\"target\":\"echo\",\"arguments\":[1]}", "Unknown hub method.")]
    [DataRow("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Count\",\"arguments\":[3]}", "The method was invoked with the wrong message type.")]
    [DataRow("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[3]}", "The method was invoked with the wrong message type.")]
    [DataRow("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Fail\",\"arguments\":[]}", "An unexpected error occurred invoking 'Fail' on the server.")]
    [DataRow("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[1,2]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[1],\"streamIds\":[\"s\"]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Count\",\"arguments\":[]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Count\",\"arguments\":[1],\"streamIds\":[\"s\"]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Count\",\"arguments\":[\"3\"]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Count\",\"arguments\":[1.5]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Count\",\"arguments\":[-1]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Count\",\"arguments\":[1001]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Broadcast\",\"arguments\":[]}", "The method was invoked with the wrong message type.")]
    [DataRow("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Broadcast\",\"arguments\":[],\"streamIds\":[\"s\"]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Collect\",\"arguments\":[1],\"streamIds\":[\"s\"]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Collect\",\"arguments\":[]}", "Invalid arguments for the hub method.")]
    [DataRow("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"s\",\"t\"]}", "Invalid arguments for the hub method.")]
    public async Task RunAsync_CallTheHubCannotServe_CompletesWithItsError(string invocation, string error)
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords(invocation);

        Assert.AreEqual("{\"type\":3,\"invocationId\":\"1\",\"error\":\"" + error + "\"}\u001e", (await client.WaitForRecordsAsync(2))[1]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_StreamInvocation_SendsEachItemAHundredMillisecondsApartThenACompletion()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords("{\"type\":4,\"invocationId\":\"s1\",\"target\":\"Count\",\"arguments\":[3]}");
        await client.WaitForRecordsAsync(2);
        await harness.Clock.AdvanceWhenWaitingAsync(HundredMilliseconds);
        await client.WaitForRecordsAsync(3);
        await harness.Clock.AdvanceWhenWaitingAsync(HundredMilliseconds);

        CollectionAssert.AreEqual(
            new[]
            {
                SignalRHubClient.HandshakeResponse,
                "{\"type\":2,\"invocationId\":\"s1\",\"item\":1}\u001e",
                "{\"type\":2,\"invocationId\":\"s1\",\"item\":2}\u001e",
                "{\"type\":2,\"invocationId\":\"s1\",\"item\":3}\u001e",
                "{\"type\":3,\"invocationId\":\"s1\"}\u001e",
            },
            await client.WaitForRecordsAsync(5));
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_StreamOfNoItems_CompletesAtOnce()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords("{\"type\":4,\"invocationId\":\"s1\",\"target\":\"Count\",\"arguments\":[0]}");

        Assert.AreEqual("{\"type\":3,\"invocationId\":\"s1\"}\u001e", (await client.WaitForRecordsAsync(2))[1]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_CancelInvocation_StopsTheStreamAndCompletesIt()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);
        client.SendRecords("{\"type\":4,\"invocationId\":\"s1\",\"target\":\"Count\",\"arguments\":[1000]}");
        await client.WaitForRecordsAsync(2);
        await harness.Clock.WaitForTimerDueAtAsync(harness.Clock.GetUtcNow() + HundredMilliseconds);

        client.SendRecords("{\"type\":5,\"invocationId\":\"s1\"}");
        await client.WaitForRecordsAsync(3);
        harness.Clock.Advance(HundredMilliseconds);
        client.SendRecords("{\"type\":1,\"invocationId\":\"after\",\"target\":\"Echo\",\"arguments\":[0]}");
        await client.WaitForRecordsAsync(4);

        CollectionAssert.AreEqual(
            new[]
            {
                SignalRHubClient.HandshakeResponse,
                "{\"type\":2,\"invocationId\":\"s1\",\"item\":1}\u001e",
                "{\"type\":3,\"invocationId\":\"s1\"}\u001e",
                "{\"type\":3,\"invocationId\":\"after\",\"result\":0}\u001e",
            },
            client.Records());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_CancelInvocationNamingNothingInProgress_IsIgnored()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords("{\"type\":5,\"invocationId\":\"nothing\"}", "{\"type\":6}", "{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[true]}");

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, "{\"type\":3,\"invocationId\":\"1\",\"result\":true}\u001e" },
            await client.WaitForRecordsAsync(2));
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_InvocationIdAlreadyInProgress_IsAProtocolError()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);
        client.SendRecords("{\"type\":4,\"invocationId\":\"s1\",\"target\":\"Count\",\"arguments\":[1000]}");
        await client.WaitForRecordsAsync(2);

        client.SendRecords("{\"type\":1,\"invocationId\":\"s1\",\"target\":\"Echo\",\"arguments\":[1]}");

        Assert.AreEqual(1002, await client.WaitForCloseCodeAsync());
        Assert.AreEqual(ProtocolErrorClose, client.Records()[^1]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_MoreStreamsThanAllowed_CompletesTheNextWithTooManyInvocations()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);
        var streams = Enumerable.Range(0, SignalRHubSession.MaxInvocationsInProgress)
            .Select(index => "{\"type\":4,\"invocationId\":\"s" + index + "\",\"target\":\"Count\",\"arguments\":[1000]}");

        client.SendRecords([.. streams, "{\"type\":4,\"invocationId\":\"one-too-many\",\"target\":\"Count\",\"arguments\":[1]}"]);

        var records = await client.WaitForRecordsAsync(SignalRHubSession.MaxInvocationsInProgress + 2);
        CollectionAssert.Contains(records, "{\"type\":3,\"invocationId\":\"one-too-many\",\"error\":\"Too many invocations in progress.\"}\u001e");
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ServerSendsNothingForAKeepAliveInterval_SendsAPingEachInterval()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        await harness.Clock.AdvanceWhenWaitingAsync(FifteenSeconds);
        await client.WaitForRecordsAsync(2);
        client.SendRecords("{\"type\":6}", "{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[null]}");
        await client.WaitForRecordsAsync(3);
        await harness.Clock.AdvanceWhenWaitingAsync(FifteenSeconds);

        CollectionAssert.AreEqual(
            new[]
            {
                SignalRHubClient.HandshakeResponse,
                "{\"type\":6}\u001e",
                "{\"type\":3,\"invocationId\":\"1\",\"result\":null}\u001e",
                "{\"type\":6}\u001e",
            },
            await client.WaitForRecordsAsync(4));
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ClientQuietPastTheTimeout_IsClosedWithTheTimeoutClose()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        await harness.Clock.AdvanceWhenWaitingAsync(FifteenSeconds);
        await client.WaitForRecordsAsync(2);
        await harness.Clock.AdvanceWhenWaitingAsync(FifteenSeconds);

        Assert.AreEqual(1000, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(
            new[]
            {
                SignalRHubClient.HandshakeResponse,
                "{\"type\":6}\u001e",
                "{\"type\":7,\"error\":\"Server timeout elapsed without receiving a message from the client.\"}\u001e",
            },
            client.Records());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ClientHubClose_ClosesNormallyWithNothingMore()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords("{\"type\":7}");

        Assert.AreEqual(1000, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse }, client.Records());
        await client.EndAsync();
    }

    [TestMethod]
    [DataRow("{\"type\":2,\"invocationId\":\"up\",\"item\":1}")]
    [DataRow("{\"type\":3,\"invocationId\":\"up\"}")]
    [DataRow("{\"type\":8,\"sequenceId\":1}")]
    [DataRow("[1]")]
    [DataRow("{\"protocol\":\"json\",\"version\":1}")]
    public async Task RunAsync_RecordTheServerDoesNotRead_IsAProtocolError(string record)
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords(record);

        Assert.AreEqual(1002, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse, ProtocolErrorClose }, client.Records());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_WebSocketPing_IsAnsweredWithAPongCarryingItsPayload()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.Connection.Send(SignalRHubClient.ClientFrame(0x89, "Hello"u8.ToArray()));
        var bytes = await client.Connection.WaitForWrittenAsync(written => SignalRHubClient.Frames(written).Any(frame => frame.Opcode == 0xA));

        CollectionAssert.AreEqual("Hello"u8.ToArray(), SignalRHubClient.Frames(bytes).Single(frame => frame.Opcode == 0xA).Payload);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_WebSocketPongAndFragments_AreReadWithoutAnAnswer()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.Connection.Send(SignalRHubClient.ClientFrame(0x8A, []));
        client.Connection.Send(SignalRHubClient.ClientFrame(0x02, Encoding.UTF8.GetBytes("{\"type\":1,\"invocationId\":\"1\",")));
        client.Connection.Send(SignalRHubClient.ClientFrame(0x80, Encoding.UTF8.GetBytes("\"target\":\"Echo\",\"arguments\":[2]}\u001e")));

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, "{\"type\":3,\"invocationId\":\"1\",\"result\":2}\u001e" },
            await client.WaitForRecordsAsync(2));
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_WebSocketClose_IsAnsweredWithANormalClose()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.Connection.Send(SignalRHubClient.ClientFrame(0x88, [0x03, 0xE8]));

        Assert.AreEqual(1000, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    [DataRow(new byte[] { 0x81, 0x01, 0x41 }, 1002, DisplayName = "unmasked")]
    [DataRow(new byte[] { 0xC1, 0x80, 0x37, 0xfa, 0x21, 0x3d }, 1002, DisplayName = "reserved bit")]
    [DataRow(new byte[] { 0x83, 0x80, 0x37, 0xfa, 0x21, 0x3d }, 1002, DisplayName = "reserved opcode")]
    [DataRow(new byte[] { 0x80, 0x80, 0x37, 0xfa, 0x21, 0x3d }, 1002, DisplayName = "continuation without a message")]
    [DataRow(new byte[] { 0x01, 0x80, 0x37, 0xfa, 0x21, 0x3d, 0x81, 0x80, 0x37, 0xfa, 0x21, 0x3d }, 1002, DisplayName = "data frame inside a message")]
    [DataRow(new byte[] { 0x81, 0x82, 0x37, 0xfa, 0x21, 0x3d, 0xC3 ^ 0x37, 0x28 ^ 0xfa }, 1007, DisplayName = "text not UTF-8")]
    [DataRow(new byte[] { 0x88, 0x84, 0x37, 0xfa, 0x21, 0x3d, 0x03 ^ 0x37, 0xE8 ^ 0xfa, 0xC3 ^ 0x21, 0x28 ^ 0x3d }, 1007, DisplayName = "close reason not UTF-8")]
    public async Task RunAsync_FrameRfc6455MakesInvalid_IsClosedWithItsCode(byte[] frame, int code)
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.Connection.Send(frame);

        Assert.AreEqual(code, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_FramePastMaxMessage_IsClosedWithMessageTooBig()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness, harness.Context(SmallMessageLimits()));

        client.Connection.Send(SignalRHubClient.ClientFrame(0x81, new byte[100]));

        Assert.AreEqual(1009, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_FragmentedMessagePastMaxMessage_IsClosedWithMessageTooBig()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness, harness.Context(SmallMessageLimits()));

        client.Connection.Send(SignalRHubClient.ClientFrame(0x01, new byte[40]));
        client.Connection.Send(SignalRHubClient.ClientFrame(0x80, new byte[40]));

        Assert.AreEqual(1009, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_RecordSpanningMessagesPastMaxMessage_IsClosedWithTheErrorCloseAndMessageTooBig()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness, harness.Context(SmallMessageLimits()));

        client.SendText("{\"type\":6}\u001e{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",");
        client.SendText("\"arguments\":[\"and a good deal more\"]}\u001e");

        Assert.AreEqual(1009, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse, ProtocolErrorClose }, client.Records());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ClientClosesTheConnectionWithoutAClose_EndsWithNothingWritten()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        await client.EndAsync();

        Assert.IsFalse(SignalRHubClient.Frames(client.Connection.WrittenBytes).Any(frame => frame.Opcode == 0x8));
        Assert.IsFalse(client.Connection.WritesCompleted);
    }

    [TestMethod]
    public async Task RunAsync_ReadFails_EndsWithNothingWritten()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.Connection.FailNextRead(new IOException("reset"));
        await client.Serving.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsFalse(client.Connection.WritesCompleted);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("the connection failed (reset)", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_ExchangeCancelledForALimit_SendsTheFarewellThenLingersForTheClientsClose()
    {
        var harness = new SignalRServerHarness();
        using var cancellation = new CancellationTokenSource();
        var client = await SignalRHubClient.ConnectAsync(harness, harness.Context(cancellationToken: cancellation.Token));
        client.SendRecords("{\"type\":4,\"invocationId\":\"s1\",\"target\":\"Count\",\"arguments\":[1000]}");
        await client.WaitForRecordsAsync(2);

        await cancellation.CancelAsync();

        Assert.AreEqual(1001, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, "{\"type\":2,\"invocationId\":\"s1\",\"item\":1}\u001e", FarewellClose },
            client.Records());
        Assert.IsTrue(client.Connection.WritesCompleted);
        Assert.IsFalse(client.Serving.IsCompleted);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_Shutdown_SendsTheFarewellAndEndsWithoutLingering()
    {
        var harness = new SignalRServerHarness();
        using var shutdown = new CancellationTokenSource();
        var client = await SignalRHubClient.ConnectAsync(harness, harness.Context(cancellationToken: shutdown.Token, shutdownToken: shutdown.Token));

        await shutdown.CancelAsync();
        await client.Serving.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(1001, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse, FarewellClose }, client.Records());
        Assert.IsTrue(client.Connection.WritesCompleted);
    }

    [TestMethod]
    public async Task RunAsync_FarewellNotWrittenWithinItsDeadline_EndsWithoutItAndNotesWhy()
    {
        var harness = new SignalRServerHarness();
        using var shutdown = new CancellationTokenSource();
        var client = await SignalRHubClient.ConnectAsync(harness, harness.Context(cancellationToken: shutdown.Token, shutdownToken: shutdown.Token));
        client.Connection.HoldWrites();

        await shutdown.CancelAsync();
        await harness.Clock.AdvanceWhenWaitingAsync(SignalRHubSession.FarewellWriteDeadline);
        await client.Serving.WaitAsync(TimeSpan.FromSeconds(10));

        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse }, client.Records());
        Assert.IsFalse(client.Connection.WritesCompleted);
        Assert.Contains("The farewell was not written within its 1-second deadline, or shutdown cut it short; the connection is closed without it.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task RunAsync_Broadcast_ReachesEveryConnectionPastItsHandshakeTheCallerIncludedThenCompletesTheCaller()
    {
        var harness = new SignalRServerHarness();
        var caller = await SignalRHubClient.ConnectAsync(harness);
        var other = await SignalRHubClient.ConnectAsync(harness);
        var beforeHandshake = await SignalRHubClient.ConnectAsync(harness, completesHandshake: false);

        caller.SendRecords("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Broadcast\",\"arguments\":[\"hi\",{ \"x\" : 1 }]}");

        const string Broadcast = "{\"type\":1,\"target\":\"Broadcast\",\"arguments\":[\"hi\",{ \"x\" : 1 }]}\u001e";
        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, Broadcast, "{\"type\":3,\"invocationId\":\"1\"}\u001e" },
            await caller.WaitForRecordsAsync(3));
        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse, Broadcast }, await other.WaitForRecordsAsync(2));
        Assert.IsEmpty(beforeHandshake.Records());
        await caller.EndAsync();
        await other.EndAsync();
        await beforeHandshake.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_NonBlockingBroadcast_BroadcastsWithoutACompletion()
    {
        var harness = new SignalRServerHarness();
        var caller = await SignalRHubClient.ConnectAsync(harness);

        caller.SendRecords(
            "{\"type\":1,\"target\":\"Broadcast\",\"arguments\":[]}",
            "{\"type\":1,\"invocationId\":\"2\",\"target\":\"Echo\",\"arguments\":[2]}");

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, "{\"type\":1,\"target\":\"Broadcast\",\"arguments\":[]}\u001e", "{\"type\":3,\"invocationId\":\"2\",\"result\":2}\u001e" },
            await caller.WaitForRecordsAsync(3));
        await caller.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_BroadcastAfterAConnectionEnded_DoesNotReachIt()
    {
        var harness = new SignalRServerHarness();
        var caller = await SignalRHubClient.ConnectAsync(harness);
        var gone = await SignalRHubClient.ConnectAsync(harness);
        await gone.EndAsync();

        caller.SendRecords("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Broadcast\",\"arguments\":[1]}");

        await caller.WaitForRecordsAsync(3);
        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse }, gone.Records());
        await caller.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_BroadcastsPastTheUnsentCap_CloseTheSlowConnectionWithTheErrorCloseAndPolicyViolation()
    {
        var harness = new SignalRServerHarness();
        var caller = await SignalRHubClient.ConnectAsync(harness);
        var slow = await SignalRHubClient.ConnectAsync(harness);
        slow.Connection.HoldWrites();
        var argument = "\"" + new string('a', 40000) + "\"";
        var broadcasts = (int)(SignalRHubSession.MaxUnsentBytes / 40000) + 1;

        for (var index = 0; index < broadcasts; index++)
        {
            caller.SendRecords("{\"type\":1,\"invocationId\":\"" + index + "\",\"target\":\"Broadcast\",\"arguments\":[" + argument + "]}");
        }

        await caller.WaitForRecordsAsync(1 + (2 * broadcasts));
        slow.Connection.ReleaseWrites();

        Assert.AreEqual(1008, await slow.WaitForCloseCodeAsync());
        var records = slow.Records();
        Assert.AreEqual(ProtocolErrorClose, records[^1]);
        Assert.IsLessThan(broadcasts, records.Length - 2);
        await slow.EndAsync();
        await caller.EndAsync();
    }

    [TestMethod]
    public async Task ReceiveBroadcast_AfterTheSessionEnded_QueuesNothing()
    {
        var harness = new SignalRServerHarness();
        var connection = new UnitTestDuplexConnection();
        var context = harness.Context();
        using var session = new SignalRHubSession(
            new SignalRWebSocketTransport(connection, new HttpMessage.HttpConnectionReader(connection, 8192), context), context, new SignalRInvocationFaults(context, "/chat", null), harness.Connections);
        connection.HalfClose();
        await session.RunAsync();

        session.ReceiveBroadcast(new HubInvocation(false, null, "Broadcast", [], []));

        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public async Task RunAsync_CollectInvocation_CompletesWithTheUploadedItemsAfterTheClientCompletesTheStream()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords(
            "{\"type\":1,\"invocationId\":\"1\",\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"s\"]}",
            "{\"type\":2,\"invocationId\":\"s\",\"item\":1}",
            "{\"type\":2,\"invocationId\":\"s\",\"item\":{ \"a\" : 2 }}",
            "{\"type\":3,\"invocationId\":\"s\"}");

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, "{\"type\":3,\"invocationId\":\"1\",\"result\":[1,{ \"a\" : 2 }]}\u001e" },
            await client.WaitForRecordsAsync(2));
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_CollectStreamInvocation_EchoesEachItemAsItArrivesThenCompletesAfterTheClient()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);
        client.SendRecords("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"s\"]}");

        client.SendRecords("{\"type\":2,\"invocationId\":\"s\",\"item\":\"x\"}");
        await client.WaitForRecordsAsync(2);
        client.SendRecords("{\"type\":3,\"invocationId\":\"s\"}");

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, "{\"type\":2,\"invocationId\":\"1\",\"item\":\"x\"}\u001e", "{\"type\":3,\"invocationId\":\"1\"}\u001e" },
            await client.WaitForRecordsAsync(3));
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_NonBlockingCollect_ReadsTheUploadAndSendsNothing()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords(
            "{\"type\":1,\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"s\"]}",
            "{\"type\":2,\"invocationId\":\"s\",\"item\":1}",
            "{\"type\":3,\"invocationId\":\"s\"}",
            "{\"type\":1,\"invocationId\":\"2\",\"target\":\"Echo\",\"arguments\":[2]}");

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, "{\"type\":3,\"invocationId\":\"2\",\"result\":2}\u001e" },
            await client.WaitForRecordsAsync(2));
        await client.EndAsync();
    }

    [TestMethod]
    [DataRow("{\"type\":2,\"invocationId\":\"s\",\"item\":2}", DisplayName = "StreamItem after the stream's Completion")]
    [DataRow("{\"type\":3,\"invocationId\":\"s\"}", DisplayName = "Completion after the stream's Completion")]
    public async Task RunAsync_UploadRecordAfterTheStreamsCompletion_IsAProtocolError(string record)
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords(
            "{\"type\":1,\"invocationId\":\"1\",\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"s\"]}",
            "{\"type\":3,\"invocationId\":\"s\"}",
            record);

        Assert.AreEqual(1002, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, "{\"type\":3,\"invocationId\":\"1\",\"result\":[]}\u001e", ProtocolErrorClose },
            client.Records());
        await client.EndAsync();
    }

    [TestMethod]
    [DataRow("{\"type\":1,\"invocationId\":\"2\",\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"s\"]}", DisplayName = "stream ID being uploaded")]
    [DataRow("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[1]}", DisplayName = "invocationId of a waiting Collect")]
    public async Task RunAsync_IdOfAnUploadInProgressUsedAgain_IsAProtocolError(string record)
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"s\"]}", record);

        Assert.AreEqual(1002, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse, ProtocolErrorClose }, client.Records());
        await client.EndAsync();
    }

    [TestMethod]
    [DataRow(1, DisplayName = "Invocation")]
    [DataRow(4, DisplayName = "StreamInvocation")]
    public async Task RunAsync_CollectUploadPastMaxFileSize_CompletesWithTooLargeAndDropsTheRest(int type)
    {
        var harness = new SignalRServerHarness();
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, 1048576, maxUploadBytes: 3);
        var client = await SignalRHubClient.ConnectAsync(harness, harness.Context(limits));

        client.SendRecords(
            "{\"type\":" + type + ",\"invocationId\":\"1\",\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"s\"]}",
            "{\"type\":2,\"invocationId\":\"s\",\"item\":12}",
            "{\"type\":2,\"invocationId\":\"s\",\"item\":3}",
            "{\"type\":2,\"invocationId\":\"s\",\"item\":4}",
            "{\"type\":2,\"invocationId\":\"s\",\"item\":5}",
            "{\"type\":3,\"invocationId\":\"s\"}",
            "{\"type\":1,\"invocationId\":\"2\",\"target\":\"Echo\",\"arguments\":[2]}");

        var records = await client.WaitForRecordsAsync(type == 1 ? 3 : 5);
        Assert.HasCount(type == 1 ? 3 : 5, records);
        CollectionAssert.AreEqual(
            new[] { "{\"type\":3,\"invocationId\":\"1\",\"error\":\"The upload stream is too large.\"}\u001e", "{\"type\":3,\"invocationId\":\"2\",\"result\":2}\u001e" },
            records[^2..]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_CollectUploadWithNoUploadLimit_TakesEveryItem()
    {
        var harness = new SignalRServerHarness();
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, 1048576, maxUploadBytes: 0);
        var client = await SignalRHubClient.ConnectAsync(harness, harness.Context(limits));

        client.SendRecords(
            "{\"type\":1,\"invocationId\":\"1\",\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"s\"]}",
            "{\"type\":2,\"invocationId\":\"s\",\"item\":\"abcdef\"}",
            "{\"type\":3,\"invocationId\":\"s\"}");

        Assert.AreEqual("{\"type\":3,\"invocationId\":\"1\",\"result\":[\"abcdef\"]}\u001e", (await client.WaitForRecordsAsync(2))[1]);
        await client.EndAsync();
    }

    [TestMethod]
    [DataRow(1, DisplayName = "waiting Collect")]
    [DataRow(4, DisplayName = "streaming Collect")]
    public async Task RunAsync_CancelInvocationOfACollect_CompletesItWithNoResultAndDropsTheRestOfTheUpload(int type)
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);

        client.SendRecords(
            "{\"type\":" + type + ",\"invocationId\":\"1\",\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"s\"]}",
            "{\"type\":5,\"invocationId\":\"1\"}",
            "{\"type\":5,\"invocationId\":\"1\"}",
            "{\"type\":2,\"invocationId\":\"s\",\"item\":1}",
            "{\"type\":3,\"invocationId\":\"s\"}",
            "{\"type\":1,\"invocationId\":\"2\",\"target\":\"Echo\",\"arguments\":[2]}");

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, "{\"type\":3,\"invocationId\":\"1\"}\u001e", "{\"type\":3,\"invocationId\":\"2\",\"result\":2}\u001e" },
            await client.WaitForRecordsAsync(3));
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_UploadsInProgressAtTheLimit_CompleteTheNextStreamAndCollectWithTooManyInvocations()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectAsync(harness);
        var uploads = Enumerable.Range(0, SignalRHubSession.MaxInvocationsInProgress)
            .Select(index => "{\"type\":1,\"invocationId\":\"c" + index + "\",\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"s" + index + "\"]}");

        client.SendRecords(
            [.. uploads,
            "{\"type\":4,\"invocationId\":\"count\",\"target\":\"Count\",\"arguments\":[1]}",
            "{\"type\":1,\"invocationId\":\"collect\",\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"more\"]}"]);

        CollectionAssert.AreEqual(
            new[]
            {
                SignalRHubClient.HandshakeResponse,
                "{\"type\":3,\"invocationId\":\"count\",\"error\":\"Too many invocations in progress.\"}\u001e",
                "{\"type\":3,\"invocationId\":\"collect\",\"error\":\"Too many invocations in progress.\"}\u001e",
            },
            await client.WaitForRecordsAsync(3));
        await client.EndAsync();
    }

    private static ExchangeLimits SmallMessageLimits() => new(TimeSpan.FromSeconds(30), 102400, 8192, maxMessageBytes: 64, 104857600);
}
