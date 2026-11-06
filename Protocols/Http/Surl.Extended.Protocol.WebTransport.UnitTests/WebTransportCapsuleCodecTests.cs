using System.Text;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// Round-trips every capsule ADR-0117 uses through <see cref="WebTransportCapsuleWriter"/> and
/// <see cref="WebTransportCapsuleReader"/>. Each expected byte string is written by hand from
/// RFC 9297 section 3.2 (type, length, value, as RFC 9000 section 16 variable-length integers) and
/// the layouts and codepoints of draft-ietf-webtrans-http3-16 sections 4.7, 5.6, 6 and 9.
/// </summary>
[TestClass]
public sealed class WebTransportCapsuleCodecTests
{
    [TestMethod]
    public void Write_CloseSessionWithCodeZeroAndNoMessage_IsTypeLengthAndFourZeroBytes()
    {
        var capsule = new WebTransportCloseSessionCapsule(0, string.Empty);

        AssertRoundTrip(capsule, "6843" + "04" + "00000000");
    }

    [TestMethod]
    public void Write_CloseSessionWithCodeAndMessage_PutsTheCodeBigEndianThenTheUtf8Message()
    {
        var capsule = new WebTransportCloseSessionCapsule(0x01020304, "bye");

        var read = AssertRoundTrip(capsule, "6843" + "07" + "01020304" + "627965");

        var close = (WebTransportCloseSessionCapsule)read;
        Assert.AreEqual(0x01020304u, close.ErrorCode);
        Assert.AreEqual("bye", close.Message);
    }

    [TestMethod]
    public void Write_DrainSession_IsAFourByteTypeAndAZeroLength()
    {
        AssertRoundTrip(new WebTransportDrainSessionCapsule(), "800078ae" + "00");
    }

    [TestMethod]
    [DataRow(WebTransportCapsuleType.MaxData, 1048576UL, "990b4d3d" + "04" + "80100000")]
    [DataRow(WebTransportCapsuleType.MaxStreamsBidirectional, 100UL, "990b4d3f" + "02" + "4064")]
    [DataRow(WebTransportCapsuleType.MaxStreamsUnidirectional, 5UL, "990b4d40" + "01" + "05")]
    [DataRow(WebTransportCapsuleType.DataBlocked, 0UL, "990b4d41" + "01" + "00")]
    [DataRow(WebTransportCapsuleType.StreamsBlockedBidirectional, 63UL, "990b4d43" + "01" + "3f")]
    [DataRow(WebTransportCapsuleType.StreamsBlockedUnidirectional, 64UL, "990b4d44" + "02" + "4040")]
    public void Write_LimitCapsule_IsTypeLengthAndOneVariableLengthInteger(WebTransportCapsuleType type, ulong limit, string expectedHex)
    {
        var read = AssertRoundTrip(new WebTransportLimitCapsule(type, limit), expectedHex);

        var limitCapsule = (WebTransportLimitCapsule)read;
        Assert.AreEqual(type, limitCapsule.Type);
        Assert.AreEqual(limit, limitCapsule.Limit);
    }

    [TestMethod]
    public void Write_Datagram_IsTypeZeroLengthAndThePayload()
    {
        var read = AssertRoundTrip(new WebTransportDatagramCapsule("abc"u8.ToArray()), "00" + "03" + "616263");

        CollectionAssert.AreEqual("abc"u8.ToArray(), ((WebTransportDatagramCapsule)read).Payload.ToArray());
    }

    [TestMethod]
    public void Write_UnparsedCapsule_KeepsItsTypeAndRawValue()
    {
        var capsule = new WebTransportUnparsedCapsule(WebTransportCapsuleType.MaxStreamData, new byte[] { 0x04, 0x10 });

        var read = AssertRoundTrip(capsule, "990b4d3e" + "02" + "0410");

        Assert.AreEqual(WebTransportCapsuleType.MaxStreamData, read.Type);
        CollectionAssert.AreEqual(new byte[] { 0x04, 0x10 }, ((WebTransportUnparsedCapsule)read).Value.ToArray());
    }

    [TestMethod]
    public void Read_UnknownType_IsCompleteAsAnUnparsedCapsule()
    {
        var result = WebTransportCapsuleReader.Read(Convert.FromHexString("17" + "01" + "ff"));

        Assert.AreEqual(WebTransportReadStatus.Complete, result.Status);
        Assert.AreEqual((WebTransportCapsuleType)0x17, result.Capsule!.Type);
        Assert.AreEqual(3, result.BytesRead);
    }

    [TestMethod]
    public void Read_CapsuleFollowedByMore_ReadsOnlyTheFirstCapsule()
    {
        var result = WebTransportCapsuleReader.Read(Convert.FromHexString("800078ae00" + "6843"));

        Assert.AreEqual(WebTransportReadStatus.Complete, result.Status);
        Assert.IsInstanceOfType<WebTransportDrainSessionCapsule>(result.Capsule);
        Assert.AreEqual(5, result.BytesRead);
    }

    [TestMethod]
    public void Read_CloseSessionWith1024ByteMessage_IsAccepted()
    {
        var message = new string('a', 1024);

        var result = WebTransportCapsuleReader.Read(CloseSessionBytes(Encoding.ASCII.GetBytes(message)));

        Assert.AreEqual(WebTransportReadStatus.Complete, result.Status);
        Assert.AreEqual(message, ((WebTransportCloseSessionCapsule)result.Capsule!).Message);
    }

    [TestMethod]
    public void Read_CloseSessionWith1025ByteMessage_IsMalformed()
    {
        var bytes = CloseSessionBytes(Encoding.ASCII.GetBytes(new string('a', 1025)));

        var result = WebTransportCapsuleReader.Read(bytes);

        Assert.AreEqual(WebTransportReadStatus.Malformed, result.Status);
        Assert.IsNull(result.Capsule);
        Assert.AreEqual(bytes.Length, result.BytesRead);
    }

    [TestMethod]
    public void Constructor_CloseSessionWith1024ByteMessage_IsAccepted()
    {
        var capsule = new WebTransportCloseSessionCapsule(7, new string('a', 1024));

        Assert.AreEqual(2 + 2 + 4 + 1024, WebTransportCapsuleWriter.Write(capsule).Length);
    }

    [TestMethod]
    public void Constructor_CloseSessionWith1025ByteMessage_IsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new WebTransportCloseSessionCapsule(7, new string('a', 1025)));
    }

    [TestMethod]
    public void Constructor_CloseSessionWhoseUtf8Exceeds1024Bytes_IsRefused()
    {
        // 513 two-byte characters: 513 characters but 1026 bytes of UTF-8.
        Assert.ThrowsExactly<ArgumentException>(() => new WebTransportCloseSessionCapsule(7, new string('é', 513)));
    }

    [TestMethod]
    public void Constructor_CloseSessionWithNullMessage_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WebTransportCloseSessionCapsule(7, null!));
    }

    [TestMethod]
    [DataRow("6843" + "03" + "000000", DisplayName = "WT_CLOSE_SESSION shorter than its error code")]
    [DataRow("6843" + "05" + "00000000" + "ff", DisplayName = "WT_CLOSE_SESSION message not UTF-8")]
    [DataRow("800078ae" + "01" + "00", DisplayName = "WT_DRAIN_SESSION with a value")]
    [DataRow("990b4d3d" + "00", DisplayName = "WT_MAX_DATA without its integer")]
    [DataRow("990b4d3d" + "02" + "0505", DisplayName = "WT_MAX_DATA with a byte after its integer")]
    [DataRow("990b4d3f" + "01" + "40", DisplayName = "WT_MAX_STREAMS whose integer is cut short")]
    public void Read_ValueBreakingItsLayout_IsMalformedNotIncomplete(string hex)
    {
        var bytes = Convert.FromHexString(hex);

        var result = WebTransportCapsuleReader.Read(bytes);

        Assert.AreEqual(WebTransportReadStatus.Malformed, result.Status);
        Assert.IsNull(result.Capsule);
        Assert.AreEqual(bytes.Length, result.BytesRead);
    }

    [TestMethod]
    [DataRow("", DisplayName = "nothing")]
    [DataRow("68", DisplayName = "the type cut short")]
    [DataRow("6843", DisplayName = "no length")]
    [DataRow("6843" + "07", DisplayName = "the length but no value")]
    [DataRow("6843" + "07" + "010203", DisplayName = "the error code cut short")]
    [DataRow("6843" + "07" + "01020304" + "6279", DisplayName = "the message cut short")]
    [DataRow("6843" + "4007" + "01020304", DisplayName = "a two-byte length, the value cut short")]
    [DataRow("990b4d3d" + "04" + "801000", DisplayName = "WT_MAX_DATA's integer cut short by the stream")]
    public void Read_TruncatedCapsule_IsIncompleteNotMalformed(string hex)
    {
        var result = WebTransportCapsuleReader.Read(Convert.FromHexString(hex));

        Assert.AreEqual(WebTransportReadStatus.Incomplete, result.Status);
        Assert.IsNull(result.Capsule);
        Assert.AreEqual(0, result.BytesRead);
    }

    [TestMethod]
    public void TryReadHeader_LengthBeyondWhatHasArrived_ReturnsTheDeclaredLengthBeforeTheValue()
    {
        var found = WebTransportCapsuleReader.TryReadHeader(Convert.FromHexString("6843" + "80100000"), out var type, out var length, out var headerLength);

        Assert.IsTrue(found);
        Assert.AreEqual(WebTransportCapsuleType.CloseSession, type);
        Assert.AreEqual(1048576UL, length);
        Assert.AreEqual(6, headerLength);
    }

    [TestMethod]
    public void TryReadHeader_HeaderCutShort_ReturnsFalseAndZeroes()
    {
        var found = WebTransportCapsuleReader.TryReadHeader(Convert.FromHexString("6843" + "80"), out var type, out var length, out var headerLength);

        Assert.IsFalse(found);
        Assert.AreEqual((WebTransportCapsuleType)0, type);
        Assert.AreEqual(0UL, length);
        Assert.AreEqual(0, headerLength);
    }

    [TestMethod]
    [DataRow(WebTransportCapsuleType.Datagram, true)]
    [DataRow(WebTransportCapsuleType.CloseSession, true)]
    [DataRow(WebTransportCapsuleType.DrainSession, true)]
    [DataRow(WebTransportCapsuleType.MaxData, true)]
    [DataRow(WebTransportCapsuleType.StreamsBlockedUnidirectional, true)]
    [DataRow(WebTransportCapsuleType.MaxStreamData, false)]
    [DataRow(WebTransportCapsuleType.StreamDataBlocked, false)]
    [DataRow((WebTransportCapsuleType)0x17, false)]
    public void HasLayout_Type_SaysWhetherItHasItsOwnRecord(WebTransportCapsuleType type, bool expected)
    {
        Assert.AreEqual(expected, WebTransportCapsuleReader.HasLayout(type));
    }

    [TestMethod]
    public void Constructor_UnparsedCapsuleOfATypeWithALayout_IsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new WebTransportUnparsedCapsule(WebTransportCapsuleType.CloseSession, Array.Empty<byte>()));
    }

    [TestMethod]
    public void Constructor_LimitCapsuleOfANonLimitType_IsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new WebTransportLimitCapsule(WebTransportCapsuleType.MaxStreamData, 1));
    }

    [TestMethod]
    public void Constructor_LimitAboveTheLargestVariableLengthInteger_IsRefused()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WebTransportLimitCapsule(WebTransportCapsuleType.MaxData, 1UL << 62));
    }

    [TestMethod]
    public void Write_NullCapsule_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WebTransportCapsuleWriter.Write(null!));
    }

    private static WebTransportCapsule AssertRoundTrip(WebTransportCapsule capsule, string expectedHex)
    {
        var written = WebTransportCapsuleWriter.Write(capsule);
        Assert.AreEqual(expectedHex, Convert.ToHexStringLower(written));

        var result = WebTransportCapsuleReader.Read(written);
        Assert.AreEqual(WebTransportReadStatus.Complete, result.Status);
        Assert.AreEqual(written.Length, result.BytesRead);
        Assert.AreEqual(capsule.GetType(), result.Capsule!.GetType());
        Assert.AreEqual(capsule.Type, result.Capsule.Type);
        return result.Capsule;
    }

    private static byte[] CloseSessionBytes(byte[] message)
    {
        // Type 0x2843, a two-byte length (the value is over 63 bytes), code 0, the message.
        var valueLength = 4 + message.Length;
        var bytes = new byte[2 + 2 + valueLength];
        bytes[0] = 0x68;
        bytes[1] = 0x43;
        bytes[2] = (byte)(0x40 | (valueLength >> 8));
        bytes[3] = (byte)valueLength;
        message.CopyTo(bytes, 8);
        return bytes;
    }
}
