namespace Surl.Http3;

/// <summary>
/// A stream's opening: whether its first variable-length integer (RFC 9000 section 16) is the marker that
/// starts a session stream, however the integer's bytes arrive (BL-1775).
/// </summary>
[TestClass]
public sealed class Http3StreamOpeningTests
{
    private const ulong Marker = 0x41;

    private static bool CheckHex(Http3StreamOpening opening, string hex) => opening.TakeBytesAndCheckForSessionStream(Convert.FromHexString(hex));

    [TestMethod]
    [DataRow("4041", DisplayName = "Two bytes")]
    [DataRow("80000041", DisplayName = "Four bytes")]
    [DataRow("C000000000000041", DisplayName = "Eight bytes")]
    [DataRow("404100", DisplayName = "Followed by the session ID")]
    [DataRow("C0000000000000410001030405060708090A0B", DisplayName = "Eight bytes, followed by more than it buffers")]
    public void TakeBytesAndCheckForSessionStream_TheMarkerWholeInTheFirstBytes_IsTrue(string hex)
    {
        var opening = new Http3StreamOpening(Marker);

        Assert.IsTrue(CheckHex(opening, hex));
    }

    [TestMethod]
    [DataRow("4041", DisplayName = "Two bytes")]
    [DataRow("80000041", DisplayName = "Four bytes")]
    [DataRow("C000000000000041", DisplayName = "Eight bytes")]
    public void TakeBytesAndCheckForSessionStream_TheMarkerSplitAnywhereIntoTwo_IsTrueOnceWholeAndStaysTrue(string hex)
    {
        var marker = Convert.FromHexString(hex);

        for (var cut = 1; cut < marker.Length; cut++)
        {
            var opening = new Http3StreamOpening(Marker);

            Assert.IsFalse(opening.TakeBytesAndCheckForSessionStream(marker.AsSpan(0, cut)), $"before the rest, cut at {cut}");
            Assert.IsTrue(opening.TakeBytesAndCheckForSessionStream(marker.AsSpan(cut)), $"whole, cut at {cut}");
            Assert.IsTrue(CheckHex(opening, "00FF"), $"afterwards, cut at {cut}");
        }
    }

    [TestMethod]
    [DataRow("4041", DisplayName = "Two bytes")]
    [DataRow("80000041", DisplayName = "Four bytes")]
    [DataRow("C000000000000041", DisplayName = "Eight bytes")]
    public void TakeBytesAndCheckForSessionStream_TheMarkerOneByteAtATime_IsTrueFromItsLastByte(string hex)
    {
        var marker = Convert.FromHexString(hex);
        var opening = new Http3StreamOpening(Marker);

        for (var index = 0; index < marker.Length; index++)
        {
            Assert.AreEqual(index == marker.Length - 1, opening.TakeBytesAndCheckForSessionStream(marker.AsSpan(index, 1)), $"byte {index}");
        }
    }

    [TestMethod]
    public void TakeBytesAndCheckForSessionStream_AnEightByteMarkerCompletedByALongChunk_IsTrue()
    {
        var opening = new Http3StreamOpening(Marker);
        CheckHex(opening, "C0");

        Assert.IsTrue(CheckHex(opening, "000000000000410001030405060708090A0B"));
    }

    [TestMethod]
    [DataRow("00", DisplayName = "A DATA frame type")]
    [DataRow("0141", DisplayName = "The marker as the second integer")]
    [DataRow("4054", DisplayName = "Another marker")]
    [DataRow("C000000000004041", DisplayName = "A longer integer ending in the marker's bytes")]
    public void TakeBytesAndCheckForSessionStream_ADifferentFirstInteger_StaysFalseWhenTheMarkerArrivesLater(string hex)
    {
        var opening = new Http3StreamOpening(Marker);

        Assert.IsFalse(CheckHex(opening, hex));
        Assert.IsFalse(CheckHex(opening, "4041"));
        Assert.IsFalse(CheckHex(opening, string.Empty));
    }

    [TestMethod]
    public void TakeBytesAndCheckForSessionStream_ADifferentFirstIntegerSplitInTwo_StaysFalseWhenTheMarkerArrivesLater()
    {
        var opening = new Http3StreamOpening(Marker);

        Assert.IsFalse(CheckHex(opening, "40"));
        Assert.IsFalse(CheckHex(opening, "54"));
        Assert.IsFalse(CheckHex(opening, "4041"));
    }

    [TestMethod]
    public void TakeBytesAndCheckForSessionStream_NoBytes_DecidesNothing()
    {
        var opening = new Http3StreamOpening(Marker);

        Assert.IsFalse(CheckHex(opening, string.Empty));
        Assert.IsTrue(CheckHex(opening, "4041"));
        Assert.IsTrue(CheckHex(opening, string.Empty));
    }

    [TestMethod]
    public void TakeBytesAndCheckForSessionStream_AnotherSessionMarker_IsTheOneItLooksFor()
    {
        Assert.IsTrue(CheckHex(new Http3StreamOpening(0x54), "4054"));
        Assert.IsFalse(CheckHex(new Http3StreamOpening(0x54), "4041"));
    }
}
