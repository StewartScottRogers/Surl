using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// Frames read and refused (AMQP 0-9-1 sections 4.2.3, 4.2.6 and 4.2.7; ADR-0112 decisions 3, 7 and 8).
/// </summary>
[TestClass]
public sealed class Amqp0FrameReaderTests
{
    private const int FrameMax = 4096;

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> EachFrameType =>
    [
        [new Amqp0Frame(Amqp0FrameType.Method, 1, new byte[] { 0x00, 0x0A, 0x00, 0x0A, 0x01 })],
        [new Amqp0Frame(Amqp0FrameType.ContentHeader, 1, new Amqp0ContentHeader(60, 3, 0x8000, new byte[] { 4, (byte)'t', (byte)'e', (byte)'x', (byte)'t' }).Encode())],
        [new Amqp0Frame(Amqp0FrameType.ContentBody, 7, new byte[] { (byte)'a', (byte)'b', (byte)'c' })],
        [Amqp0Frame.Heartbeat],
    ];

    [TestMethod]
    [DynamicData(nameof(EachFrameType))]
    public async Task ReadFrameAsync_EachFrameType_RoundTrips(Amqp0Frame frame)
    {
        var encoded = frame.Encode();
        var reader = Reader(encoded.Select(value => (ReadOnlyMemory<byte>)new[] { value }).ToArray());

        var result = await reader.ReadFrameAsync(FrameMax, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0FrameReadOutcome.FrameRead, result.Outcome);
        Assert.IsNull(result.RefusalReplyCode);
        Assert.AreEqual(frame.Type, result.Frame!.Type);
        Assert.AreEqual(frame.Channel, result.Frame.Channel);
        CollectionAssert.AreEqual(frame.Payload.ToArray(), result.Frame.Payload.ToArray());
        CollectionAssert.AreEqual(encoded, result.Frame.Encode());
    }

    [TestMethod]
    public async Task ReadFrameAsync_TwoFramesInOneRead_AreReadOneAtATime()
    {
        byte[] bothFrames = [.. Amqp0Frame.Heartbeat.Encode(), .. new Amqp0Frame(Amqp0FrameType.ContentBody, 2, new byte[] { 9 }).Encode()];
        var reader = Reader([bothFrames]);

        var first = await reader.ReadFrameAsync(FrameMax, TestContext.CancellationToken);
        var second = await reader.ReadFrameAsync(FrameMax, TestContext.CancellationToken);
        var third = await reader.ReadFrameAsync(FrameMax, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0FrameType.Heartbeat, first.Frame!.Type);
        Assert.AreEqual(Amqp0FrameType.ContentBody, second.Frame!.Type);
        Assert.AreEqual(Amqp0FrameReadOutcome.ConnectionClosed, third.Outcome);
        Assert.IsNull(third.RefusalReplyCode);
    }

    [TestMethod]
    public async Task ReadFrameAsync_BadFrameEnd_IsRefusedAs501()
    {
        byte[] frame = [3, 0, 1, 0, 0, 0, 1, 0x41, 0xCF];

        var result = await Reader([frame]).ReadFrameAsync(FrameMax, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0FrameReadOutcome.BadFrameEnd, result.Outcome);
        Assert.AreEqual(Amqp0ReplyCode.FrameError, result.RefusalReplyCode);
        Assert.AreEqual(501, (int)result.RefusalReplyCode!.Value);
        Assert.IsNull(result.Frame);
    }

    [TestMethod]
    public async Task ReadFrameAsync_FrameLargerThanFrameMax_IsRefusedAs501FromItsHeader()
    {
        // 4089 bytes of payload and 8 of overhead make 4097, one above frame-max; no payload follows.
        byte[] header = [3, 0, 1, 0, 0, 0x0F, 0xF9];

        var result = await Reader([header]).ReadFrameAsync(FrameMax, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0FrameReadOutcome.FrameTooLarge, result.Outcome);
        Assert.AreEqual(Amqp0ReplyCode.FrameError, result.RefusalReplyCode);
    }

    [TestMethod]
    public async Task ReadFrameAsync_FrameExactlyFrameMax_IsRead()
    {
        var frame = new Amqp0Frame(Amqp0FrameType.ContentBody, 1, new byte[FrameMax - Amqp0Frame.OverheadBytes]);

        var result = await Reader([frame.Encode()]).ReadFrameAsync(FrameMax, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0FrameReadOutcome.FrameRead, result.Outcome);
    }

    [TestMethod]
    public async Task ReadFrameAsync_SizeNearUInt32Max_IsRefusedWithoutOverflow()
    {
        byte[] header = [3, 0, 1, 0xFF, 0xFF, 0xFF, 0xFF];

        var result = await Reader([header]).ReadFrameAsync(int.MaxValue, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0FrameReadOutcome.FrameTooLarge, result.Outcome);
    }

    [TestMethod]
    [DataRow((ushort)1)]
    [DataRow((ushort)65535)]
    public async Task ReadFrameAsync_HeartbeatOffChannelZero_IsRefusedAs501(ushort channel)
    {
        var frame = new Amqp0Frame(Amqp0FrameType.Heartbeat, channel, ReadOnlyMemory<byte>.Empty);

        var result = await Reader([frame.Encode()]).ReadFrameAsync(FrameMax, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0FrameReadOutcome.HeartbeatOffChannelZero, result.Outcome);
        Assert.AreEqual(Amqp0ReplyCode.FrameError, result.RefusalReplyCode);
    }

    [TestMethod]
    [DataRow((byte)0)]
    [DataRow((byte)4)]
    [DataRow((byte)9)]
    public async Task ReadFrameAsync_UnknownFrameType_IsRefusedAs501(byte type)
    {
        byte[] frame = [type, 0, 0, 0, 0, 0, 0, 0xCE];

        var result = await Reader([frame]).ReadFrameAsync(FrameMax, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0FrameReadOutcome.UnknownFrameType, result.Outcome);
        Assert.AreEqual(Amqp0ReplyCode.FrameError, result.RefusalReplyCode);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ClosedInsideTheHeader_IsClosedMidFrame()
    {
        var result = await Reader([new byte[] { 1, 0 }]).ReadFrameAsync(FrameMax, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0FrameReadOutcome.ConnectionClosedMidFrame, result.Outcome);
        Assert.IsNull(result.RefusalReplyCode);
    }

    [TestMethod]
    public async Task ReadFrameAsync_ClosedInsideThePayload_IsClosedMidFrame()
    {
        var result = await Reader([new byte[] { 3, 0, 1, 0, 0, 0, 4, 0x41 }]).ReadFrameAsync(FrameMax, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0FrameReadOutcome.ConnectionClosedMidFrame, result.Outcome);
    }

    [TestMethod]
    public async Task ReadFrameAsync_FrameMaxBelowOverhead_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            async () => await Reader([]).ReadFrameAsync(Amqp0Frame.OverheadBytes - 1, TestContext.CancellationToken));
    }

    private static Amqp0FrameReader Reader(ReadOnlyMemory<byte>[] chunks) => new(new InMemoryConnection(chunks));
}
