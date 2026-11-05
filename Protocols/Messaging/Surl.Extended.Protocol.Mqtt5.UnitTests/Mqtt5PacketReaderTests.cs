using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5TestPackets;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// Reading packets from a connection: reassembly across reads, the end of the stream, the
/// Remaining Length, and the packet and payload limits of ADR-0093 decision 8.
/// </summary>
[TestClass]
public sealed class Mqtt5PacketReaderTests
{
    // PUBLISH QoS 1, topic "a", Packet Identifier 1, no properties, payload "hello".
    private static readonly byte[] PublishHello = [0x32, 0x0B, 0x00, 0x01, 0x61, 0x00, 0x01, 0x00, 0x68, 0x65, 0x6C, 0x6C, 0x6F];

    [TestMethod]
    public async Task ReadPacketAsync_PacketSplitAcrossReads_ReassemblesIt()
    {
        var chunks = new[] { PublishHello[..1], PublishHello[1..3], PublishHello[3..6], PublishHello[6..] };

        var result = await ReadAsync(chunks);

        var packet = (Mqtt5PublishPacket)result.Packet!;
        Assert.AreEqual("a", packet.TopicName);
        Assert.AreEqual((ushort)1, packet.PacketIdentifier);
        CollectionAssert.AreEqual("hello"u8.ToArray(), packet.Payload);
    }

    [TestMethod]
    public async Task ReadPacketAsync_TwoPacketsInOneRead_ReadsThemOneAfterTheOther()
    {
        var connection = new Surl.Protocol.Abstractions.InMemoryConnection([(byte[])[0xC0, 0x00, 0xE0, 0x00]]);
        var reader = new Mqtt5PacketReader(connection, 0, 0);

        var first = await reader.ReadPacketAsync(CancellationToken.None);
        var second = await reader.ReadPacketAsync(CancellationToken.None);
        var third = await reader.ReadPacketAsync(CancellationToken.None);

        Assert.AreEqual(Mqtt5PacketType.PingRequest, first.Packet!.Type);
        Assert.AreEqual(Mqtt5PacketType.Disconnect, second.Packet!.Type);
        Assert.AreSame(Mqtt5PacketReadResult.ConnectionClosed, third);
    }

    [TestMethod]
    public async Task ReadPacketAsync_NoBytes_IsConnectionClosed()
    {
        Assert.AreEqual(Mqtt5PacketReadOutcome.ConnectionClosed, (await ReadAsync(Array.Empty<byte[]>())).Outcome);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30 }, DisplayName = "in the Remaining Length")]
    [DataRow(new byte[] { 0x30, 0x05, 0x00 }, DisplayName = "in the body")]
    public async Task ReadPacketAsync_ClosedPartWay_IsConnectionClosedMidPacket(byte[] bytes)
    {
        Assert.AreEqual(Mqtt5PacketReadOutcome.ConnectionClosedMidPacket, (await ReadAsync(bytes)).Outcome);
    }

    [TestMethod]
    public async Task ReadPacketAsync_RemainingLengthWithAFifthByte_IsMalformed()
    {
        var result = await ReadAsync([0x30, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F]);

        Assert.AreEqual(Mqtt5PacketReadOutcome.PacketRefused, result.Outcome);
        Assert.AreEqual(Mqtt5PacketFault.Malformed("1.5.5"), result.Fault);
    }

    [TestMethod]
    public async Task ReadPacketAsync_RemainingLengthNotMinimal_IsMalformed()
    {
        var result = await ReadAsync([0xC0, 0x80, 0x00]);

        Assert.AreEqual(Mqtt5PacketFault.Malformed("[MQTT-1.5.5-1]"), result.Fault);
    }

    [TestMethod]
    public async Task ReadPacketAsync_MalformedBody_IsRefusedWithTheDecodersFault()
    {
        var result = await ReadAsync([0xC0, 0x01, 0x00]);

        Assert.AreEqual(Mqtt5PacketFault.Malformed("3.12.2"), result.Fault);
    }

    [TestMethod]
    public async Task ReadPacketAsync_PacketOverThePacketLimit_IsPacketTooLargeWithoutReadingTheBody()
    {
        var result = await ReadAsync(PublishHello[..2], maxPacketBytes: PublishHello.Length - 1);

        Assert.AreEqual(Mqtt5ReasonCode.PacketTooLarge, result.Fault!.ReasonCode);
    }

    [TestMethod]
    public async Task ReadPacketAsync_PacketAtThePacketLimit_IsRead()
    {
        var result = await ReadAsync(PublishHello, maxPacketBytes: PublishHello.Length);

        Assert.AreEqual(Mqtt5PacketReadOutcome.PacketRead, result.Outcome);
    }

    [TestMethod]
    public async Task ReadPacketAsync_PublishPayloadOverThePayloadLimit_IsPacketTooLargeBeforeThePayloadIsRead()
    {
        var result = await ReadAsync(PublishHello[..8], maxPublishPayloadBytes: 4);

        Assert.AreEqual(new Mqtt5PacketFault(Mqtt5ReasonCode.PacketTooLarge, "ADR-0093 decision 8"), result.Fault);
    }

    [TestMethod]
    public async Task ReadPacketAsync_PublishPayloadAtThePayloadLimit_IsRead()
    {
        var result = await ReadAsync(OneByteAtATime(PublishHello), maxPublishPayloadBytes: 5);

        Assert.AreEqual(Mqtt5PacketReadOutcome.PacketRead, result.Outcome);
    }

    [TestMethod]
    public async Task ReadPacketAsync_PublishWithATwoBytePropertyLength_CountsOnlyThePayloadAgainstTheLimit()
    {
        var packet = new Mqtt5PublishPacket(false, 0, false, "a", 0, [Mqtt5Property.FromUserProperty(new string('n', 200), "v")], [1, 2, 3]);
        var bytes = Mqtt5PacketEncoder.Encode(packet);

        var atLimit = await ReadAsync(bytes, maxPublishPayloadBytes: 3);
        var overLimit = await ReadAsync(bytes, maxPublishPayloadBytes: 2);

        Assert.AreEqual(Mqtt5PacketReadOutcome.PacketRead, atLimit.Outcome);
        Assert.AreEqual(Mqtt5ReasonCode.PacketTooLarge, overLimit.Fault!.ReasonCode);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x01, 0x00 }, DisplayName = "no room for the topic length")]
    [DataRow(new byte[] { 0x30, 0x03, 0x00, 0x05, 0x61 }, DisplayName = "a topic past the packet")]
    [DataRow(new byte[] { 0x30, 0x03, 0x00, 0x01, 0x61 }, DisplayName = "no room for the property length")]
    public async Task ReadPacketAsync_PublishPrefixThatDoesNotFit_IsReadWholeAndRefusedAsMalformed(byte[] bytes)
    {
        var result = await ReadAsync(bytes, maxPublishPayloadBytes: 1);

        Assert.AreEqual(Mqtt5PacketFault.Malformed("4.13.1"), result.Fault);
    }

    [TestMethod]
    public async Task ReadPacketAsync_PublishPropertyLengthOverFourBytes_IsReadWholeAndRefusedAsMalformed()
    {
        var result = await ReadAsync([0x30, 0x08, 0x00, 0x01, 0x61, 0xFF, 0xFF, 0xFF, 0xFF, 0x00], maxPublishPayloadBytes: 1);

        Assert.AreEqual(Mqtt5PacketFault.Malformed("1.5.5"), result.Fault);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x0A, 0x00 }, DisplayName = "in the topic length")]
    [DataRow(new byte[] { 0x30, 0x0A, 0x00, 0x01, 0x61 }, DisplayName = "before the property length")]
    public async Task ReadPacketAsync_ClosedInAPublishPrefix_IsConnectionClosedMidPacket(byte[] bytes)
    {
        var result = await ReadAsync(bytes, maxPublishPayloadBytes: 1);

        Assert.AreEqual(Mqtt5PacketReadOutcome.ConnectionClosedMidPacket, result.Outcome);
    }

    [TestMethod]
    public async Task ReadPacketAsync_BodyLargerThanTheFirstBuffer_GrowsAndReadsItAll()
    {
        var payload = new byte[300_000];
        payload[^1] = 0x7A;
        var bytes = Mqtt5PacketEncoder.Encode(new Mqtt5PublishPacket(false, 0, false, "a", 0, [], payload));
        var chunks = bytes.Chunk(70_000);

        var result = await ReadAsync(chunks, maxPublishPayloadBytes: payload.Length);

        CollectionAssert.AreEqual(payload, ((Mqtt5PublishPacket)result.Packet!).Payload);
    }
}
