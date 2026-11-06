namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Reading a resumable upload's <c>Content-Range</c> ("Perform resumable uploads").
/// </summary>
[TestClass]
public sealed class GcsContentRangeTests
{
    [TestMethod]
    [DataRow("bytes 0-8388607/20000000", 0L, 8388607L, 20000000L, DisplayName = "the page's example")]
    [DataRow(" Bytes 5-9/* ", 5L, 9L, null, DisplayName = "an unknown total")]
    [DataRow("bytes */11", null, null, 11L, DisplayName = "a status check")]
    [DataRow("bytes */*", null, null, null, DisplayName = "a status check, total unknown")]
    public void Of_AFieldThatParses_GivesItsBytesAndTotal(string field, long? first, long? last, long? total)
    {
        var range = GcsContentRange.Of(field, 0);

        Assert.AreEqual(new GcsContentRange(first, last, total), range);
    }

    [TestMethod]
    [DataRow("pages 0-4/5", DisplayName = "another unit")]
    [DataRow("bytes 0-4", DisplayName = "no total")]
    [DataRow("bytes 0-4/x", DisplayName = "a total that is not a number")]
    [DataRow("bytes 04/5", DisplayName = "no dash")]
    [DataRow("bytes a-4/5", DisplayName = "a first byte that is not a number")]
    [DataRow("bytes 0-b/5", DisplayName = "a last byte that is not a number")]
    [DataRow("bytes 4-0/5", DisplayName = "the last byte before the first")]
    [DataRow("bytes 0-5/5", DisplayName = "a byte past the total")]
    public void Of_AFieldThatDoesNotParse_IsNull(string field)
    {
        Assert.IsNull(GcsContentRange.Of(field, 0));
    }

    [TestMethod]
    public void Of_NoField_IsTheWholeAnnouncedBody()
    {
        Assert.AreEqual(new GcsContentRange(0, 9, 10), GcsContentRange.Of(null, 10));
        Assert.AreEqual(new GcsContentRange(null, null, 0), GcsContentRange.Of(null, 0));
    }

    [TestMethod]
    public void Count_IsTheChunksBytesOrNone()
    {
        Assert.AreEqual(5, new GcsContentRange(5, 9, null).Count);
        Assert.AreEqual(0, new GcsContentRange(null, null, 11).Count);
    }
}
