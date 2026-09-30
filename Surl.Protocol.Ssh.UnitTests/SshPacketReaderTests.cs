using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshPacketReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadPayloadAsync_PacketsOneAfterAnother_ReturnsEachPayload()
    {
        var reader = Reader(Concat(Packet(2, 9), Packet([21], 10)), 0);

        var first = await reader.ReadPayloadAsync(TestContext.CancellationToken);
        var second = await reader.ReadPayloadAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new byte[] { 2, 9 }, first);
        CollectionAssert.AreEqual(new byte[] { 21 }, second);
    }

    [TestMethod]
    public async Task ReadPayloadAsync_PacketExactlyAtTheLimit_IsRead()
    {
        var reader = Reader(Packet([21], 10), 16);

        var payload = await reader.ReadPayloadAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new byte[] { 21 }, payload);
    }

    [TestMethod]
    public async Task ReadPayloadAsync_PacketOverTheLimit_IsRefusedFromItsLength()
    {
        var reader = Reader(UInt32(20), 16);

        var refusal = await Assert.ThrowsExactlyAsync<SshDisconnectRequiredException>(
            () => reader.ReadPayloadAsync(TestContext.CancellationToken).AsTask());

        Assert.AreEqual(SshDisconnectReason.ProtocolError, refusal.Reason);
        Assert.AreEqual("Protocol error", refusal.Description);
        Assert.AreEqual("An SSH packet announced 24 bytes, over the 16-byte packet limit.", refusal.Message);
    }

    [TestMethod]
    public async Task ReadPayloadAsync_NoLimitSet_StillRefusesAPacketNoArrayHolds()
    {
        var reader = Reader(UInt32(0xFFFFFFF8), 0);

        var refusal = await Assert.ThrowsExactlyAsync<SshDisconnectRequiredException>(
            () => reader.ReadPayloadAsync(TestContext.CancellationToken).AsTask());

        Assert.AreEqual($"An SSH packet announced 4294967292 bytes, over the {Array.MaxLength}-byte packet limit.", refusal.Message);
    }

    [TestMethod]
    public async Task ReadPayloadAsync_LengthNotABlockMultiple_IsRefused()
    {
        var reader = Reader(UInt32(13), 0);

        var refusal = await Assert.ThrowsExactlyAsync<SshDisconnectRequiredException>(
            () => reader.ReadPayloadAsync(TestContext.CancellationToken).AsTask());

        Assert.AreEqual("An SSH packet announced 17 bytes, not a multiple of the 8-byte block size.", refusal.Message);
    }

    [TestMethod]
    [DataRow(3, 8, DisplayName = "3 padding bytes")]
    [DataRow(11, 0, DisplayName = "No payload")]
    public async Task ReadPayloadAsync_PaddingUnder4OrNoPayload_IsRefused(int paddingLength, int payloadLength)
    {
        var reader = Reader(Packet(new byte[payloadLength], paddingLength), 0);

        var refusal = await Assert.ThrowsExactlyAsync<SshDisconnectRequiredException>(
            () => reader.ReadPayloadAsync(TestContext.CancellationToken).AsTask());

        Assert.AreEqual(
            $"An SSH packet of 12 bytes announced {paddingLength} padding bytes: fewer than 4, or no room for a message.",
            refusal.Message);
    }

    [TestMethod]
    public async Task ReadPayloadAsync_ClosedBetweenPackets_EndsWithNoNote()
    {
        var reader = Reader([], 0);

        var ended = await Assert.ThrowsExactlyAsync<SshExchangeEndedException>(
            () => reader.ReadPayloadAsync(TestContext.CancellationToken).AsTask());

        Assert.IsNull(ended.Note);
        Assert.AreEqual("The client ended the SSH exchange.", ended.Message);
    }

    [TestMethod]
    [DataRow(2, DisplayName = "Inside the length field")]
    [DataRow(9, DisplayName = "Inside the body")]
    public async Task ReadPayloadAsync_ClosedPartWayThroughAPacket_EndsWithANote(int bytesSent)
    {
        var reader = Reader(Packet(2, 9)[..bytesSent], 0);

        var ended = await Assert.ThrowsExactlyAsync<SshExchangeEndedException>(
            () => reader.ReadPayloadAsync(TestContext.CancellationToken).AsTask());

        Assert.AreEqual("The client closed the connection part way through an SSH packet.", ended.Note);
    }

    private static SshPacketReader Reader(byte[] inbound, long maxPacketBytes) =>
        new(new SshConnectionReader(new InMemoryConnection([inbound])), maxPacketBytes);
}
