namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Which bytes of a blob a <c>Range</c> field selects (RFC 9110 section 14; ADR-0115, decision 5.2).
/// </summary>
[TestClass]
public sealed class OciRangeSelectionTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("items=0-1")]
    [DataRow("bytes=0-1,3-4")]
    [DataRow("bytes=5")]
    [DataRow("bytes=x-1")]
    [DataRow("bytes=-x")]
    [DataRow("bytes=4-2")]
    [DataRow("bytes=1-x")]
    public void Select_NoSingleValidRange_ServesTheWholeBlob(string? field)
    {
        Assert.IsNull(OciRangeSelection.Select(field, 10));
    }

    [TestMethod]
    [DataRow("bytes=2-4", 2L, 4L)]
    [DataRow("bytes=7-", 7L, 9L)]
    [DataRow("bytes=-3", 7L, 9L)]
    [DataRow("bytes=-30", 0L, 9L)]
    public void Select_OneRange_SelectsItsBytes(string field, long first, long last)
    {
        var range = OciRangeSelection.Select(field, 10)!;

        Assert.IsTrue(range.IsSatisfiable);
        Assert.AreEqual(first, range.FirstByte);
        Assert.AreEqual(last, range.LastByte);
    }

    [TestMethod]
    [DataRow("bytes=10-", 10L)]
    [DataRow("bytes=-0", 10L)]
    [DataRow("bytes=-5", 0L)]
    public void Select_RangeNamingNoByte_IsUnsatisfiable(string field, long length)
    {
        Assert.IsFalse(OciRangeSelection.Select(field, length)!.IsSatisfiable);
    }
}
