namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseResultTests
{
    [TestMethod]
    public void ToString_Pass_IsTheNumberAndPass()
    {
        Assert.AreEqual("test1 pass", UpstreamTestCaseResult.Pass(1).ToString());
    }

    [TestMethod]
    public void ToString_Fail_NamesTheReason()
    {
        Assert.AreEqual("test3 fail protocol", UpstreamTestCaseResult.Fail(3, "protocol").ToString());
    }

    [TestMethod]
    public void ToString_Skip_NamesTheReason()
    {
        Assert.AreEqual("test7 skip libtest", UpstreamTestCaseResult.Skip(7, "libtest").ToString());
    }

    [TestMethod]
    public void TimeoutReason_IsTimeout()
    {
        Assert.AreEqual("test9 fail timeout", UpstreamTestCaseResult.Fail(9, UpstreamTestCaseResult.TimeoutReason).ToString());
    }

    [TestMethod]
    public void FileNameFor_Platform_IsTheAdrsName()
    {
        Assert.AreEqual("upstream-test-case-results.win-x64.txt", UpstreamTestCaseResult.FileNameFor("win-x64"));
    }

    [TestMethod]
    public void Format_Results_WritesOneLinePerResultInNumberOrder()
    {
        var text = UpstreamTestCaseResult.Format(
            [UpstreamTestCaseResult.Skip(20, "libtest"), UpstreamTestCaseResult.Pass(3), UpstreamTestCaseResult.Fail(100, "stdout")]);

        Assert.AreEqual("test3 pass\ntest20 skip libtest\ntest100 fail stdout\n", text);
    }

    [TestMethod]
    public void Format_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseResult.Format(null!));
    }
}
