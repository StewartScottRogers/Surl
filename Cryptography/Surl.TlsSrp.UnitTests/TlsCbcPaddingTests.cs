namespace Surl.TlsSrp;

/// <summary>RFC 5246 section 6.2.3.2's CBC padding.</summary>
[TestClass]
public sealed class TlsCbcPaddingTests
{
    [TestMethod]
    [DataRow(0, 16, 16)]
    [DataRow(15, 16, 16)]
    [DataRow(16, 16, 32)]
    [DataRow(23, 8, 24)]
    [DataRow(24, 8, 32)]
    public void Pad_Content_FillsToTheNextWholeBlockWithTheLengthByte(int contentLength, int blockLength, int paddedLength)
    {
        var padded = TlsCbcPadding.Pad(new byte[contentLength], blockLength);

        Assert.HasCount(paddedLength, padded);
        var paddingLength = paddedLength - contentLength - 1;
        Assert.IsTrue(padded.Skip(contentLength).All(value => value == paddingLength));
    }

    [TestMethod]
    public void MeasureContent_CorrectPaddingAfterTrailer_IsValidAndEndsTheContent()
    {
        byte[] padded = [9, 9, 9, 0xEE, 0xEE, 2, 2, 2];

        var contentLength = TlsCbcPadding.MeasureContent(padded, 2, out var paddingIsValid);

        Assert.IsTrue(paddingIsValid);
        Assert.AreEqual(3, contentLength);
    }

    [TestMethod]
    public void MeasureContent_WrongPaddingByte_IsInvalid()
    {
        byte[] padded = [9, 9, 9, 1, 2, 2];

        TlsCbcPadding.MeasureContent(padded, 0, out var paddingIsValid);

        Assert.IsFalse(paddingIsValid);
    }

    [TestMethod]
    public void MeasureContent_PaddingLongerThanRoomLeft_IsInvalidAndTreatedAsOneByte()
    {
        byte[] padded = [9, 9, 9, 0xEE, 0xEE, 6, 6, 6];

        var contentLength = TlsCbcPadding.MeasureContent(padded, 2, out var paddingIsValid);

        Assert.IsFalse(paddingIsValid);
        Assert.AreEqual(5, contentLength);
    }

    [TestMethod]
    public void MeasureContent_MaximumPaddingOf256Bytes_IsValid()
    {
        var padded = Enumerable.Repeat((byte)255, 260).ToArray();

        var contentLength = TlsCbcPadding.MeasureContent(padded, 0, out var paddingIsValid);

        Assert.IsTrue(paddingIsValid);
        Assert.AreEqual(4, contentLength);
    }
}
