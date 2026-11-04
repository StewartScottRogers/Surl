using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Mqtt.MqttTestExchange;

namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class PublishPayloadLimitTests
{
    private const string PayloadTooLargeNote = "A PUBLISH payload was longer than 200 bytes; closed with no reply.";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PayloadOverMaxUploadBytes_ClosesWithNoBytes()
    {
        var retained = new MqttRetainedMessages();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("publish-payload-over-the-limit")));

        await new MqttProtocolServer(retained, new AnonymousAuthenticationPolicy()).ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, UploadLimit(200), log));

        CollectionAssert.AreEqual(RecordedFixture.ReadAcceptedReplyBytes("publish-payload-over-the-limit"), connection.WrittenBytes);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual(PayloadTooLargeNote, log.Notes.Single());
        Assert.IsEmpty(retained.MatchingAny(["#"]));
    }

    [TestMethod]
    public void RecordedPayloadOverTheLimit_UpstreamCurlExits0WithoutWaitingForAReply()
    {
        Assert.AreEqual("0", Encoding.ASCII.GetString(RecordedFixture.ReadBytes("publish-payload-over-the-limit", "exitcode.txt")));
        Assert.IsEmpty(RecordedFixture.ReadBytes("publish-payload-over-the-limit", "stderr.txt"));
    }

    [TestMethod]
    public async Task PayloadOverMaxUploadBytes_IsRefusedWithoutReadingPastTheTopicNameLength()
    {
        var publish = PublishOf(201);
        var log = new RecordingExchangeLog();
        var connect = ClientPackets.CurlConnect();
        var connection = new ReadCountingConnection(new InMemoryConnection(
            [connect, .. RecordedFixture.OneBytePerRead(publish)],
            peerHalfClosesWhenExhausted: false));

        await new MqttProtocolServer(new MqttRetainedMessages(), new AnonymousAuthenticationPolicy()).ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, UploadLimit(200), log));

        // The fixed header (0x30, a two-byte remaining length) and the topic name's two-byte length.
        Assert.AreEqual(connect.Length + 3 + 2, connection.BytesRead);
        Assert.AreEqual(PayloadTooLargeNote, log.Notes.Single());
    }

    [TestMethod]
    [DataRow(0, DisplayName = "QoS 0")]
    [DataRow(1, DisplayName = "QoS 1, whose packet identifier is not payload")]
    public async Task PayloadOfExactlyMaxUploadBytes_IsKept(int qualityOfService)
    {
        var retained = new MqttRetainedMessages();

        var log = await ServeAsync(PublishOf(200, qualityOfService), UploadLimit(200), retained);

        Assert.IsEmpty(log.Notes);
        Assert.HasCount(200, retained.MatchingAny(["t"]).Single().Value.ToArray());
    }

    [TestMethod]
    public async Task QualityOfService1PayloadOneByteOver_ClosesWithNoReply()
    {
        var log = await ServeAsync(PublishOf(201, qualityOfService: 1), UploadLimit(200), new MqttRetainedMessages());

        Assert.AreEqual(PayloadTooLargeNote, log.Notes.Single());
    }

    [TestMethod]
    public async Task NoLimits_AcceptA2MiBPublish()
    {
        var retained = new MqttRetainedMessages();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 0, MaxUploadBytes = 0 };

        var log = await ServeAsync(PublishOf(2 * 1024 * 1024), limits, retained);

        Assert.IsEmpty(log.Notes);
        Assert.HasCount(2 * 1024 * 1024, retained.MatchingAny(["t"]).Single().Value.ToArray());
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x03, 0x00, 0x09, (byte)'t' }, DisplayName = "A topic name longer than the packet")]
    [DataRow(new byte[] { 0x30, 0x01, 0x00 }, DisplayName = "A body shorter than a topic name's length")]
    public async Task MalformedPublish_IsRefusedAsMalformedNotAsTooLarge(byte[] publish)
    {
        var log = await ServeAsync(publish, UploadLimit(200), new MqttRetainedMessages());

        StringAssert.Contains(log.Notes.Single(), "PUBLISH was malformed");
    }

    [TestMethod]
    public async Task ConnectionClosedInsideTheTopicNameLength_ClosesMidPacket()
    {
        byte[] publish = [0x30, 0x05, 0x00];

        var log = await ServeAsync(publish, UploadLimit(200), new MqttRetainedMessages());

        Assert.AreEqual("The client closed the connection part way through a packet.", log.Notes.Single());
    }

    private static ExchangeLimits UploadLimit(long maxUploadBytes) => ExchangeLimits.Default with { MaxUploadBytes = maxUploadBytes };

    private async Task<RecordingExchangeLog> ServeAsync(byte[] publish, ExchangeLimits limits, MqttRetainedMessages retained)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([ClientPackets.CurlConnect(), publish]);

        await new MqttProtocolServer(retained, new AnonymousAuthenticationPolicy()).ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, limits, log));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes[..ConnackAccepted.Length]);
        return log;
    }
}
