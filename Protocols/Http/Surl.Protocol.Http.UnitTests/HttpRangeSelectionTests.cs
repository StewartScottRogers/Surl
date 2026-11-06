namespace Surl.Protocol.Http;

/// <summary>
/// <see cref="HttpRangeSelection"/> turns a <c>Range</c> field into the bytes of a 17-byte file
/// it selects (RFC 9110, section 14.1.2; ADR-0133, decision 1).
/// </summary>
[TestClass]
public sealed class HttpRangeSelectionTests
{
    [TestMethod]
    [DataRow("bytes=0-9", 0L, 9L, DisplayName = "First and last")]
    [DataRow("BYTES= 0-9 ", 0L, 9L, DisplayName = "Unit in any case, spaces around")]
    [DataRow("bytes=3-", 3L, 16L, DisplayName = "Open-ended")]
    [DataRow("bytes=10-100", 10L, 16L, DisplayName = "Last past the end, clamped")]
    [DataRow("bytes=-5", 12L, 16L, DisplayName = "Suffix")]
    [DataRow("bytes=-100", 0L, 16L, DisplayName = "Suffix longer than the file")]
    public void Select_SatisfiableRange_SelectsItsBytes(string field, long first, long last)
    {
        var range = HttpRangeSelection.Select(field, 17);

        Assert.IsNotNull(range);
        Assert.IsTrue(range.IsSatisfiable);
        Assert.AreEqual(first, range.FirstByte);
        Assert.AreEqual(last, range.LastByte);
    }

    [TestMethod]
    [DataRow("bytes=17-", 17L, DisplayName = "First at the end")]
    [DataRow("bytes=100-200", 17L, DisplayName = "First past the end")]
    [DataRow("bytes=-0", 17L, DisplayName = "Zero suffix")]
    [DataRow("bytes=-5", 0L, DisplayName = "Suffix of an empty file")]
    [DataRow("bytes=0-", 0L, DisplayName = "Any range of an empty file")]
    public void Select_RangeNamingNoByte_IsUnsatisfiable(string field, long fileLength)
    {
        var range = HttpRangeSelection.Select(field, fileLength);

        Assert.IsNotNull(range);
        Assert.IsFalse(range.IsSatisfiable);
        Assert.AreEqual(fileLength, range.FileLength);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "No field")]
    [DataRow("items=0-9", DisplayName = "Another unit")]
    [DataRow("bytes=0-1,3-4", DisplayName = "Several ranges")]
    [DataRow("bytes=5", DisplayName = "No dash")]
    [DataRow("bytes=-x", DisplayName = "Suffix not a number")]
    [DataRow("bytes=x-9", DisplayName = "First not a number")]
    [DataRow("bytes=0-x", DisplayName = "Last not a number")]
    [DataRow("bytes=+1-9", DisplayName = "A sign")]
    [DataRow("bytes=9-3", DisplayName = "Last before first")]
    public void Select_FieldTheServerIgnores_IsNull(string? field)
    {
        Assert.IsNull(HttpRangeSelection.Select(field, 17));
    }
}
