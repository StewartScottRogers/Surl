namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCasePassingListTests
{
    [TestMethod]
    public void FileNameFor_Platform_IsTheAdrsName()
    {
        Assert.AreEqual("upstream-test-cases-passing.linux-x64.txt", UpstreamTestCasePassingList.FileNameFor("linux-x64"));
    }

    [TestMethod]
    public void Parse_AscendingNumbers_ReturnsThem()
    {
        var numbers = UpstreamTestCasePassingList.Parse("1\r\n3\n\n200\n");

        CollectionAssert.AreEqual(new[] { 1, 3, 200 }, numbers.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyText_ReturnsNoNumbers()
    {
        Assert.IsEmpty(UpstreamTestCasePassingList.Parse(string.Empty));
    }

    [TestMethod]
    public void Parse_LineNotANumber_ThrowsNamingTheLine()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamTestCasePassingList.Parse("1\ntest2\n"));

        Assert.Contains("Line 2", exception.Message);
    }

    [TestMethod]
    public void Parse_NumberOutOfOrder_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => UpstreamTestCasePassingList.Parse("5\n4\n"));
    }

    [TestMethod]
    public void Parse_RepeatedNumber_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => UpstreamTestCasePassingList.Parse("5\n5\n"));
    }

    [TestMethod]
    public void Parse_NullText_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePassingList.Parse(null!));
    }

    [TestMethod]
    public void Format_Results_WritesThePassingNumbersAscending()
    {
        var text = UpstreamTestCasePassingList.Format(
            [UpstreamTestCaseResult.Pass(30), UpstreamTestCaseResult.Fail(2, "stdout"), UpstreamTestCaseResult.Skip(3, "libtest"), UpstreamTestCaseResult.Pass(4)]);

        Assert.AreEqual("4\n30\n", text);
    }

    [TestMethod]
    public void Format_NullResults_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePassingList.Format(null!));
    }
}
