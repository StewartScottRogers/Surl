namespace Surl.Protocol.Http;

[TestClass]
public sealed class Http2FrameEncoderTests
{
    // The frames the server sends, against the bytes RFC 9113 sections 4.1 and 6 define. Each one
    // pinned here was sent to pinned upstream curl 8.21.0 in BL-364 and completed the exchange
    // (ADR-0081, "Measured").

    [TestMethod]
    public void Encode_EmptySettings_IsNineBytesOfType4OnStream0()
    {
        AssertEncodes("000000040000000000", new Http2SettingsFrame(false, []));
    }

    [TestMethod]
    public void Encode_SettingsAcknowledgement_HasTheAckFlagAndNoPayload()
    {
        AssertEncodes("000000040100000000", Http2SettingsFrame.Acknowledgement);
    }

    [TestMethod]
    public void Encode_TheServersSettings_WritesEachEntryAsIdentifierAndValue()
    {
        AssertEncodes(
            "00000C040000000000" + "000300000064" + "000600100000",
            new Http2SettingsFrame(false, [
                new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 100),
                new Http2Setting(Http2SettingIdentifier.MaxHeaderListSize, 1048576),
            ]));
    }

    [TestMethod]
    public void Encode_HeadersEndingTheStreamAndTheBlock_SetsBothFlags()
    {
        AssertEncodes("000001010500000001" + "88", new Http2HeadersFrame(1, [0x88], EndStream: true, EndHeaders: true));
    }

    [TestMethod]
    public void Encode_HeadersFollowedByContinuation_SplitsTheBlockOverBothFrames()
    {
        AssertEncodes("000001010100000001" + "88", new Http2HeadersFrame(1, [0x88], EndStream: true, EndHeaders: false));
        AssertEncodes("000005090400000001" + "0001780179", new Http2ContinuationFrame(1, Convert.FromHexString("0001780179"), EndHeaders: true));
    }

    [TestMethod]
    public void Encode_DataEndingTheStream_CarriesTheContent()
    {
        AssertEncodes("000002000100000001" + "6869", new Http2DataFrame(1, "hi"u8.ToArray(), EndStream: true));
    }

    [TestMethod]
    public void Encode_PingAcknowledgement_EchoesTheEightBytes()
    {
        AssertEncodes("000008060100000000" + "0102030405060708", new Http2PingFrame(true, 0x0102030405060708));
    }

    [TestMethod]
    [DataRow(0x1u, "00000001")]
    [DataRow(0x7u, "00000007")]
    [DataRow(0xBu, "0000000B")]
    [DataRow(0xDu, "0000000D")]
    public void Encode_RstStream_CarriesTheErrorCode(uint errorCode, string errorCodeHex)
    {
        AssertEncodes("000004030000000001" + errorCodeHex, new Http2RstStreamFrame(1, (Http2ErrorCode)errorCode));
    }

    [TestMethod]
    public void Encode_GoAway_NamesTheLastStreamAndTheErrorCode()
    {
        AssertEncodes("000008070000000000" + "00000001" + "00000000", new Http2GoAwayFrame(1, Http2ErrorCode.NoError, []));
    }

    [TestMethod]
    public void Encode_GoAwayWithDebugData_AppendsIt()
    {
        AssertEncodes(
            "000011070000000000" + "00000000" + "00000000" + "73687574646F776E00",
            new Http2GoAwayFrame(0, Http2ErrorCode.NoError, "shutdown\0"u8.ToArray()));
    }

    [TestMethod]
    [DataRow(0, 1048510465, "000004080000000000" + "3E7F0001")]
    [DataRow(1, 16384, "000004080000000001" + "00004000")]
    public void Encode_WindowUpdate_CarriesTheIncrement(int streamId, int increment, string hex)
    {
        AssertEncodes(hex, new Http2WindowUpdateFrame(streamId, increment));
    }

    // Frames the server never sends, written for completeness and read back unchanged.

    [TestMethod]
    public void Encode_PaddedHeadersWithPriority_RoundTripsThroughTheParser()
    {
        AssertRoundTrips(
            "000009012800000005" + "02" + "80000003FF" + "88" + "0000",
            new Http2HeadersFrame(5, [0x88], EndStream: false, EndHeaders: false, new Http2Priority(true, 3, 256), PaddingLength: 2));
    }

    [TestMethod]
    public void Encode_PaddedData_RoundTripsThroughTheParser()
    {
        AssertRoundTrips("000006000900000003" + "03" + "6869" + "000000", new Http2DataFrame(3, "hi"u8.ToArray(), EndStream: true, PaddingLength: 3));
    }

    [TestMethod]
    public void Encode_PaddedPushPromise_RoundTripsThroughTheParser()
    {
        AssertRoundTrips(
            "000007050C00000001" + "01" + "00000002" + "88" + "00",
            new Http2PushPromiseFrame(1, 2, [0x88], EndHeaders: true, PaddingLength: 1));
    }

    [TestMethod]
    public void Encode_UnpaddedPushPromise_HasNoFlags()
    {
        AssertEncodes("000004050000000001" + "00000002", new Http2PushPromiseFrame(1, 2, [], EndHeaders: false));
    }

    [TestMethod]
    public void Encode_Priority_WritesTheDependencyAndTheWeightLessOne()
    {
        AssertEncodes("000005020000000003" + "0000000100", new Http2PriorityFrame(3, new Http2Priority(false, 1, 1)));
    }

    [TestMethod]
    public void Encode_PingWithoutAck_HasNoFlags()
    {
        AssertEncodes("000008060000000000" + "0000000000000001", new Http2PingFrame(false, 1));
    }

    [TestMethod]
    public void Encode_ContinuationNotEndingTheBlock_HasNoFlags()
    {
        AssertEncodes("000000090000000001", new Http2ContinuationFrame(1, [], EndHeaders: false));
    }

    [TestMethod]
    public void Encode_NegativeStreamId_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameEncoder.Encode(new Http2WindowUpdateFrame(-1, 1)));
    }

    [TestMethod]
    public void Encode_PayloadLongerThan24BitsCanSay_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameEncoder.Encode(new Http2DataFrame(1, new byte[16777216], EndStream: false)));
    }

    [TestMethod]
    public void Encode_PayloadOfTheLargest24BitLength_IsWritten()
    {
        var bytes = Http2FrameEncoder.Encode(new Http2DataFrame(1, new byte[16777215], EndStream: false));

        CollectionAssert.AreEqual(Convert.FromHexString("FFFFFF000000000001"), bytes[..Http2FrameHeader.EncodedLength]);
    }

    [TestMethod]
    public void Encode_NullFrame_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Http2FrameEncoder.Encode(null!));
    }

    private static void AssertEncodes(string expectedHex, Http2Frame frame)
    {
        Assert.AreEqual(expectedHex, Convert.ToHexString(Http2FrameEncoder.Encode(frame)));
    }

    private static void AssertRoundTrips(string expectedHex, Http2Frame frame)
    {
        var bytes = Http2FrameEncoder.Encode(frame);
        Assert.AreEqual(expectedHex, Convert.ToHexString(bytes));

        var result = Http2FrameParser.Parse(Http2FrameHeader.Read(bytes), bytes[Http2FrameHeader.EncodedLength..]);

        Assert.AreEqual(Http2FrameReadOutcome.FrameRead, result.Outcome);
        Assert.AreEqual(Convert.ToHexString(bytes), Convert.ToHexString(Http2FrameEncoder.Encode(result.Frame!)));
    }
}
