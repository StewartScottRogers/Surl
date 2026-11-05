using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5WebSocketExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// <c>mqtt5+ws</c> (BL-500; ADR-0093 decision 7; MQTT 5.0 chapter 6): the upgrade, MQTT packets
/// in binary frames however they are split, and the frames that close the connection, every
/// expected byte written from RFC 6455 and MQTT 5.0.
/// </summary>
[TestClass]
public sealed class Mqtt5WebSocketServerTests
{
    private const string SwitchingProtocolsHead =
        "HTTP/1.1 101 Switching Protocols\r\nDate: Tue, 29 Sep 2026 08:00:00 GMT\r\nServer: surl\r\nUpgrade: websocket\r\n"
        + "Connection: Upgrade\r\nSec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\nSec-WebSocket-Protocol: mqtt\r\n\r\n";

    private static readonly byte[] ConnackSuccessFrame = ServerBinaryFrame(ConnackSuccess);

    private static readonly byte[] NormalClose = ServerClose(1000);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullBroker_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Mqtt5WebSocketServer(null!));
    }

    [TestMethod]
    public void Schemes_IsMqtt5Ws()
    {
        CollectionAssert.AreEqual(new[] { "mqtt5+ws" }, new Mqtt5WebSocketServer(Broker()).Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new Mqtt5WebSocketServer(Broker());
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(ConnectionSending(false), null!));
    }

    [TestMethod]
    public async Task ServeAsync_UpgradeOfferingMqtt_AcceptedWithSubprotocolMqttAndConnectAnswered()
    {
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log), UpgradeRequest(), BinaryFrame(Connect()), BinaryFrame(DisconnectNormally));

        Assert.AreEqual(SwitchingProtocolsHead, Head(connection.WrittenBytes));
        CollectionAssert.AreEqual(ConnackSuccessFrame.Concat(NormalClose).ToArray(), AfterHead(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("WebSocket upgrade accepted for /mqtt with subprotocol mqtt.", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_UpgradeOfferingMqttAmongOthers_ChoosesMqtt()
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(subprotocols: "wamp, mqtt"));

        Assert.AreEqual(SwitchingProtocolsHead, Head(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_UpgradeNotOfferingMqtt_RefusedBadRequestAndClosed()
    {
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log), UpgradeRequest(subprotocols: "mqttv3.1"), BinaryFrame(Connect()));

        Assert.AreEqual(
            "HTTP/1.1 400 Bad Request\r\nDate: Tue, 29 Sep 2026 08:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            Encoding.ASCII.GetString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("WebSocket upgrade refused: 400 Bad Request: the upgrade does not offer the subprotocol mqtt ([MQTT-6.0.0-3]); closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_UpgradeOfferingNoSubprotocol_RefusedBadRequest()
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(subprotocols: null));

        StringAssert.StartsWith(Encoding.ASCII.GetString(connection.WrittenBytes), "HTTP/1.1 400 Bad Request\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PathOtherThanMqtt_RefusedNotFound()
    {
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log), UpgradeRequest(path: "/ws?x=1"));

        StringAssert.StartsWith(Encoding.ASCII.GetString(connection.WrittenBytes), "HTTP/1.1 404 Not Found\r\n");
        Assert.AreEqual("WebSocket upgrade refused: 404 Not Found: the path /ws is not /mqtt; closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_PathMqttWithQuery_Accepted()
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(path: "/mqtt?client=1"));

        Assert.AreEqual(SwitchingProtocolsHead, Head(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_NoHost_RefusedBadRequest()
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(host: string.Empty));

        StringAssert.StartsWith(Encoding.ASCII.GetString(connection.WrittenBytes), "HTTP/1.1 400 Bad Request\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_NotAWebSocketUpgrade_RefusedUpgradeRequiredWithItsFields()
    {
        var request = Encoding.ASCII.GetBytes("GET /mqtt HTTP/1.1\r\nHost: h\r\n\r\n");

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), request);

        Assert.AreEqual(
            "HTTP/1.1 426 Upgrade Required\r\nDate: Tue, 29 Sep 2026 08:00:00 GMT\r\nServer: surl\r\nUpgrade: websocket\r\n"
            + "Sec-WebSocket-Version: 13\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            Encoding.ASCII.GetString(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesBeforeAnyByte_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("No upgrade request was read: ConnectionClosed; closed with no bytes.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_HeadTooLarge_RefusedRequestHeaderFieldsTooLarge()
    {
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 32 };

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken, limits), UpgradeRequest());

        StringAssert.StartsWith(Encoding.ASCII.GetString(connection.WrittenBytes), "HTTP/1.1 431 ");
    }

    [TestMethod]
    public async Task ServeAsync_MalformedHead_RefusedBadRequest()
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), "nonsense\r\n\r\n"u8.ToArray());

        StringAssert.StartsWith(Encoding.ASCII.GetString(connection.WrittenBytes), "HTTP/1.1 400 Bad Request\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_RefusalNotWrittenWithinItsDeadline_ClosesWithoutIt()
    {
        var log = new RecordingExchangeLog();
        var connection = new Mqtt5StalledWriteConnection(UpgradeRequest(subprotocols: null));

        await new Mqtt5WebSocketServer(Broker()).ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        Assert.AreEqual("The 400 was not written within its 1-second write deadline; the connection is closed without it.", log.Notes[1]);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectSplitAcrossTwoBinaryFrames_Answered()
    {
        var connect = Connect();

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(), BinaryFrame(connect[..5]), BinaryFrame(connect[5..]), BinaryFrame(DisconnectNormally));

        CollectionAssert.AreEqual(ConnackSuccessFrame.Concat(NormalClose).ToArray(), AfterHead(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ConnectSplitAcrossAFragmentedMessage_Answered()
    {
        var connect = Connect();
        var first = ClientFrame(Binary, connect[..3], fin: false);
        var ping = ClientFrame(Ping, "p"u8.ToArray());
        var second = ClientFrame(Continuation, connect[3..]);

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(), first, ping, second, BinaryFrame(DisconnectNormally));

        CollectionAssert.AreEqual(new byte[] { 0x8A, 0x01, (byte)'p' }.Concat(ConnackSuccessFrame).Concat(NormalClose).ToArray(), AfterHead(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_TwoPacketsInOneFrame_BothHandled()
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(), BinaryFrame([.. Connect(), .. PingRequest]), BinaryFrame(DisconnectNormally));

        var pingResponse = ServerBinaryFrame([0xD0, 0x00]);
        CollectionAssert.AreEqual(ConnackSuccessFrame.Concat(pingResponse).Concat(NormalClose).ToArray(), AfterHead(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_UpgradeAndConnectInOneChunk_ConnectReadFromTheBufferedBytes()
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), [.. UpgradeRequest(), .. BinaryFrame(Connect()), .. BinaryFrame(DisconnectNormally)]);

        CollectionAssert.AreEqual(ConnackSuccessFrame.Concat(NormalClose).ToArray(), AfterHead(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_TextFrame_ClosesWithUnsupportedDataAndNoMqttPacket()
    {
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log), UpgradeRequest(), ClientFrame(Text, Connect()));

        CollectionAssert.AreEqual(ServerClose(1003), AfterHead(connection.WrittenBytes));
        CollectionAssert.Contains(log.Notes.ToList(), "WebSocket closed with 1003: a text frame arrived ([MQTT-6.0.0-1]).");
    }

    [TestMethod]
    public async Task ServeAsync_TextFrameAfterConnack_ClosesWithUnsupportedDataAndPublishesTheWill()
    {
        var state = NewState();
        var published = PublishedOn(state);
        var connection = ConnectionSending(false, UpgradeRequest(), BinaryFrame(Connect(will: Will())), ClientFrame(Text, "x"u8.ToArray()));

        await new Mqtt5WebSocketServer(Broker(state)).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        CollectionAssert.AreEqual(ConnackSuccessFrame.Concat(ServerClose(1003)).ToArray(), AfterHead(connection.WrittenBytes));
        Assert.AreEqual("w", published.Single().Topic);
    }

    [TestMethod]
    public async Task ServeAsync_ClientClose_AnsweredWithCloseEchoingItsCode()
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(), BinaryFrame(Connect()), ClientFrame(Close, [0x03, 0xE9, (byte)'b', (byte)'y']));

        CollectionAssert.AreEqual(ConnackSuccessFrame.Concat(ServerClose(1001)).ToArray(), AfterHead(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ClientCloseWithNoCode_AnsweredWithEmptyClose()
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(), ClientFrame(Close, []));

        CollectionAssert.AreEqual(new byte[] { 0x88, 0x00 }, AfterHead(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_Pong_IsIgnored()
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(), ClientFrame(Pong, []), BinaryFrame(Connect()), BinaryFrame(DisconnectNormally));

        CollectionAssert.AreEqual(ConnackSuccessFrame.Concat(NormalClose).ToArray(), AfterHead(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_EmptyBinaryFrame_IsSkipped()
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(), BinaryFrame(), BinaryFrame(Connect()), BinaryFrame(DisconnectNormally));

        CollectionAssert.AreEqual(ConnackSuccessFrame.Concat(NormalClose).ToArray(), AfterHead(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow(false, (byte)0, Binary, DisplayName = "unmasked")]
    [DataRow(true, (byte)0x40, Binary, DisplayName = "RSV1")]
    [DataRow(true, (byte)0x20, Binary, DisplayName = "RSV2")]
    [DataRow(true, (byte)0x10, Binary, DisplayName = "RSV3")]
    [DataRow(true, (byte)0, Continuation, DisplayName = "continuation with nothing to continue")]
    [DataRow(true, (byte)0, (byte)0x3, DisplayName = "reserved opcode")]
    public async Task ServeAsync_FrameRfc6455Refuses_ClosesWithProtocolError(bool masked, byte rsvBits, byte opcode)
    {
        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(), ClientFrame(opcode, Connect(), masked: masked, rsvBits: rsvBits));

        CollectionAssert.AreEqual(ServerClose(1002), AfterHead(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_BinaryFrameInsideAFragmentedMessage_ClosesWithProtocolError()
    {
        var connect = Connect();

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken), UpgradeRequest(), ClientFrame(Binary, connect[..3], fin: false), BinaryFrame(connect[3..]));

        CollectionAssert.AreEqual(ServerClose(1002), AfterHead(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_FrameOverTheMessageLimit_ClosesWithMessageTooBig()
    {
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 8 };

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken, limits), UpgradeRequest(), BinaryFrame(Connect()));

        CollectionAssert.AreEqual(ServerClose(1009), AfterHead(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesMidFrame_EndsWithANormalClose()
    {
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log), UpgradeRequest(), BinaryFrame(Connect())[..4]);

        CollectionAssert.AreEqual(NormalClose, AfterHead(connection.WrittenBytes));
        CollectionAssert.Contains(log.Notes.ToList(), "The client closed the connection: ConnectionClosedMidFrame.");
    }

    [TestMethod]
    public async Task ServeAsync_NoConnectWithinWhatTheUpgradeLeftOfTheHeadTimeout_ClosesWithNoMqttPacket()
    {
        var clock = new ManualTimeProvider();
        var connection = new Mqtt5TestPipeConnection();
        var log = new RecordingExchangeLog();
        var context = Context(clock, TestContext.CancellationToken, ExchangeLimits.Default with { HeadTimeout = TimeSpan.FromSeconds(10) }, log);

        var serving = new Mqtt5WebSocketServer(Broker()).ServeAsync(connection, context);
        await WaitForAsync(() => clock.PendingTimerCount == 1);
        clock.Advance(TimeSpan.FromSeconds(6));
        connection.Send(UpgradeRequest());
        await WaitForAsync(() => log.Notes.Count == 1 && clock.PendingTimerCount == 1);
        clock.Advance(TimeSpan.FromSeconds(4) - TimeSpan.FromTicks(1));
        var notesBeforeTheTimeout = log.Notes.Count;
        clock.Advance(TimeSpan.FromTicks(1));
        await WaitForAsync(() => log.Notes.Count == 2);
        connection.Abort();
        await serving;

        Assert.AreEqual(1, notesBeforeTheTimeout);
        Assert.AreEqual("The first packet was not complete within the head timeout; closed with no reply.", log.Notes[1]);
        CollectionAssert.AreEqual(NormalClose, AfterHead(connection.UnreadServerBytes()));
    }

    [TestMethod]
    public async Task ServeAsync_InfiniteHeadTimeout_LeavesTheBrokerNoHeadTimeout()
    {
        var limits = ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan };

        var connection = await ServeAsync(Context(new ManualTimeProvider(), TestContext.CancellationToken, limits), UpgradeRequest(), BinaryFrame(Connect()), BinaryFrame(DisconnectNormally));

        CollectionAssert.AreEqual(ConnackSuccessFrame.Concat(NormalClose).ToArray(), AfterHead(connection.WrittenBytes));
    }

    private async Task WaitForAsync(Func<bool> condition)
    {
        while (!condition())
        {
            await Task.Delay(5, TestContext.CancellationToken);
        }
    }
}
