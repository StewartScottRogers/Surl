namespace Surl.Qpack;

[TestClass]
public sealed class QpackFieldSectionReaderTests
{
    [TestMethod]
    public void ReconstructRequiredInsertCount_Rfc9204Example_IsNine()
    {
        // RFC 9204 section 4.5.1.1: a 100-byte table, 10 inserts received, encoded value 4.
        var count = QpackFieldSectionReader.ReconstructRequiredInsertCount(4, 100, 10);

        Assert.AreEqual(9, count);
    }

    [TestMethod]
    public void ReconstructRequiredInsertCount_Zero_IsZero()
    {
        var count = QpackFieldSectionReader.ReconstructRequiredInsertCount(0, 0, 0);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    [DataRow(7L, 100, 10L)]
    [DataRow(5L, 100, 0L)]
    [DataRow(1L, 100, 0L)]
    [DataRow(1L, 0, 0L)]
    public void ReconstructRequiredInsertCount_ValueNoConformantEncoderSends_Fails(
        long encodedInsertCount, int maximumTableCapacity, long totalInserts)
    {
        var exception = Assert.ThrowsExactly<QpackDecodingException>(
            () => QpackFieldSectionReader.ReconstructRequiredInsertCount(
                encodedInsertCount, maximumTableCapacity, totalInserts));

        Assert.AreEqual(QpackDecodingError.InvalidRequiredInsertCount, exception.Error);
    }

    [TestMethod]
    public void ReconstructRequiredInsertCount_WithinTheLatestWrap_IsNotUnwrapped()
    {
        // 100-byte table: values wrap every 6; with 10 inserts, encoded 2 is 12 + 2 - 1 = 13.
        var count = QpackFieldSectionReader.ReconstructRequiredInsertCount(2, 100, 10);

        Assert.AreEqual(13, count);
    }
}
