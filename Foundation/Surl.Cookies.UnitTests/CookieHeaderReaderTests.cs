namespace Surl.Cookies;

[TestClass]
public sealed class CookieHeaderReaderTests
{
    private static string Flatten(string? header) => string.Join("|", CookieHeaderReader.Read(header).Select(p => p.Name + "=" + p.Value));

    [TestMethod]
    public void Read_CurlsMeasuredHeader_GivesPairsInOrder()
    {
        Assert.AreEqual("sess=xyz|sid=abc123", Flatten("sess=xyz; sid=abc123"));
    }

    [TestMethod]
    public void Read_EmptyValue_IsAPairWithAnEmptyValue()
    {
        Assert.AreEqual("a=|b=1", Flatten("a=; b=1"));
    }

    [TestMethod]
    public void Read_StrayWhitespace_IsDropped()
    {
        Assert.AreEqual("a=1|b=2", Flatten("  a = 1 ;b=2  ;"));
    }

    [TestMethod]
    public void Read_ValueHoldingEquals_SplitsAtTheFirstOne()
    {
        Assert.AreEqual("a=x=y", Flatten("a=x=y"));
    }

    [TestMethod]
    public void Read_SegmentWithoutEqualsOrName_IsSkipped()
    {
        Assert.AreEqual("b=2", Flatten("junk; =v; b=2;;"));
    }

    [TestMethod]
    public void Read_NullOrEmpty_GivesNoPairs()
    {
        Assert.AreEqual(0, CookieHeaderReader.Read(null).Count);
        Assert.AreEqual(0, CookieHeaderReader.Read(string.Empty).Count);
    }
}
