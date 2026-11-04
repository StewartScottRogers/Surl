namespace Surl.Content;

[TestClass]
public sealed class ContentByteRangeTests
{
    [TestMethod]
    [DataRow(10L, 0L, 9L, 0L, 9L, 10L)]
    [DataRow(10L, 2L, 5L, 2L, 5L, 4L)]
    [DataRow(10L, 9L, 9L, 9L, 9L, 1L)]
    [DataRow(10L, 3L, 3L, 3L, 3L, 1L)]
    public void Select_RangeInsideTheFile_IsKeptAsAsked(long length, long first, long last, long expectedFirst, long expectedLast, long expectedCount)
    {
        ContentByteRange range = ContentByteRange.Select(length, first, last);

        Assert.IsTrue(range.IsSatisfiable);
        Assert.AreEqual(expectedFirst, range.FirstByte);
        Assert.AreEqual(expectedLast, range.LastByte);
        Assert.AreEqual(expectedCount, range.ByteCount);
        Assert.AreEqual(length, range.FileLength);
    }

    [TestMethod]
    [DataRow(10L, 4L, 10L)]
    [DataRow(10L, 4L, long.MaxValue)]
    public void Select_LastOffsetPastTheEnd_IsClampedToLengthMinusOne(long length, long first, long last)
    {
        ContentByteRange range = ContentByteRange.Select(length, first, last);

        Assert.IsTrue(range.IsSatisfiable);
        Assert.AreEqual(4L, range.FirstByte);
        Assert.AreEqual(9L, range.LastByte);
        Assert.AreEqual(6L, range.ByteCount);
    }

    [TestMethod]
    [DataRow(10L, 10L, 20L)]
    [DataRow(10L, 11L, 11L)]
    [DataRow(0L, 0L, 0L)]
    public void Select_FirstOffsetAtOrPastTheEnd_IsUnsatisfiableAndCarriesTheLength(long length, long first, long last)
    {
        ContentByteRange range = ContentByteRange.Select(length, first, last);

        Assert.IsFalse(range.IsSatisfiable);
        Assert.AreEqual(length, range.FileLength);
        Assert.AreEqual(0L, range.ByteCount);
    }

    [TestMethod]
    public void Select_FirstOffsetGreaterThanTheLast_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ContentByteRange.Select(10, 5, 4));
    }

    [TestMethod]
    [DataRow(-1L, 0L, 0L)]
    [DataRow(10L, -1L, 3L)]
    public void Select_NegativeLengthOrOffset_IsRejected(long length, long first, long last)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ContentByteRange.Select(length, first, last));
    }

    [TestMethod]
    public void WholeFile_CoversEveryByte()
    {
        ContentByteRange range = ContentByteRange.WholeFile(10);

        Assert.IsTrue(range.IsSatisfiable);
        Assert.AreEqual(0L, range.FirstByte);
        Assert.AreEqual(9L, range.LastByte);
        Assert.AreEqual(10L, range.ByteCount);
        Assert.AreEqual(10L, range.FileLength);
    }

    [TestMethod]
    public void WholeFile_ZeroLengthFile_IsSatisfiableWithNoBytes()
    {
        ContentByteRange range = ContentByteRange.WholeFile(0);

        Assert.IsTrue(range.IsSatisfiable);
        Assert.AreEqual(0L, range.ByteCount);
    }

    [TestMethod]
    public void WholeFile_NegativeLength_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ContentByteRange.WholeFile(-1));
    }
}
