using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class MqttProtocolServerTests
{
    // The engine hands an mqtts exchange a connection whose implicit handshake is done (ADR-0010).
    private static readonly ListenUrl MqttsListenUrl = new ListenUrl("mqtts", "127.0.0.1", 18884).WithBoundPort(18884);
    private static readonly TlsSession ImplicitTlsSession = new(SslProtocols.Tls12, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, null, null, null);

    private static readonly byte[] ConnackAccepted = [0x20, 0x02, 0x00, 0x00];
    private static readonly byte[] ConnackUnacceptableProtocolLevel = [0x20, 0x02, 0x00, 0x01];
    private static readonly byte[] ConnackIdentifierRejected = [0x20, 0x02, 0x00, 0x02];
    private static readonly byte[] Disconnect = [0xE0, 0x00];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullRetainedMessages_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MqttProtocolServer(null!));
    }

    [TestMethod]
    public void Schemes_AreMqttThenMqtts()
    {
        var server = new MqttProtocolServer(new MqttRetainedMessages());

        CollectionAssert.AreEqual(new[] { "mqtt", "mqtts" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new MqttProtocolServer(new MqttRetainedMessages());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog())));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    [DataRow("subscribe-t", false)]
    [DataRow("subscribe-t", true)]
    [DataRow("subscribe-200-bytes", false)]
    [DataRow("subscribe-200-bytes", true)]
    [DataRow("subscribe-nothing-retained", false)]
    [DataRow("subscribe-wildcard", false)]
    [DataRow("subscribe-wildcard", true)]
    [DataRow("publish-hi", false)]
    [DataRow("publish-hi", true)]
    [DataRow("publish-200-bytes", false)]
    [DataRow("publish-200-bytes", true)]
    public async Task ServeAsync_RecordedRequest_SendsThePacketsUpstreamCurlAccepted(string caseName, bool oneBytePerRead)
    {
        var request = RecordedFixture.ReadRequestBytes(caseName);
        var chunks = oneBytePerRead ? RecordedFixture.OneBytePerRead(request) : RecordedFixture.Whole(request);

        var (connection, log) = await ServeAsync(chunks, RetainedFor(caseName));

        Assert.AreEqual("0", Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "exitcode.txt")));
        CollectionAssert.AreEqual(RecordedFixture.ReadAcceptedReplyBytes(caseName), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.IsFalse(log.Notes.Any(note => note.Contains("closed with no reply", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("mqtts-subscribe-t", false)]
    [DataRow("mqtts-subscribe-t", true)]
    [DataRow("mqtts-publish-hi", false)]
    [DataRow("mqtts-publish-hi", true)]
    public async Task ServeAsync_RecordedMqttsRequestOverTls_SendsThePacketsUpstreamCurlAccepted(string caseName, bool oneBytePerRead)
    {
        var request = RecordedFixture.ReadRequestBytes(caseName);
        var chunks = oneBytePerRead ? RecordedFixture.OneBytePerRead(request) : RecordedFixture.Whole(request);

        var (connection, log) = await ServeAsync(chunks, RetainedFor(caseName), listenUrl: MqttsListenUrl, tlsSession: ImplicitTlsSession);

        Assert.AreEqual("0", Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "exitcode.txt")));
        Assert.IsEmpty(RecordedFixture.ReadBytes(caseName, "stderr.txt"));
        CollectionAssert.AreEqual(RecordedFixture.ReadAcceptedReplyBytes(caseName), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreSame(ImplicitTlsSession, connection.TlsSession);
        Assert.IsFalse(log.Notes.Any(note => note.Contains("closed with no reply", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeOf200Bytes_EncodesTheRemainingLengthInTwoBytes()
    {
        var (connection, _) = await ServeAsync(
            RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("subscribe-200-bytes")),
            RetainedFor("subscribe-200-bytes"));

        var publish = connection.WrittenBytes.AsSpan(ConnackAccepted.Length + 5);
        Assert.AreEqual(0x31, publish[0]);
        Assert.AreEqual(0xCB, publish[1]);
        Assert.AreEqual(0x01, publish[2]);
        Assert.AreEqual(203, publish.Length - 3 - Disconnect.Length);
    }

    [TestMethod]
    public async Task ServeAsync_PublishThenSubscribeFromCurl_DeliversWhatWasPublished()
    {
        var retained = new MqttRetainedMessages();

        await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("publish-hi")), retained);
        var (subscriber, _) = await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("subscribe-t")), retained);

        CollectionAssert.AreEqual(RecordedFixture.ReadAcceptedReplyBytes("subscribe-t"), subscriber.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_Publish200BytesFromCurl_KeepsTheWholePayload()
    {
        var retained = new MqttRetainedMessages();

        await ServeAsync(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("publish-200-bytes")), retained);

        var message = retained.MatchingAny(["t"]).Single();
        Assert.AreEqual(new string('x', 200), Encoding.ASCII.GetString(message.Value));
    }

    [TestMethod]
    public async Task ServeAsync_EmptyPublish_RemovesTheRetainedMessage()
    {
        var retained = new MqttRetainedMessages();
        retained.Retain("t", "hi"u8);

        var (connection, _) = await ServeAsync(ClientPackets.CurlConnect(), ClientPackets.Publish(0x30, "t", ""), ClientPackets.Disconnect, retained);

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        Assert.AreEqual(0, retained.MatchingAny(["#"]).Count);
    }

    [TestMethod]
    public async Task ServeAsync_MalformedRemainingLength_ClosesWithNoBytes()
    {
        var (connection, log) = await ServeBytesAsync([0x10, 0xFF, 0xFF, 0xFF, 0xFF, 0x00]);

        Assert.AreEqual(0, connection.WrittenBytes.Length);
        Assert.IsFalse(connection.Aborted);
        StringAssert.Contains(log.Notes.Single(), "remaining length ran past four bytes");
    }

    [TestMethod]
    public async Task ServeAsync_FourByteRemainingLength_IsRead()
    {
        // 0x80 0x80 0x80 0x00 is a remaining length of 0 spread over four bytes; a CONNECT
        // with no body is then malformed, so the packet was read and judged, not refused as a length.
        var (connection, log) = await ServeBytesAsync([0x10, 0x80, 0x80, 0x80, 0x00]);

        Assert.AreEqual(0, connection.WrittenBytes.Length);
        StringAssert.Contains(log.Notes.Single(), "CONNECT was malformed");
    }

    [TestMethod]
    public async Task ServeAsync_NoPacketLimit_AcceptsAnyLength()
    {
        var (connection, _) = await ServeAsync(RecordedFixture.Whole(ClientPackets.CurlConnect()), new MqttRetainedMessages(), Limits(0));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow("MQTT", (byte)3)]
    [DataRow("MQTT", (byte)5)]
    [DataRow("MQIsdp", (byte)3)]
    public async Task ServeAsync_UnsupportedProtocolLevel_AnswersConnack1AndCloses(string protocolName, byte level)
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Join(
            ClientPackets.Connect(protocolName, level, 0x02, "c"),
            ClientPackets.PingRequest));

        CollectionAssert.AreEqual(ConnackUnacceptableProtocolLevel, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), "CONNACK 1");
    }

    [TestMethod]
    public async Task ServeAsync_EmptyClientIdentifierWithoutCleanSession_AnswersConnack2AndCloses()
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Connect("MQTT", 4, 0x00, ""));

        CollectionAssert.AreEqual(ConnackIdentifierRejected, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), "CONNACK 2");
    }

    [TestMethod]
    public async Task ServeAsync_EmptyClientIdentifierWithCleanSession_IsAccepted()
    {
        var (connection, _) = await ServeBytesAsync(ClientPackets.Connect("MQTT", 4, 0x02, ""));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x10, 0x03, 0x00, 0x04, (byte)'M' }, DisplayName = "protocol name cut short")]
    [DataRow(new byte[] { 0x10, 0x06, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T' }, DisplayName = "no level")]
    [DataRow(new byte[] { 0x10, 0x07, 0x00, 0x04, (byte)'H', (byte)'T', (byte)'T', (byte)'P', 0x04 }, DisplayName = "other protocol name")]
    [DataRow(new byte[] { 0x10, 0x07, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04 }, DisplayName = "no flags")]
    [DataRow(new byte[] { 0x10, 0x0C, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, 0x03, 0x00, 0x3C, 0x00, 0x00 }, DisplayName = "reserved flag set")]
    [DataRow(new byte[] { 0x10, 0x09, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, 0x02, 0x00 }, DisplayName = "keep alive cut short")]
    [DataRow(new byte[] { 0x10, 0x0A, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, 0x02, 0x00, 0x3C }, DisplayName = "no client identifier")]
    [DataRow(new byte[] { 0x10, 0x0E, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, 0x02, 0x00, 0x3C, 0x00, 0x02, 0xC3, 0x28 }, DisplayName = "client identifier not UTF-8")]
    [DataRow(new byte[] { 0x10, 0x0D, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, 0x02, 0x00, 0x3C, 0x00, 0x01, 0x00 }, DisplayName = "client identifier holds U+0000")]
    public async Task ServeAsync_MalformedConnect_ClosesWithNoBytes(byte[] connect)
    {
        var (connection, log) = await ServeBytesAsync(connect);

        Assert.AreEqual(0, connection.WrittenBytes.Length);
        StringAssert.Contains(log.Notes.Single(), "CONNECT was malformed; closed with no reply");
    }

    [TestMethod]
    [DataRow(1, DisplayName = "in the fixed header's first byte")]
    [DataRow(2, DisplayName = "in the remaining length")]
    [DataRow(10, DisplayName = "in the body")]
    public async Task ServeAsync_ConnectionClosedMidPacket_ClosesWithNoBytes(int bytesSent)
    {
        var connect = ClientPackets.CurlConnect();
        byte[] request = bytesSent == 2 ? [0x10, 0x80] : connect[..bytesSent];

        var (connection, log) = await ServeBytesAsync(request);

        Assert.AreEqual(0, connection.WrittenBytes.Length);
        StringAssert.Contains(log.Notes.Single(), "part way through a packet");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionClosedBetweenPackets_ClosesWithNoNote()
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.CurlConnect());

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        Assert.AreEqual(0, log.Notes.Count);
    }

    [TestMethod]
    public async Task ServeAsync_NothingSent_ClosesWithNoBytes()
    {
        var (connection, log) = await ServeBytesAsync([]);

        Assert.AreEqual(0, connection.WrittenBytes.Length);
        Assert.AreEqual(0, log.Notes.Count);
    }

    [TestMethod]
    public async Task ServeAsync_FirstPacketNotConnect_ClosesWithNoBytes()
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.PingRequest);

        Assert.AreEqual(0, connection.WrittenBytes.Length);
        StringAssert.Contains(log.Notes.Single(), "The first packet was PingRequest, not CONNECT");
    }

    [TestMethod]
    public async Task ServeAsync_SecondConnect_ClosesWithNoReply()
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), ClientPackets.CurlConnect()));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), "A second CONNECT arrived");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x11, 0x00 }, "Connect", DisplayName = "CONNECT with flags")]
    [DataRow(new byte[] { 0x80, 0x00 }, "Subscribe", DisplayName = "SUBSCRIBE without flag 2")]
    [DataRow(new byte[] { 0xA0, 0x00 }, "Unsubscribe", DisplayName = "UNSUBSCRIBE without flag 2")]
    [DataRow(new byte[] { 0x60, 0x00 }, "PublishRelease", DisplayName = "PUBREL without flag 2")]
    [DataRow(new byte[] { 0xC1, 0x00 }, "PingRequest", DisplayName = "PINGREQ with flags")]
    public async Task ServeAsync_WrongFixedHeaderFlags_ClosesWithNoReply(byte[] packet, string typeName)
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), packet));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), $"A {typeName} packet had fixed header flags");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x20, 0x02, 0x00, 0x00 }, "ConnectAcknowledgement")]
    [DataRow(new byte[] { 0xD0, 0x00 }, "PingResponse")]
    public async Task ServeAsync_PacketOnlyAServerSends_ClosesWithNoReply(byte[] packet, string typeName)
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), packet));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), $"A client sent {typeName}, which only a server sends");
    }

    [TestMethod]
    [DataRow((byte)0x00, 0)]
    [DataRow((byte)0xF0, 15)]
    public async Task ServeAsync_ReservedPacketType_ClosesWithNoReply(byte firstByte, int type)
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), [firstByte, 0x00]));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), $"A packet of reserved type {type} arrived");
    }

    [TestMethod]
    [DataRow((byte)0x0A, DisplayName = "will QoS without the will flag")]
    [DataRow((byte)0x22, DisplayName = "will retain without the will flag")]
    [DataRow((byte)0x1E, DisplayName = "will QoS 3")]
    [DataRow((byte)0x42, DisplayName = "password without user name")]
    public async Task ServeAsync_ConnectFlagsSection312Forbids_ClosesWithNoBytes(byte connectFlags)
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Connect("MQTT", 4, connectFlags, "c"));

        Assert.AreEqual(0, connection.WrittenBytes.Length);
        StringAssert.Contains(log.Notes.Single(), "CONNECT was malformed");
    }

    [TestMethod]
    [DataRow((byte)0x0E, DisplayName = "will at QoS 1")]
    [DataRow((byte)0x36, DisplayName = "will at QoS 2, retained")]
    [DataRow((byte)0xC2, DisplayName = "user name and password")]
    [DataRow((byte)0x82, DisplayName = "user name alone")]
    public async Task ServeAsync_ConnectFlagsSection312Allows_AnswersConnack0(byte connectFlags)
    {
        var (connection, _) = await ServeBytesAsync(ClientPackets.Connect("MQTT", 4, connectFlags, "c"));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_DuplicateFlagOnQualityOfService0Publish_ClosesWithNoReply()
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), ClientPackets.Publish(0x38, "t", "hi")));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), "PUBLISH was malformed");
    }

    [TestMethod]
    public async Task ServeAsync_DuplicateFlagOnQualityOfService1Publish_AnswersPuback()
    {
        var (connection, _) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), ClientPackets.Publish(0x3A, "t", 5, "hi")));

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, 0x00, 0x40, 0x02, 0x00, 0x05 }, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_PublishPastTheRetainedBounds_ClosesWithNoReplyAndKeepsNothing()
    {
        var retained = new MqttRetainedMessages(maxTopics: 1);
        retained.Retain("a", "1"u8);

        var (connection, log) = await ServeAsync(ClientPackets.CurlConnect(), ClientPackets.Publish(0x32, "b", 1, "2"), [], retained);

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), "past their bounds");
        CollectionAssert.AreEqual(new[] { "a" }, retained.MatchingAny(["#"]).Select(message => message.Key).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_BodyLongerThanTheFirstBuffer_IsReadWhole()
    {
        var payload = Enumerable.Repeat((byte)'z', 70_000).ToArray();
        // Remaining length 70003 (topic length, "t", payload) is F3 A2 04 in section 2.2.3's encoding.
        byte[] publish = [0x30, 0xF3, 0xA2, 0x04, 0x00, 0x01, (byte)'t', .. payload];
        var retained = new MqttRetainedMessages();

        var (connection, _) = await ServeAsync(ClientPackets.CurlConnect(), publish, ClientPackets.Disconnect, retained, Limits(0));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        CollectionAssert.AreEqual(payload, retained.MatchingAny(["t"]).Single().Value);
    }

    [TestMethod]
    public async Task ServeAsync_LargeRemainingLengthNeverSent_ClosesMidPacket()
    {
        byte[] publish = [0x30, 0xFF, 0xFF, 0xFF, 0x7F, 0x00, 0x01, (byte)'t'];

        var (_, log) = await ServeAsync(ClientPackets.CurlConnect(), publish, [], new MqttRetainedMessages(), Limits(0) with { MaxUploadBytes = 0 });

        StringAssert.Contains(log.Notes.Single(), "part way through a packet");
    }

    [TestMethod]
    public async Task ServeAsync_PingRequest_AnswersPingResponse()
    {
        var (connection, _) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), ClientPackets.PingRequest, ClientPackets.PingRequest));

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, 0x00, 0xD0, 0x00, 0xD0, 0x00 }, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_Disconnect_StopsReading()
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), ClientPackets.Disconnect, ClientPackets.PingRequest));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        Assert.AreEqual(0, log.Notes.Count);
    }

    [TestMethod]
    public async Task ServeAsync_QualityOfService1Publish_AnswersPubackAndKeepsTheMessage()
    {
        var retained = new MqttRetainedMessages();

        var (connection, _) = await ServeAsync(ClientPackets.CurlConnect(), ClientPackets.Publish(0x32, "t", 0x1234, "hi"), ClientPackets.Disconnect, retained);

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, 0x00, 0x40, 0x02, 0x12, 0x34 }, connection.WrittenBytes);
        Assert.AreEqual("hi", Encoding.ASCII.GetString(retained.MatchingAny(["t"]).Single().Value));
    }

    [TestMethod]
    public async Task ServeAsync_QualityOfService2Publish_AnswersPubrecThenPubcomp()
    {
        var release = new byte[] { 0x62, 0x02, 0x00, 0x07 };

        var (connection, _) = await ServeAsync(ClientPackets.CurlConnect(), ClientPackets.Publish(0x34, "t", 7, "hi"), release, new MqttRetainedMessages());

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, 0x00, 0x50, 0x02, 0x00, 0x07, 0x70, 0x02, 0x00, 0x07 }, connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x62, 0x01, 0x00 }, DisplayName = "identifier cut short")]
    [DataRow(new byte[] { 0x62, 0x03, 0x00, 0x07, 0x00 }, DisplayName = "bytes after the identifier")]
    [DataRow(new byte[] { 0x62, 0x02, 0x00, 0x00 }, DisplayName = "identifier zero")]
    public async Task ServeAsync_MalformedPubrel_ClosesWithNoReply(byte[] release)
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), release));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), "PublishRelease was malformed");
    }

    [TestMethod]
    [DataRow(0x36, "t", "PUBLISH was malformed", DisplayName = "QoS 3")]
    [DataRow(0x30, "", "PUBLISH was malformed", DisplayName = "empty topic")]
    [DataRow(0x30, "a/+", "PUBLISH was malformed", DisplayName = "wildcard in topic")]
    [DataRow(0x32, "t", "PUBLISH had no packet identifier", DisplayName = "QoS 1 without identifier")]
    public async Task ServeAsync_MalformedPublish_ClosesWithNoReply(int firstByte, string topic, string note)
    {
        var retained = new MqttRetainedMessages();

        var (connection, log) = await ServeAsync(ClientPackets.CurlConnect(), ClientPackets.Packet((byte)firstByte, ClientPackets.String(topic)), [], retained);

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), note);
        Assert.AreEqual(0, retained.MatchingAny(["#"]).Count);
    }

    [TestMethod]
    public async Task ServeAsync_PublishWithTopicCutShort_ClosesWithNoReply()
    {
        var (_, log) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), [0x30, 0x03, 0x00, 0x05, (byte)'t']));

        StringAssert.Contains(log.Notes.Single(), "PUBLISH was malformed");
    }

    [TestMethod]
    public async Task ServeAsync_QualityOfService1PublishWithIdentifierZero_ClosesWithNoReply()
    {
        var (_, log) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), ClientPackets.Publish(0x32, "t", 0, "hi")));

        StringAssert.Contains(log.Notes.Single(), "PUBLISH had no packet identifier");
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeWithValidAndInvalidFilters_GrantsQos0AndFailsTheInvalidOne()
    {
        var retained = new MqttRetainedMessages();
        retained.Retain("a/b", "1"u8);
        retained.Retain("c", "2"u8);
        retained.Retain("$SYS/x", "3"u8);

        var (connection, log) = await ServeAsync(
            ClientPackets.CurlConnect(),
            ClientPackets.Subscribe(9, ("a/#", 1), ("a/#x", 0), ("#", 2)),
            [],
            retained);

        byte[] expected =
        [
            .. ConnackAccepted,
            0x90, 0x05, 0x00, 0x09, 0x00, 0x80, 0x00,
            0x31, 0x06, 0x00, 0x03, (byte)'a', (byte)'/', (byte)'b', (byte)'1',
            0x31, 0x04, 0x00, 0x01, (byte)'c', (byte)'2',
            .. Disconnect,
        ];
        CollectionAssert.AreEqual(expected, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), "2 retained messages delivered");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x82, 0x01, 0x00 }, "SUBSCRIBE had no packet identifier", DisplayName = "identifier cut short")]
    [DataRow(new byte[] { 0x82, 0x06, 0x00, 0x00, 0x00, 0x01, (byte)'t', 0x00 }, "SUBSCRIBE had no packet identifier", DisplayName = "identifier zero")]
    [DataRow(new byte[] { 0x82, 0x02, 0x00, 0x01 }, "SUBSCRIBE had no topic filter", DisplayName = "no filter")]
    [DataRow(new byte[] { 0x82, 0x04, 0x00, 0x01, 0x00, 0x05 }, "SUBSCRIBE was malformed", DisplayName = "filter cut short")]
    [DataRow(new byte[] { 0x82, 0x05, 0x00, 0x01, 0x00, 0x01, (byte)'t' }, "SUBSCRIBE was malformed", DisplayName = "no requested QoS")]
    [DataRow(new byte[] { 0x82, 0x06, 0x00, 0x01, 0x00, 0x01, (byte)'t', 0x03 }, "SUBSCRIBE was malformed", DisplayName = "requested QoS 3")]
    public async Task ServeAsync_MalformedSubscribe_ClosesWithNoReply(byte[] subscribe, string note)
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), subscribe));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), note);
    }

    [TestMethod]
    public async Task ServeAsync_Unsubscribe_AnswersUnsuback()
    {
        var (connection, _) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), ClientPackets.Unsubscribe(0x0102, "t", "a/#")));

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, 0x00, 0xB0, 0x02, 0x01, 0x02 }, connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xA2, 0x01, 0x00 }, DisplayName = "identifier cut short")]
    [DataRow(new byte[] { 0xA2, 0x05, 0x00, 0x00, 0x00, 0x01, (byte)'t' }, DisplayName = "identifier zero")]
    [DataRow(new byte[] { 0xA2, 0x02, 0x00, 0x01 }, DisplayName = "no filter")]
    [DataRow(new byte[] { 0xA2, 0x04, 0x00, 0x01, 0x00, 0x05 }, DisplayName = "filter cut short")]
    [DataRow(new byte[] { 0xA2, 0x04, 0x00, 0x01, 0x00, 0x00 }, DisplayName = "empty filter")]
    public async Task ServeAsync_MalformedUnsubscribe_ClosesWithNoReply(byte[] unsubscribe)
    {
        var (connection, log) = await ServeBytesAsync(ClientPackets.Join(ClientPackets.CurlConnect(), unsubscribe));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), "UNSUBSCRIBE was malformed");
    }

    private static MqttRetainedMessages RetainedFor(string caseName)
    {
        var retained = new MqttRetainedMessages();
        switch (caseName)
        {
            case "subscribe-t":
            case "mqtts-subscribe-t":
                retained.Retain("t", "hi"u8);
                break;
            case "subscribe-200-bytes":
                retained.Retain("t", Encoding.ASCII.GetBytes(new string('y', 200)));
                break;
            case "subscribe-wildcard":
                retained.Retain("a/1", "hi"u8);
                retained.Retain("a/2", "yo"u8);
                retained.Retain("b/1", "no"u8);
                retained.Retain("a/1/x", "no"u8);
                break;
        }

        return retained;
    }

    private static ExchangeLimits Limits(long maxMessageBytes) => ExchangeLimits.Default with { MaxMessageBytes = maxMessageBytes };

    private static ExchangeContext Context(IExchangeLog log, CancellationToken cancellationToken = default, ListenUrl? listenUrl = null) => new(
        1,
        listenUrl ?? new ListenUrl("mqtt", "127.0.0.1", 18883).WithBoundPort(18883),
        new IPEndPoint(IPAddress.Loopback, 18883),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        TimeProvider.System,
        cancellationToken);

    private Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeBytesAsync(byte[] request) =>
        ServeAsync(RecordedFixture.Whole(request), new MqttRetainedMessages());

    private Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        byte[] first,
        byte[] second,
        byte[] third,
        MqttRetainedMessages retained,
        ExchangeLimits? limits = null) =>
        ServeAsync(RecordedFixture.Whole(ClientPackets.Join(first, second, third)), retained, limits);

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        IEnumerable<ReadOnlyMemory<byte>> chunks,
        MqttRetainedMessages retained,
        ExchangeLimits? limits = null,
        ListenUrl? listenUrl = null,
        TlsSession? tlsSession = null)
    {
        var server = new MqttProtocolServer(retained);
        var connection = new InMemoryConnection(chunks, initialTlsSession: tlsSession);
        var log = new RecordingExchangeLog();
        var context = Context(log, TestContext.CancellationToken, listenUrl) with { Limits = limits ?? ExchangeLimits.Default };

        await server.ServeAsync(connection, context);
        await connection.DisposeAsync();

        return (connection, log);
    }
}
