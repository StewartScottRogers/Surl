namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCasePassingListComparisonTests
{
    [TestMethod]
    public void Compare_ListedCaseFails_IsARegressionNamingTheCaseAndSection()
    {
        var comparison = UpstreamTestCasePassingListComparison.Compare(
            "win-x64", [1, 2], [UpstreamTestCaseResult.Pass(1), UpstreamTestCaseResult.Fail(2, "protocol")]);

        Assert.IsTrue(comparison.HasRegressions);
        Assert.AreEqual("test2 fail protocol", comparison.Regressions.Single().ToString());
        Assert.Contains("test2 fail protocol", comparison.DescribeRegressions());
        Assert.Contains("Documentation/Conformance/upstream-test-cases-passing.win-x64.txt", comparison.DescribeRegressions());
    }

    [TestMethod]
    public void Compare_ListedCaseSkipped_IsARegression()
    {
        var comparison = UpstreamTestCasePassingListComparison.Compare("win-x64", [7], [UpstreamTestCaseResult.Skip(7, "libtest")]);

        Assert.AreEqual("test7 skip libtest", comparison.Regressions.Single().ToString());
    }

    [TestMethod]
    public void Compare_ListedCaseWithNoResult_IsARegression()
    {
        var comparison = UpstreamTestCasePassingListComparison.Compare("win-x64", [9], []);

        Assert.AreEqual("test9 fail no-result", comparison.Regressions.Single().ToString());
    }

    [TestMethod]
    public void Compare_EveryListedCasePasses_HasNoRegressions()
    {
        var comparison = UpstreamTestCasePassingListComparison.Compare("win-x64", [1], [UpstreamTestCaseResult.Pass(1), UpstreamTestCaseResult.Fail(2, "stdout")]);

        Assert.IsFalse(comparison.HasRegressions);
        Assert.IsEmpty(comparison.NewlyPassing);
        Assert.AreEqual(string.Empty, comparison.DescribeNewlyPassing());
    }

    [TestMethod]
    public void Compare_UnlistedCasePasses_IsNewlyPassingWithTheLinesToAdd()
    {
        var comparison = UpstreamTestCasePassingListComparison.Compare(
            "osx-arm64", [1], [UpstreamTestCaseResult.Pass(12), UpstreamTestCaseResult.Pass(1), UpstreamTestCaseResult.Pass(3)]);

        CollectionAssert.AreEqual(new[] { 3, 12 }, comparison.NewlyPassing.ToArray());
        Assert.IsFalse(comparison.HasRegressions);
        var message = comparison.DescribeNewlyPassing();
        Assert.Contains("upstream-test-cases-passing.osx-arm64.txt", message);
        Assert.Contains("Upstream-Test-Case-Pass-Rate.md", message);
        Assert.EndsWith("\n3\n12", message);
    }

    [TestMethod]
    public void Compare_NullArguments_ThrowArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePassingListComparison.Compare(null!, [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePassingListComparison.Compare("win-x64", null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePassingListComparison.Compare("win-x64", [], null!));
    }
}
