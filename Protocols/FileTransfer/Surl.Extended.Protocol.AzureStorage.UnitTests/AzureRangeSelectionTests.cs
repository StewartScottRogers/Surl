namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The <c>Range</c> forms "Specifying the range header for Blob service operations" serves, ignores and refuses.
/// </summary>
[TestClass]
public sealed class AzureRangeSelectionTests
{
    [TestMethod]
    [DataRow("bytes=0-4", 0L, 4L, DisplayName = "first and last")]
    [DataRow("BYTES=0-4", 0L, 4L, DisplayName = "the unit in any case")]
    [DataRow("bytes=6-", 6L, 10L, DisplayName = "from an offset")]
    [DataRow("bytes=6-99", 6L, 10L, DisplayName = "a last past the end")]
    [DataRow("bytes=-5", 6L, 10L, DisplayName = "a suffix")]
    [DataRow("bytes=-99", 0L, 10L, DisplayName = "a suffix past the start")]
    public void Select_OneSatisfiableRange_SelectsItsBytes(string field, long first, long last)
    {
        var range = AzureRangeSelection.Select(field, 11);

        Assert.IsNotNull(range);
        Assert.IsTrue(range.IsSatisfiable);
        Assert.AreEqual(first, range.FirstByte);
        Assert.AreEqual(last, range.LastByte);
    }

    [TestMethod]
    [DataRow("bytes=11-", 11L, DisplayName = "a first at the end")]
    [DataRow("bytes=-0", 11L, DisplayName = "a zero suffix")]
    [DataRow("bytes=-5", 0L, DisplayName = "a suffix of an empty blob")]
    [DataRow("bytes=0-", 0L, DisplayName = "any range of an empty blob")]
    public void Select_RangeNamingNoByte_IsUnsatisfiable(string field, long blobLength)
    {
        var range = AzureRangeSelection.Select(field, blobLength);

        Assert.IsNotNull(range);
        Assert.IsFalse(range.IsSatisfiable);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no field")]
    [DataRow("items=0-4", DisplayName = "another unit")]
    [DataRow("bytes=0-1,4-5", DisplayName = "several ranges")]
    [DataRow("bytes=5", DisplayName = "no dash")]
    [DataRow("bytes=a-4", DisplayName = "a first that is no number")]
    [DataRow("bytes=0-b", DisplayName = "a last that is no number")]
    [DataRow("bytes=-x", DisplayName = "a suffix that is no number")]
    [DataRow("bytes=5-4", DisplayName = "a last before the first")]
    public void Select_FormThePageDoesNotServe_ServesTheWholeObject(string? field)
    {
        Assert.IsNull(AzureRangeSelection.Select(field, 11));
    }
}
