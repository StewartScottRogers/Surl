using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Frames read and refused (AMQP 1.0 Part 2 section 2.3; ADR-0107 decisions 3 and 7). The bytes
/// are written by hand from the specification's frame layout.
/// </summary>
[TestClass]
public sealed class Amqp1FrameReaderTests
{
    private const uint MaxFrameSize = Amqp1FrameLimits.MinMaxFrameSize;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadFrameAsync_FrameSplitIntoSingleBytes_IsReadWhole()
    {
        var frame = Amqp1Frame.Carrying(3, new Amqp1Open("c"));
        var encoded = frame.Encode();

        var result = await Reader(encoded.Select(value => (ReadOnlyMemory<byte>)new[] { value }).ToArray()).ReadFrameAsync(MaxFrameSize, TestContext.CancellationToken);

        Assert.AreEqual(Amqp1FrameReadOutcome.FrameRead, result.Outcome);
        Assert.IsNull(result.RefusalCondition);
        Assert.AreEqual(Amqp1FrameType.Amqp, result.Frame!.Type);
        Assert.AreEqual((ushort)3, result.Frame.Channel);
        CollectionAssert.AreEqual(encoded, result.Frame.Encode());
    }

    [TestMethod]
    public async Task ReadFrameAsync_DataOffsetAbove2_SkipsTheExtendedHeader()
    {
        var bytes = Convert.FromHexString("0000001003000005DEADBEEF00531845");

        var result = await Reader([bytes]).ReadFrameAsync(MaxFrameSize, TestContext.CancellationToken);

        Assert.AreEqual(Amqp1FrameReadOutcome.FrameRead, result.Outcome);
        Assert.AreEqual((ushort)5, result.Frame!.Channel);
        CollectionAssert.AreEqual(Convert.FromHexString("00531845"), result.Frame.Body.ToArray());
        Assert.IsInstanceOfType<Amqp1Close>(Amqp1FrameBody.Read(result.Frame).Performative);
    }

    [TestMethod]
    public async Task ReadFrameAsync_EmptyFrame_IsReadAsAHeartbeat()
    {
        var bytes = Convert.FromHexString("0000000802000000");

        var result = await Reader([bytes]).ReadFrameAsync(MaxFrameSize, TestContext.CancellationToken);

        Assert.IsTrue(result.Frame!.IsEmpty);
        Assert.AreEqual(Amqp1Frame.Empty.Type, result.Frame.Type);
        Assert.IsNull(Amqp1FrameBody.Read(result.Frame).Performative);
        Assert.IsTrue(Amqp1FrameBody.Read(result.Frame).Payload.IsEmpty);
        CollectionAssert.AreEqual(bytes, Amqp1Frame.Empty.Encode());
    }

    [TestMethod]
    public async Task ReadFrameAsync_FrameOverMaxFrameSize_IsRefusedWithoutReadingItsBody()
    {
        var connection = new InMemoryConnection([Convert.FromHexString("0000020102000000")]);

        var result = await new Amqp1FrameReader(connection).ReadFrameAsync(MaxFrameSize, TestContext.CancellationToken);

        Assert.AreEqual(Amqp1FrameReadOutcome.FrameTooLarge, result.Outcome);
        Assert.AreEqual(Amqp1ErrorCondition.FramingError, result.RefusalCondition);
        Assert.IsNull(result.Frame);
    }

    [TestMethod]
    public async Task ReadFrameAsync_FrameOfExactlyMaxFrameSize_IsRead()
    {
        byte[] bytes = [0x00, 0x00, 0x02, 0x00, 0x02, 0x00, 0x00, 0x00, .. new byte[504]];

        var result = await Reader([bytes]).ReadFrameAsync(MaxFrameSize, TestContext.CancellationToken);

        Assert.AreEqual(Amqp1FrameReadOutcome.FrameRead, result.Outcome);
        Assert.AreEqual(504, result.Frame!.Body.Length);
    }

    [TestMethod]
    [DataRow("0000000702000000", Amqp1FrameReadOutcome.FrameTooSmall, DisplayName = "size 7")]
    [DataRow("0000000801000000", Amqp1FrameReadOutcome.BadDataOffset, DisplayName = "DOFF 1")]
    [DataRow("0000000803000000", Amqp1FrameReadOutcome.BadDataOffset, DisplayName = "DOFF past the frame")]
    [DataRow("0000000802020000", Amqp1FrameReadOutcome.UnknownFrameType, DisplayName = "type 0x02")]
    public async Task ReadFrameAsync_BadHeader_IsRefusedAsAFramingError(string hex, Amqp1FrameReadOutcome expected)
    {
        var result = await Reader([Convert.FromHexString(hex)]).ReadFrameAsync(MaxFrameSize, TestContext.CancellationToken);

        Assert.AreEqual(expected, result.Outcome);
        Assert.AreEqual(Amqp1ErrorCondition.FramingError, result.RefusalCondition);
    }

    [TestMethod]
    [DataRow("", Amqp1FrameReadOutcome.ConnectionClosed, DisplayName = "between frames")]
    [DataRow("00000009", Amqp1FrameReadOutcome.ConnectionClosedMidFrame, DisplayName = "in the header")]
    [DataRow("0000000902000000", Amqp1FrameReadOutcome.ConnectionClosedMidFrame, DisplayName = "in the body")]
    public async Task ReadFrameAsync_ClientCloses_SaysWhere(string hex, Amqp1FrameReadOutcome expected)
    {
        var result = await Reader([Convert.FromHexString(hex)]).ReadFrameAsync(MaxFrameSize, TestContext.CancellationToken);

        Assert.AreEqual(expected, result.Outcome);
        Assert.IsNull(result.RefusalCondition);
    }

    [TestMethod]
    public async Task ReadFrameAsync_TwoFramesInOneRead_AreReadOneAtATime()
    {
        byte[] both = [.. Amqp1Frame.Empty.Encode(), .. Amqp1Frame.Carrying(0, new Amqp1SaslOutcome(0)).Encode()];
        var reader = Reader([both]);

        var first = await reader.ReadFrameAsync(MaxFrameSize, TestContext.CancellationToken);
        var second = await reader.ReadFrameAsync(MaxFrameSize, TestContext.CancellationToken);

        Assert.IsTrue(first.Frame!.IsEmpty);
        Assert.AreEqual(Amqp1FrameType.Sasl, second.Frame!.Type);
    }

    [TestMethod]
    public async Task ReadFrameAsync_MaxFrameSizeBelowTheHeader_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            async () => await Reader([]).ReadFrameAsync(7, TestContext.CancellationToken));
    }

    private static Amqp1FrameReader Reader(ReadOnlyMemory<byte>[] chunks) => new(new InMemoryConnection(chunks));
}
