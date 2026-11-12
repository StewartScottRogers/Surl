

namespace Surl.Protocol.Http;

/// <summary>
/// <see cref="HttpRangeSelection"/> turns a <c>Range</c> field into the bytes of a 17-byte file
/// it selects (RFC 9110, section 14.1.2; ADR-0133, decision 1 and Amendment 1).
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
    [DataRow("bytes=0-4,2-6", 0L, 6L, DisplayName = "Overlapping ranges coalesce to one")]
    [DataRow("bytes=0-4,5-9", 0L, 9L, DisplayName = "Touching ranges coalesce to one")]
    [DataRow("bytes=0-1,,0-3", 0L, 3L, DisplayName = "An empty list element is skipped")]
    [DataRow("bytes=0-1,100-200", 0L, 1L, DisplayName = "A range past the end is dropped")]
    public void Select_RangesCoalescingToOne_SelectsItsBytes(string field, long first, long last)
    {
        var ranges = HttpRangeSelection.Select(field, 17);

        Assert.IsNotNull(ranges);
        var range = ranges.Single();
        Assert.IsTrue(range.IsSatisfiable);
        Assert.AreEqual(first, range.FirstByte);
        Assert.AreEqual(last, range.LastByte);
    }

    [TestMethod]
    public void Select_SeveralSeparateRanges_KeepsTheOrderSent()
    {
        var ranges = HttpRangeSelection.Select("bytes=10-11,0-1,5-6", 17);

        Assert.IsNotNull(ranges);
        CollectionAssert.AreEqual(new long[] { 10, 0, 5 }, ranges.Select(range => range.FirstByte).ToArray());
    }

    [TestMethod]
    public void Select_SeveralRangesWhereSomeCoalesce_SortsThem()
    {
        var ranges = HttpRangeSelection.Select("bytes=10-11,0-1,1-3", 17);

        Assert.IsNotNull(ranges);
        CollectionAssert.AreEqual(new long[] { 0, 10 }, ranges.Select(range => range.FirstByte).ToArray());
        CollectionAssert.AreEqual(new long[] { 3, 11 }, ranges.Select(range => range.LastByte).ToArray());
    }

    [TestMethod]
    public void Select_ExactlyTheMostRanges_SelectsThemAll()
    {
        var field = "bytes=" + string.Join(',', Enumerable.Range(0, HttpRangeSelection.MaxRanges).Select(index => $"{index * 2}-{index * 2}"));

        var ranges = HttpRangeSelection.Select(field, 1000);

        Assert.IsNotNull(ranges);
        Assert.HasCount(HttpRangeSelection.MaxRanges, ranges);
    }

    [TestMethod]
    public void Select_MoreThanTheMostRanges_IsNull()
    {
        var field = "bytes=" + string.Join(',', Enumerable.Repeat("0-0", HttpRangeSelection.MaxRanges + 1));

        Assert.IsNull(HttpRangeSelection.Select(field, 17));
    }

    [TestMethod]
    [DataRow("bytes=17-", 17L, DisplayName = "First at the end")]
    [DataRow("bytes=100-200", 17L, DisplayName = "First past the end")]
    [DataRow("bytes=-0", 17L, DisplayName = "Zero suffix")]
    [DataRow("bytes=-5", 0L, DisplayName = "Suffix of an empty file")]
    [DataRow("bytes=0-", 0L, DisplayName = "Any range of an empty file")]
    [DataRow("bytes=20-30,40-50", 17L, DisplayName = "Several ranges all past the end")]
    public void Select_RangesNamingNoByte_IsEmpty(string field, long fileLength)
    {
        var ranges = HttpRangeSelection.Select(field, fileLength);

        Assert.IsNotNull(ranges);
        Assert.IsEmpty(ranges);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "No field")]
    [DataRow("items=0-9", DisplayName = "Another unit")]
    [DataRow("bytes=", DisplayName = "No range")]
    [DataRow("bytes=,", DisplayName = "Only empty elements")]
    [DataRow("bytes=0-1,5", DisplayName = "One of several has no dash")]
    [DataRow("bytes=5", DisplayName = "No dash")]
    [DataRow("bytes=-x", DisplayName = "Suffix not a number")]
    [DataRow("bytes=x-9", DisplayName = "First not a number")]
    [DataRow("bytes=0-x", DisplayName = "Last not a number")]
    [DataRow("bytes=+1-9", DisplayName = "A sign")]
    [DataRow("bytes=9-3", DisplayName = "Last before first")]
    [DataRow("bytes=0-1,9-3", DisplayName = "One of several with last before first")]
    public void Select_FieldTheServerIgnores_IsNull(string? field)
    {
        Assert.IsNull(HttpRangeSelection.Select(field, 17));
    }
}
