using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Mqtt.MqttTestExchange;

namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class PacketLimitTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PacketOfExactlyTheLimit_IsAccepted()
    {
        var publish = PublishOf(1000);
        var retained = new MqttRetainedMessages();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = publish.Length };
        var connection = new InMemoryConnection([ClientPackets.CurlConnect(), publish]);

        await new MqttProtocolServer(retained, new AnonymousAuthenticationPolicy()).ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, limits));

        CollectionAssert.AreEqual(ConnackAccepted, connection.WrittenBytes);
        Assert.HasCount(1000, retained.MatchingAny(["t"]).Single().Value.ToArray());
    }

    [TestMethod]
    public async Task PacketOneByteOverTheLimit_ClosesWithNoBytes()
    {
        var publish = PublishOf(1000);
        var retained = new MqttRetainedMessages();
        var log = new RecordingExchangeLog();
        var connect = ClientPackets.CurlConnect();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = publish.Length - 1 };
        var connection = new ReadCountingConnection(new InMemoryConnection(
            [connect, .. RecordedFixture.OneBytePerRead(publish)],
            peerHalfClosesWhenExhausted: false));

        await new MqttProtocolServer(retained, new AnonymousAuthenticationPolicy()).ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, limits, log));

        // The fixed header is 0x30 and a two-byte remaining length; not one body byte was read.
        Assert.AreEqual(connect.Length + 3, connection.BytesRead);
        CollectionAssert.AreEqual(ConnackAccepted, connection.Inner.WrittenBytes);
        Assert.IsFalse(connection.Inner.Aborted);
        Assert.AreEqual($"A packet was longer than {publish.Length - 1} bytes; closed with no reply.", log.Notes.Single());
        Assert.IsEmpty(retained.MatchingAny(["#"]));
    }

    [TestMethod]
    public async Task RecordedPublishOverTheDefaultLimit_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes("packet-over-the-limit")));

        await new MqttProtocolServer(new MqttRetainedMessages(), new AnonymousAuthenticationPolicy()).ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(RecordedFixture.ReadAcceptedReplyBytes("packet-over-the-limit"), connection.WrittenBytes);
        Assert.AreEqual("A packet was longer than 1048576 bytes; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public void RecordedPublishOverTheLimit_UpstreamCurlExits0WithoutWaitingForAReply()
    {
        Assert.AreEqual("0", Encoding.ASCII.GetString(RecordedFixture.ReadBytes("packet-over-the-limit", "exitcode.txt")));
        Assert.IsEmpty(RecordedFixture.ReadBytes("packet-over-the-limit", "stderr.txt"));
    }
}
