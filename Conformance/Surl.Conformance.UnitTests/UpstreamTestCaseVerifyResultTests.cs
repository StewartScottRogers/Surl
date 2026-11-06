namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseVerifyResultTests
{
    [TestMethod]
    public void Pass_HasNoSection_AndSaysPass()
    {
        var result = UpstreamTestCaseVerifyResult.Pass;

        Assert.IsTrue(result.Passed);
        Assert.IsNull(result.FailedSection);
        Assert.IsNull(result.Difference);
        Assert.AreEqual("pass", result.ToString());
    }

    [TestMethod]
    public void Fail_NamesTheSectionAndDifference()
    {
        var result = UpstreamTestCaseVerifyResult.Fail("stdout", "line 1 differs");

        Assert.IsFalse(result.Passed);
        Assert.AreEqual("stdout", result.FailedSection);
        Assert.AreEqual("line 1 differs", result.Difference);
        Assert.AreEqual("fail stdout: line 1 differs", result.ToString());
    }

    [TestMethod]
    public void Fail_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseVerifyResult.Fail(null!, "x"));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseVerifyResult.Fail("x", null!));
    }
}
