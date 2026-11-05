namespace Surl.Quic;

/// <summary>
/// Round-trips each frame type of RFC 9000 section 19 through <see cref="QuicFrameWriter"/> and
/// <see cref="QuicFrameReader"/>, and checks what the writer refuses.
/// </summary>
[TestClass]
public sealed class QuicFrameWriterTests
{
    private static readonly byte[] Eight = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] Sixteen = [.. Enumerable.Range(0, 16).Select(value => (byte)value)];

    public static IEnumerable<object[]> EveryFrameType =>
    [
        [0x00, new QuicPaddingFrame(3), "000000"],
        [0x01, new QuicPingFrame(), "01"],
        [0x02, new QuicAckFrame([new QuicAckRange(5, 9), new QuicAckRange(1, 2)], 7, null), "02090701040101"],
        [0x03, new QuicAckFrame([new QuicAckRange(0, 0)], 0, new QuicEcnCounts(1, 2, 3)), "0300000000010203"],
        [0x04, new QuicResetStreamFrame(4, 0x10c, 100), "0404410c4064"],
        [0x05, new QuicStopSendingFrame(8, 3), "050803"],
        [0x06, new QuicCryptoFrame(0x40, new byte[] { 0xaa, 0xbb }), "06404002aabb"],
        [0x07, new QuicNewTokenFrame(new byte[] { 0x74 }), "070174"],
        [0x08, new QuicStreamFrame(0, 0, new byte[] { 0x61 }, Fin: false, HasLength: false), "080061"],
        [0x09, new QuicStreamFrame(0, 0, new byte[] { 0x61 }, Fin: true, HasLength: false), "090061"],
        [0x0a, new QuicStreamFrame(4, 0, new byte[] { 0x61 }, Fin: false), "0a040161"],
        [0x0b, new QuicStreamFrame(4, 0, new byte[] { 0x61 }, Fin: true), "0b040161"],
        [0x0c, new QuicStreamFrame(4, 2, new byte[] { 0x61 }, Fin: false, HasLength: false), "0c040261"],
        [0x0d, new QuicStreamFrame(4, 2, new byte[] { 0x61 }, Fin: true, HasLength: false), "0d040261"],
        [0x0e, new QuicStreamFrame(4, 2, new byte[] { 0x61 }, Fin: false), "0e04020161"],
        [0x0f, new QuicStreamFrame(4, 2, ReadOnlyMemory<byte>.Empty, Fin: true), "0f040200"],
        [0x10, new QuicMaxDataFrame(1048576), "1080100000"],
        [0x11, new QuicMaxStreamDataFrame(0, 262144), "110080040000"],
        [0x12, new QuicMaxStreamsFrame(Bidirectional: true, 100), "124064"],
        [0x13, new QuicMaxStreamsFrame(Bidirectional: false, 3), "1303"],
        [0x14, new QuicDataBlockedFrame(9), "1409"],
        [0x15, new QuicStreamDataBlockedFrame(4, 9), "150409"],
        [0x16, new QuicStreamsBlockedFrame(Bidirectional: true, 1UL << 60), "16d000000000000000"],
        [0x17, new QuicStreamsBlockedFrame(Bidirectional: false, 3), "1703"],
        [0x18, new QuicNewConnectionIdFrame(1, 0, Eight, Sixteen), "18010008" + "0102030405060708" + "000102030405060708090a0b0c0d0e0f"],
        [0x19, new QuicRetireConnectionIdFrame(1), "1901"],
        [0x1a, new QuicPathChallengeFrame(Eight), "1a0102030405060708"],
        [0x1b, new QuicPathResponseFrame(Eight), "1b0102030405060708"],
        [0x1c, new QuicConnectionCloseFrame(IsApplicationError: false, 0x0a, 0x08, "no"u8.ToArray()), "1c0a08026e6f"],
        [0x1d, new QuicConnectionCloseFrame(IsApplicationError: true, 0x0100, 0, ReadOnlyMemory<byte>.Empty), "1d410000"],
        [0x1e, new QuicHandshakeDoneFrame(), "1e"],
    ];

    [TestMethod]
    [DynamicData(nameof(EveryFrameType))]
    public void Write_EachFrameType_WritesItsBytesAndReadsBackAsTheSameFrame(int frameType, QuicFrame frame, string expectedHex)
    {
        var bytes = frame.ToBytes();
        var frames = QuicFrameReader.Read(bytes, QuicPacketType.OneRtt);

        Assert.AreEqual(frameType, bytes[0]);
        Assert.AreEqual(expectedHex, Convert.ToHexStringLower(bytes));
        Assert.HasCount(1, frames);
        Assert.AreEqual(frame.GetType(), frames[0].GetType());
        Assert.AreEqual(expectedHex, Convert.ToHexStringLower(frames[0].ToBytes()));
    }

    [TestMethod]
    public void Write_SeveralFrames_WritesThemInOrderAndReadsBackTheirFields()
    {
        QuicFrame[] frames =
        [
            new QuicAckFrame([new QuicAckRange(5, 9), new QuicAckRange(1, 2)], 7, new QuicEcnCounts(1, 2, 3)),
            new QuicStreamFrame(4, 2, new byte[] { 0x61, 0x62 }, Fin: true),
            new QuicConnectionCloseFrame(IsApplicationError: false, 0x0a, 0x08, "no"u8.ToArray()),
            new QuicPaddingFrame(2),
        ];

        var read = QuicFrameReader.Read(QuicFrameWriter.Write(frames), QuicPacketType.OneRtt);

        Assert.HasCount(4, read);
        var ack = (QuicAckFrame)read[0];
        Assert.AreEqual(9UL, ack.LargestAcknowledged);
        Assert.AreEqual(7UL, ack.AckDelay);
        CollectionAssert.AreEqual(new[] { new QuicAckRange(5, 9), new QuicAckRange(1, 2) }, ack.Ranges.ToArray());
        Assert.AreEqual(new QuicEcnCounts(1, 2, 3), ack.EcnCounts);
        var stream = (QuicStreamFrame)read[1];
        Assert.AreEqual(4UL, stream.StreamId);
        Assert.AreEqual(2UL, stream.Offset);
        Assert.IsTrue(stream.Fin);
        Assert.IsTrue(stream.HasLength);
        CollectionAssert.AreEqual(new byte[] { 0x61, 0x62 }, stream.Data.ToArray());
        var close = (QuicConnectionCloseFrame)read[2];
        Assert.IsFalse(close.IsApplicationError);
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, close.ErrorCode);
        Assert.AreEqual(8UL, close.FrameType);
        Assert.AreEqual("no", System.Text.Encoding.UTF8.GetString(close.ReasonPhrase.Span));
        Assert.AreEqual(new QuicPaddingFrame(2), read[3]);
    }

    [TestMethod]
    public void Write_NullFrames_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicFrameWriter.Write(null!));
    }

    [TestMethod]
    public void ToBytes_PaddingOfNoBytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicPaddingFrame(0).ToBytes());
    }

    [TestMethod]
    public void ToBytes_AckWithoutRanges_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicAckFrame([], 0, null).ToBytes());
    }

    [TestMethod]
    public void ToBytes_AckRangeSmallestAboveLargest_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new QuicAckFrame([new QuicAckRange(3, 2)], 0, null).ToBytes());
    }

    [TestMethod]
    public void ToBytes_AckRangesWithoutAGap_Throws()
    {
        var frame = new QuicAckFrame([new QuicAckRange(5, 9), new QuicAckRange(1, 4)], 0, null);

        Assert.ThrowsExactly<ArgumentException>(() => frame.ToBytes());
    }

    [TestMethod]
    public void ToBytes_EmptyNewToken_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicNewTokenFrame(ReadOnlyMemory<byte>.Empty).ToBytes());
    }

    [TestMethod]
    public void ToBytes_NewConnectionIdRetiringBeyondItsSequenceNumber_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicNewConnectionIdFrame(1, 2, Eight, Sixteen).ToBytes());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(21)]
    public void ToBytes_NewConnectionIdOfAnInvalidLength_Throws(int length)
    {
        var frame = new QuicNewConnectionIdFrame(1, 0, new byte[length], Sixteen);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => frame.ToBytes());
    }

    [TestMethod]
    public void ToBytes_NewConnectionIdWithAShortResetToken_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicNewConnectionIdFrame(1, 0, Eight, Eight).ToBytes());
    }

    [TestMethod]
    public void ToBytes_PathChallengeNotEightBytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuicPathChallengeFrame(Sixteen).ToBytes());
    }

    [TestMethod]
    public void ToBytes_StreamIdAbove2To62Minus1_Throws()
    {
        var frame = new QuicMaxStreamDataFrame(QuicVariableLengthInteger.MaxValue + 1, 0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => frame.ToBytes());
    }
}
