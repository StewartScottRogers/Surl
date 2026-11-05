namespace Surl.Protocol.Http;

[TestClass]
public sealed class Http2FrameParserTests
{
    // Each malformed case is the frame's bytes in hex (header and payload) and the RFC 9113 error it
    // maps to: the error code and whether it ends the connection (GOAWAY) or one stream (RST_STREAM).

    [TestMethod]
    [DataRow("000005040000000000" + "0003000000", DisplayName = "SETTINGS length 5")]
    [DataRow("000009040000000000" + "000300000064" + "000400", DisplayName = "SETTINGS length 9")]
    public void Parse_SettingsWhoseLengthIsNotAMultipleOfSix_IsAFrameSizeConnectionError(string hex) =>
        AssertMalformed(hex, Http2FrameMalformation.SettingsLengthNotMultipleOfSix, Http2ErrorCode.FrameSizeError, connectionError: true);

    [TestMethod]
    public void Parse_SettingsAckWithAPayload_IsAFrameSizeConnectionError() =>
        AssertMalformed("000006040100000000" + "000300000064", Http2FrameMalformation.SettingsAckWithPayload, Http2ErrorCode.FrameSizeError, connectionError: true);

    [TestMethod]
    [DataRow("000007060000000000" + "01020304050607", DisplayName = "PING length 7")]
    [DataRow("000009060100000000" + "010203040506070809", DisplayName = "PING ACK length 9")]
    public void Parse_PingThatIsNotEightBytes_IsAFrameSizeConnectionError(string hex) =>
        AssertMalformed(hex, Http2FrameMalformation.PingLengthNotEight, Http2ErrorCode.FrameSizeError, connectionError: true);

    [TestMethod]
    public void Parse_WindowUpdateWithIncrementZeroOnTheConnection_IsAProtocolConnectionError() =>
        AssertMalformed("000004080000000000" + "00000000", Http2FrameMalformation.WindowUpdateIncrementZeroOnConnection, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    public void Parse_WindowUpdateWithIncrementZeroOnAStream_IsAProtocolStreamError() =>
        AssertMalformed("000004080000000005" + "80000000", Http2FrameMalformation.WindowUpdateIncrementZeroOnStream, Http2ErrorCode.ProtocolError, connectionError: false);

    [TestMethod]
    [DataRow("000003000800000001" + "036869", DisplayName = "DATA pad 3 of 3")]
    [DataRow("000003000800000001" + "076869", DisplayName = "DATA pad 7 of 3")]
    [DataRow("000002010C00000001" + "0288", DisplayName = "HEADERS pad 2 of 2")]
    [DataRow("000007012800000001" + "02000000000F88", DisplayName = "HEADERS with priority, pad 2 of 1 left")]
    [DataRow("000006050C00000001" + "020000000288", DisplayName = "PUSH_PROMISE pad 2 of 1 left")]
    public void Parse_PaddingLongerThanThePayload_IsAProtocolConnectionError(string hex) =>
        AssertMalformed(hex, Http2FrameMalformation.PaddingTooLong, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    [DataRow("000000000800000001", DisplayName = "padded DATA with no Pad Length byte")]
    [DataRow("000004012400000001" + "00000000", DisplayName = "HEADERS with PRIORITY but 4 bytes")]
    [DataRow("000004050C00000001" + "00000002", DisplayName = "padded PUSH_PROMISE with 3 bytes after Pad Length")]
    [DataRow("000003050400000001" + "000002", DisplayName = "PUSH_PROMISE with 3 bytes")]
    public void Parse_PayloadTooShortForItsFixedFields_IsAFrameSizeConnectionError(string hex) =>
        AssertMalformed(hex, Http2FrameMalformation.PayloadTooShortForFields, Http2ErrorCode.FrameSizeError, connectionError: true);

    [TestMethod]
    public void Parse_DataOnStreamZero_IsAProtocolConnectionError() =>
        AssertMalformed("000002000100000000" + "6869", Http2FrameMalformation.DataOnStreamZero, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    public void Parse_HeadersOnStreamZero_IsAProtocolConnectionError() =>
        AssertMalformed("000001010500000000" + "88", Http2FrameMalformation.HeadersOnStreamZero, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    [DataRow("000006040000000001" + "000300000064", DisplayName = "SETTINGS on stream 1")]
    [DataRow("000000040100000003", DisplayName = "SETTINGS ACK on stream 3")]
    public void Parse_SettingsOnANonZeroStream_IsAProtocolConnectionError(string hex) =>
        AssertMalformed(hex, Http2FrameMalformation.SettingsOnStream, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    public void Parse_PingOnANonZeroStream_IsAProtocolConnectionError() =>
        AssertMalformed("000008060000000001" + "0102030405060708", Http2FrameMalformation.PingOnStream, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    public void Parse_PriorityOnStreamZero_IsAProtocolConnectionError() =>
        AssertMalformed("000005020000000000" + "000000010F", Http2FrameMalformation.PriorityOnStreamZero, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    public void Parse_PriorityWhoseLengthIsNotFive_IsAFrameSizeStreamError() =>
        AssertMalformed("000004020000000003" + "00000001", Http2FrameMalformation.PriorityLengthNotFive, Http2ErrorCode.FrameSizeError, connectionError: false);

    [TestMethod]
    public void Parse_PriorityDependingOnItsOwnStream_IsAProtocolStreamError() =>
        AssertMalformed("000005020000000003" + "000000030F", Http2FrameMalformation.PriorityDependsOnItself, Http2ErrorCode.ProtocolError, connectionError: false);

    [TestMethod]
    public void Parse_RstStreamOnStreamZero_IsAProtocolConnectionError() =>
        AssertMalformed("000004030000000000" + "00000008", Http2FrameMalformation.RstStreamOnStreamZero, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    public void Parse_RstStreamWhoseLengthIsNotFour_IsAFrameSizeConnectionError() =>
        AssertMalformed("000005030000000001" + "0000000800", Http2FrameMalformation.RstStreamLengthNotFour, Http2ErrorCode.FrameSizeError, connectionError: true);

    [TestMethod]
    [DataRow("000002", DisplayName = "ENABLE_PUSH 2")]
    [DataRow("FFFFFFFF", DisplayName = "ENABLE_PUSH 2^32-1")]
    public void Parse_SettingsEnablePushOtherThanZeroOrOne_IsAProtocolConnectionError(string valueHex) =>
        AssertMalformed("000006040000000000" + "0002" + valueHex.PadLeft(8, '0'), Http2FrameMalformation.SettingsEnablePushNotZeroOrOne, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    public void Parse_SettingsInitialWindowSizeAboveTwoToTheThirtyFirstMinusOne_IsAFlowControlConnectionError() =>
        AssertMalformed("00000C040000000000" + "000300000064" + "000480000000", Http2FrameMalformation.SettingsInitialWindowSizeTooLarge, Http2ErrorCode.FlowControlError, connectionError: true);

    [TestMethod]
    [DataRow("00003FFF", DisplayName = "MAX_FRAME_SIZE 16383")]
    [DataRow("01000000", DisplayName = "MAX_FRAME_SIZE 16777216")]
    public void Parse_SettingsMaxFrameSizeOutOfRange_IsAProtocolConnectionError(string valueHex) =>
        AssertMalformed("000006040000000000" + "0005" + valueHex, Http2FrameMalformation.SettingsMaxFrameSizeOutOfRange, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    public void Parse_PushPromiseOnStreamZero_IsAProtocolConnectionError() =>
        AssertMalformed("000005050400000000" + "0000000288", Http2FrameMalformation.PushPromiseOnStreamZero, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    public void Parse_GoAwayOnANonZeroStream_IsAProtocolConnectionError() =>
        AssertMalformed("000008070000000001" + "0000000100000000", Http2FrameMalformation.GoAwayOnStream, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    public void Parse_GoAwayShorterThanEightBytes_IsAFrameSizeConnectionError() =>
        AssertMalformed("000007070000000000" + "00000001000000", Http2FrameMalformation.GoAwayTooShort, Http2ErrorCode.FrameSizeError, connectionError: true);

    [TestMethod]
    [DataRow("000003080000000001" + "000001", DisplayName = "WINDOW_UPDATE length 3")]
    [DataRow("000005080000000000" + "0000000100", DisplayName = "WINDOW_UPDATE length 5")]
    public void Parse_WindowUpdateWhoseLengthIsNotFour_IsAFrameSizeConnectionError(string hex) =>
        AssertMalformed(hex, Http2FrameMalformation.WindowUpdateLengthNotFour, Http2ErrorCode.FrameSizeError, connectionError: true);

    [TestMethod]
    public void Parse_ContinuationOnStreamZero_IsAProtocolConnectionError() =>
        AssertMalformed("000001090400000000" + "88", Http2FrameMalformation.ContinuationOnStreamZero, Http2ErrorCode.ProtocolError, connectionError: true);

    [TestMethod]
    public void Parse_PaddedDataFrame_StripsThePaddingAndKeepsItsLength()
    {
        var frame = (Http2DataFrame)ParseFrame("000006000900000003" + "03" + "6869" + "000000");

        Assert.AreEqual(3, frame.StreamId);
        CollectionAssert.AreEqual("hi"u8.ToArray(), frame.Data);
        Assert.IsTrue(frame.EndStream);
        Assert.AreEqual(3, frame.PaddingLength);
        Assert.AreEqual(6, frame.PayloadLength);
    }

    [TestMethod]
    public void Parse_PaddedDataFrameWithZeroPadding_IsPaddedWithLengthZero()
    {
        var frame = (Http2DataFrame)ParseFrame("000001000800000001" + "00");

        Assert.IsEmpty(frame.Data);
        Assert.IsFalse(frame.EndStream);
        Assert.AreEqual(0, frame.PaddingLength);
    }

    [TestMethod]
    public void Parse_PaddedHeadersWithPriority_ReadsThePriorityAndTheFragment()
    {
        var frame = (Http2HeadersFrame)ParseFrame("000009012800000005" + "02" + "80000003FF" + "88" + "0000");

        Assert.AreEqual(5, frame.StreamId);
        CollectionAssert.AreEqual(new byte[] { 0x88 }, frame.FieldBlockFragment);
        Assert.IsFalse(frame.EndStream);
        Assert.IsFalse(frame.EndHeaders);
        Assert.AreEqual(new Http2Priority(true, 3, 256), frame.Priority);
        Assert.AreEqual(2, frame.PaddingLength);
    }

    [TestMethod]
    public void Parse_HeadersWithPriorityOnItsOwnStream_IsReadForTheConnectionToJudge()
    {
        var frame = (Http2HeadersFrame)ParseFrame("000006012500000003" + "000000030F" + "88");

        Assert.AreEqual(new Http2Priority(false, 3, 16), frame.Priority);
        Assert.IsTrue(frame.EndStream);
        Assert.IsTrue(frame.EndHeaders);
    }

    [TestMethod]
    public void Parse_PriorityFrame_ReadsItsFields()
    {
        Assert.AreEqual(new Http2PriorityFrame(3, new Http2Priority(false, 1, 1)), ParseFrame("000005020000000003" + "0000000100"));
    }

    [TestMethod]
    public void Parse_RstStream_ReadsAnUnknownErrorCodeAsItsNumber()
    {
        Assert.AreEqual(new Http2RstStreamFrame(1, (Http2ErrorCode)0xFF), ParseFrame("000004030000000001" + "000000FF"));
    }

    [TestMethod]
    public void Parse_SettingsWithUnknownAndBoundaryValues_KeepsEveryEntryInOrder()
    {
        var frame = (Http2SettingsFrame)ParseFrame(
            "00001E040000000000" + "000200000001" + "00047FFFFFFF" + "000500004000" + "000500FFFFFF" + "00FF12345678");

        CollectionAssert.AreEqual(
            new[]
            {
                new Http2Setting(Http2SettingIdentifier.EnablePush, 1),
                new Http2Setting(Http2SettingIdentifier.InitialWindowSize, int.MaxValue),
                new Http2Setting(Http2SettingIdentifier.MaxFrameSize, 16384),
                new Http2Setting(Http2SettingIdentifier.MaxFrameSize, 16777215),
                new Http2Setting((Http2SettingIdentifier)0xFF, 0x12345678),
            },
            frame.Settings.ToArray());
    }

    [TestMethod]
    public void Parse_PaddedPushPromise_ReadsThePromisedStreamIgnoringItsReservedBit()
    {
        var frame = (Http2PushPromiseFrame)ParseFrame("000007050C00000001" + "01" + "80000002" + "88" + "00");

        Assert.AreEqual(1, frame.StreamId);
        Assert.AreEqual(2, frame.PromisedStreamId);
        CollectionAssert.AreEqual(new byte[] { 0x88 }, frame.FieldBlockFragment);
        Assert.IsTrue(frame.EndHeaders);
        Assert.AreEqual(1, frame.PaddingLength);
    }

    [TestMethod]
    public void Parse_UnpaddedPushPromise_HasNoPadding()
    {
        var frame = (Http2PushPromiseFrame)ParseFrame("000004050000000001" + "00000002");

        Assert.IsEmpty(frame.FieldBlockFragment);
        Assert.IsFalse(frame.EndHeaders);
        Assert.IsNull(frame.PaddingLength);
    }

    [TestMethod]
    public void Parse_PingAck_ReadsTheOpaqueData()
    {
        Assert.AreEqual(new Http2PingFrame(true, 0x0102030405060708), ParseFrame("000008060100000000" + "0102030405060708"));
    }

    [TestMethod]
    public void Parse_GoAway_IgnoresTheReservedBitOfTheLastStream()
    {
        var frame = (Http2GoAwayFrame)ParseFrame("000008070000000000" + "80000007" + "0000000B");

        Assert.AreEqual(7, frame.LastStreamId);
        Assert.AreEqual(Http2ErrorCode.EnhanceYourCalm, frame.ErrorCode);
        Assert.IsEmpty(frame.DebugData);
    }

    [TestMethod]
    public void Parse_FrameHeaderWithTheReservedBitSet_IgnoresIt()
    {
        Assert.AreEqual(new Http2WindowUpdateFrame(1, 0x7FFFFFFF), ParseFrame("000004080080000001" + "FFFFFFFF"));
    }

    [TestMethod]
    public void Parse_Continuation_ReadsTheFragment()
    {
        var frame = (Http2ContinuationFrame)ParseFrame("000005090400000001" + "0001780179");

        CollectionAssert.AreEqual(Convert.FromHexString("0001780179"), frame.FieldBlockFragment);
        Assert.IsTrue(frame.EndHeaders);
    }

    [TestMethod]
    [DataRow((byte)0x0, true)]
    [DataRow((byte)0x9, true)]
    [DataRow((byte)0xA, false)]
    [DataRow((byte)0xFF, false)]
    public void IsDefinedType_SaysWhetherRfc9113DefinesTheType(byte type, bool expected)
    {
        Assert.AreEqual(expected, Http2FrameParser.IsDefinedType(type));
    }

    private static Http2Frame ParseFrame(string hex)
    {
        var result = Parse(hex);
        Assert.AreEqual(Http2FrameReadOutcome.FrameRead, result.Outcome, $"Malformation: {result.Malformation}");

        return result.Frame!;
    }

    private static Http2FrameReadResult Parse(string hex)
    {
        var bytes = Convert.FromHexString(hex);
        var header = Http2FrameHeader.Read(bytes);
        Assert.AreEqual(bytes.Length - Http2FrameHeader.EncodedLength, header.Length, "The test's frame length is wrong.");

        return Http2FrameParser.Parse(header, bytes[Http2FrameHeader.EncodedLength..]);
    }

    private static void AssertMalformed(string hex, Http2FrameMalformation malformation, Http2ErrorCode errorCode, bool connectionError)
    {
        var result = Parse(hex);

        Assert.AreEqual(Http2FrameReadOutcome.Malformed, result.Outcome);
        Assert.IsNull(result.Frame);
        Assert.AreEqual(malformation, result.Malformation);
        Assert.AreEqual(Http2FrameHeader.Read(Convert.FromHexString(hex)), result.Header);
        Assert.AreEqual(errorCode, Http2FrameMalformations.ErrorCodeOf(malformation));
        Assert.AreEqual(connectionError, Http2FrameMalformations.IsConnectionError(malformation));
    }
}
