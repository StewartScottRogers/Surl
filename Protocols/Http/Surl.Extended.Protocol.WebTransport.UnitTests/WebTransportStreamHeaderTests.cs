namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The stream headers of draft-ietf-webtrans-http3-16 sections 4.2 and 4.3: <c>0x41</c> or
/// <c>0x54</c> (each a two-byte variable-length integer, <c>40 41</c> and <c>40 54</c>), then the
/// session ID in any of RFC 9000 section 16's four lengths. Bytes written by hand from those layouts.
/// </summary>
[TestClass]
public sealed class WebTransportStreamHeaderTests
{
    [TestMethod]
    [DataRow("00", 0UL, DisplayName = "1-byte session ID")]
    [DataRow("4100", 0x100UL, DisplayName = "2-byte session ID")]
    [DataRow("80040000", 0x40000UL, DisplayName = "4-byte session ID")]
    [DataRow("c000000040000000", 0x40000000UL, DisplayName = "8-byte session ID")]
    [DataRow("4004", 4UL, DisplayName = "2-byte encoding of a 1-byte value")]
    public void Read_BidirectionalHeader_ReadsTheSessionIdOfEachLength(string sessionIdHex, ulong sessionId)
    {
        var bytes = Convert.FromHexString("4041" + sessionIdHex + "68656c6c6f");

        var result = WebTransportStreamHeader.Read(bytes);

        Assert.AreEqual(WebTransportReadStatus.Complete, result.Status);
        Assert.AreEqual(new WebTransportStreamHeader(WebTransportStreamKind.Bidirectional, sessionId), result.Header);
        Assert.AreEqual(2 + (sessionIdHex.Length / 2), result.BytesRead);
    }

    [TestMethod]
    [DataRow("00", 0UL, DisplayName = "1-byte session ID")]
    [DataRow("4100", 0x100UL, DisplayName = "2-byte session ID")]
    [DataRow("80040000", 0x40000UL, DisplayName = "4-byte session ID")]
    [DataRow("c000000040000000", 0x40000000UL, DisplayName = "8-byte session ID")]
    public void Read_UnidirectionalHeader_ReadsTheSessionIdOfEachLength(string sessionIdHex, ulong sessionId)
    {
        var bytes = Convert.FromHexString("4054" + sessionIdHex);

        var result = WebTransportStreamHeader.Read(bytes);

        Assert.AreEqual(WebTransportReadStatus.Complete, result.Status);
        Assert.AreEqual(new WebTransportStreamHeader(WebTransportStreamKind.Unidirectional, sessionId), result.Header);
        Assert.AreEqual(bytes.Length, result.BytesRead);
    }

    [TestMethod]
    [DataRow(WebTransportStreamKind.Bidirectional, 0UL, "404100")]
    [DataRow(WebTransportStreamKind.Unidirectional, 4UL, "405404")]
    [DataRow(WebTransportStreamKind.Unidirectional, 0x100UL, "40544100")]
    [DataRow(WebTransportStreamKind.Bidirectional, 0x40000UL, "404180040000")]
    [DataRow(WebTransportStreamKind.Bidirectional, 0x40000000UL, "4041c000000040000000")]
    public void Encode_Header_WritesKindThenSessionIdInShortestEncoding(WebTransportStreamKind kind, ulong sessionId, string expectedHex)
    {
        var header = new WebTransportStreamHeader(kind, sessionId);

        Assert.AreEqual(expectedHex, Convert.ToHexStringLower(header.Encode()));
    }

    [TestMethod]
    [DataRow("", DisplayName = "nothing")]
    [DataRow("40", DisplayName = "the signal cut short")]
    [DataRow("4041", DisplayName = "no session ID")]
    [DataRow("4054c0000000", DisplayName = "an 8-byte session ID cut short")]
    public void Read_TruncatedHeader_IsIncomplete(string hex)
    {
        var result = WebTransportStreamHeader.Read(Convert.FromHexString(hex));

        Assert.AreEqual(WebTransportReadStatus.Incomplete, result.Status);
        Assert.IsNull(result.Header);
        Assert.AreEqual(0, result.BytesRead);
    }

    [TestMethod]
    [DataRow("4000", 2, DisplayName = "an HTTP/3 DATA frame type")]
    [DataRow("01", 1, DisplayName = "the 1-byte value 0x01")]
    [DataRow("41", 2, DisplayName = "a bare 0x41 byte starts a 2-byte integer, 0x100")]
    [DataRow("4055", 2, DisplayName = "0x55")]
    public void Read_StreamStartingWithNeitherSignal_IsMalformed(string hex, int expectedBytesRead)
    {
        var result = WebTransportStreamHeader.Read(Convert.FromHexString(hex + "00"));

        Assert.AreEqual(WebTransportReadStatus.Malformed, result.Status);
        Assert.IsNull(result.Header);
        Assert.AreEqual(expectedBytesRead, result.BytesRead);
    }

    [TestMethod]
    public void Encode_SessionIdAboveTheLargestVariableLengthInteger_Throws()
    {
        var header = new WebTransportStreamHeader(WebTransportStreamKind.Bidirectional, 1UL << 62);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => header.Encode());
    }
}
