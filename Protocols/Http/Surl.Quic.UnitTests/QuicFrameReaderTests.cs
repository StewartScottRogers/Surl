namespace Surl.Quic;

/// <summary>
/// The malformed frames <see cref="QuicFrameReader"/> refuses, each with the transport error
/// RFC 9000 names, and the packet types each frame may appear in (section 12.4, table 3).
/// </summary>
[TestClass]
public sealed class QuicFrameReaderTests
{
    [TestMethod]
    public void Read_TruncatedVarint_ThrowsFrameEncodingError()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read("0440"));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
        Assert.AreEqual(0x04UL, exception.FrameType);
    }

    [TestMethod]
    public void Read_TruncatedFrameType_ThrowsFrameEncodingErrorWithNoFrameType()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read("40"));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
        Assert.IsNull(exception.FrameType);
    }

    [TestMethod]
    [DataRow("1f")]
    [DataRow("4040")]
    [DataRow("80004000")]
    public void Read_FrameTypeUnknown_ThrowsFrameEncodingError(string hex)
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read(hex));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
    }

    [TestMethod]
    public void Read_ResetStreamAtReliableSizeAboveFinalSize_ThrowsFrameEncodingError()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read("2400000506"));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
        Assert.AreEqual(0x24UL, exception.FrameType);
    }

    [TestMethod]
    public void Read_ResetStreamAtReliableSizeEqualToFinalSize_ReadsIt()
    {
        var frame = (QuicResetStreamAtFrame)Read("2400070505").Single();

        Assert.AreEqual(new QuicResetStreamAtFrame(0, 7, 5, 5), frame);
    }

    [TestMethod]
    public void Read_FrameTypeNotInItsShortestEncoding_ThrowsProtocolViolation()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read("4001"));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
        Assert.AreEqual(0x01UL, exception.FrameType);
    }

    [TestMethod]
    [DataRow("080061", QuicPacketType.Initial)]
    [DataRow("1e", QuicPacketType.Handshake)]
    [DataRow("0200000000", QuicPacketType.ZeroRtt)]
    [DataRow("070174", QuicPacketType.ZeroRtt)]
    [DataRow("1b0102030405060708", QuicPacketType.ZeroRtt)]
    [DataRow("1d0000", QuicPacketType.Initial)]
    [DataRow("1003", QuicPacketType.Handshake)]
    [DataRow("2400000000", QuicPacketType.Initial)]
    [DataRow("2400000000", QuicPacketType.Handshake)]
    public void Read_FrameTypeUnknownInItsPacketType_ThrowsProtocolViolation(string hex, QuicPacketType packetType)
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => QuicFrameReader.Read(Convert.FromHexString(hex), packetType));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
        Assert.AreEqual((ulong)Convert.FromHexString(hex)[0], exception.FrameType);
    }

    [TestMethod]
    [DataRow("00", QuicPacketType.Initial)]
    [DataRow("01", QuicPacketType.ZeroRtt)]
    [DataRow("0200000000", QuicPacketType.Handshake)]
    [DataRow("0600016a", QuicPacketType.Initial)]
    [DataRow("1c000000", QuicPacketType.Handshake)]
    [DataRow("080061", QuicPacketType.ZeroRtt)]
    [DataRow("2400000000", QuicPacketType.ZeroRtt)]
    public void Read_FramePermittedInItsPacketType_ReadsIt(string hex, QuicPacketType packetType)
    {
        var frames = QuicFrameReader.Read(Convert.FromHexString(hex), packetType);

        Assert.HasCount(1, frames);
    }

    [TestMethod]
    public void Read_EmptyPayload_ThrowsProtocolViolation()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read(""));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
        Assert.IsNull(exception.FrameType);
    }

    [TestMethod]
    [DataRow(QuicPacketType.Retry)]
    [DataRow(QuicPacketType.VersionNegotiation)]
    public void Read_PacketTypeThatCarriesNoFrames_Throws(QuicPacketType packetType)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicFrameReader.Read(new byte[] { 1 }, packetType));
    }

    [TestMethod]
    public void Read_StreamFrameEndingBeyond2To62Minus1_ThrowsFrameEncodingError()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read("0e00ffffffffffffffff0161"));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
        Assert.AreEqual(0x0eUL, exception.FrameType);
    }

    [TestMethod]
    public void Read_StreamFrameEndingExactlyAt2To62Minus1_ReadsIt()
    {
        var frames = Read("0e00fffffffffffffffe0161");

        Assert.AreEqual(QuicVariableLengthInteger.MaxValue - 1, ((QuicStreamFrame)frames[0]).Offset);
    }

    [TestMethod]
    public void Read_CryptoFrameEndingBeyond2To62Minus1_ThrowsCryptoBufferExceeded()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read("06ffffffffffffffff0161"));

        Assert.AreEqual(QuicTransportErrorCode.CryptoBufferExceeded, exception.ErrorCode);
        Assert.AreEqual(0x06UL, exception.FrameType);
    }

    [TestMethod]
    public void Read_FrameAnnouncingMoreBytesThanRemain_ThrowsFrameEncodingError()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read("06000561"));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
    }

    [TestMethod]
    [DataRow("0202000003")]
    [DataRow("02050001050000")]
    [DataRow("02050001010004")]
    public void Read_AckRangeThatUnderflows_ThrowsFrameEncodingError(string hex)
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read(hex));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
        Assert.AreEqual(0x02UL, exception.FrameType);
    }

    [TestMethod]
    public void Read_AckRangesReachingPacketNumberZero_ReadsThem()
    {
        var ack = (QuicAckFrame)Read("02050001010002")[0];

        CollectionAssert.AreEqual(new[] { new QuicAckRange(4, 5), new QuicAckRange(0, 2) }, ack.Ranges.ToArray());
    }

    [TestMethod]
    [DataRow("12d000000000000001")]
    [DataRow("17d000000000000001")]
    public void Read_StreamCountAbove2To60_ThrowsFrameEncodingError(string hex)
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read(hex));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
    }

    [TestMethod]
    public void Read_NewTokenEmpty_ThrowsFrameEncodingError()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read("0700"));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(21)]
    public void Read_NewConnectionIdOfAnInvalidLength_ThrowsFrameEncodingError(int length)
    {
        var hex = "180100" + length.ToString("x2") + new string('0', 2 * (length + 16));

        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read(hex));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
        Assert.AreEqual(0x18UL, exception.FrameType);
    }

    [TestMethod]
    public void Read_NewConnectionIdOfTwentyBytes_ReadsIt()
    {
        var hex = "180100" + "14" + new string('0', 2 * (20 + 16));

        var frame = (QuicNewConnectionIdFrame)Read(hex)[0];

        Assert.AreEqual(20, frame.ConnectionId.Length);
        Assert.AreEqual(QuicNewConnectionIdFrame.StatelessResetTokenLength, frame.StatelessResetToken.Length);
    }

    [TestMethod]
    public void Read_NewConnectionIdRetiringBeyondItsSequenceNumber_ThrowsFrameEncodingError()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read("180102"));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
    }

    [TestMethod]
    public void Read_PaddingBetweenFrames_FoldsEachRunIntoOneFrame()
    {
        var frames = Read("00000100");

        Assert.HasCount(3, frames);
        Assert.AreEqual(new QuicPaddingFrame(2), frames[0]);
        Assert.IsInstanceOfType<QuicPingFrame>(frames[1]);
        Assert.AreEqual(new QuicPaddingFrame(1), frames[2]);
    }

    [TestMethod]
    public void Read_TruncatedFrameTypeAfterAFrame_ReportsNoFrameType()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read("0140"));

        Assert.IsNull(exception.FrameType);
    }

    [TestMethod]
    public void Read_DatagramFrameWithoutLength_TakesTheRestOfThePayload()
    {
        var frames = Read("30010203");

        var frame = (QuicDatagramFrame)frames.Single();
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, frame.Data.ToArray());
        Assert.IsFalse(frame.HasLength);
        Assert.AreEqual("30010203", Convert.ToHexString(frame.ToBytes()));
    }

    [TestMethod]
    public void Read_DatagramFrameWithLength_TakesItsLengthAndReadsOn()
    {
        var frames = Read("31020102" + "01");

        Assert.HasCount(2, frames);
        var frame = (QuicDatagramFrame)frames[0];
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, frame.Data.ToArray());
        Assert.IsTrue(frame.HasLength);
        Assert.AreEqual("31020102", Convert.ToHexString(frame.ToBytes()));
        Assert.IsInstanceOfType<QuicPingFrame>(frames[1]);
    }

    [TestMethod]
    [DataRow("3000", QuicPacketType.Initial)]
    [DataRow("3100", QuicPacketType.Handshake)]
    public void Read_DatagramFrameOutsideZeroRttAndOneRtt_ThrowsProtocolViolation(string hex, QuicPacketType packetType)
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => QuicFrameReader.Read(Convert.FromHexString(hex), packetType));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
    }

    [TestMethod]
    public void Read_DatagramFrameInZeroRtt_ReadsIt()
    {
        var frames = QuicFrameReader.Read(Convert.FromHexString("3100"), QuicPacketType.ZeroRtt);

        Assert.IsEmpty(((QuicDatagramFrame)frames.Single()).Data.ToArray());
    }

    [TestMethod]
    [DataRow("20")]
    [DataRow("2f")]
    [DataRow("32")]
    public void Read_FrameTypeBetweenOrAfterTheDefinedOnes_ThrowsFrameEncodingError(string hex)
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read(hex));

        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode);
    }

    [TestMethod]
    public void Read_DatagramFrameTypeNotInItsShortestEncoding_ThrowsProtocolViolation()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => Read("403000"));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
    }

    private static IReadOnlyList<QuicFrame> Read(string hex) =>
        QuicFrameReader.Read(Convert.FromHexString(hex), QuicPacketType.OneRtt);
}
