namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseDebugBuildCasesTests
{
    [TestMethod]
    public void Numbers_AreTheSixHttp2AndHttp3CasesListingDebug()
    {
        CollectionAssert.AreEquivalent(new[] { 358, 359, 2400, 2401, 2406, 2501 }, UpstreamTestCaseDebugBuildCases.Numbers.ToArray());
    }

    [TestMethod]
    [DataRow(2400)]
    [DataRow(358)]
    [DataRow(2501)]
    public void UsesDebugBuild_ListedCaseOnAPlatformThatPinsOne_IsTrue(int number)
    {
        Assert.IsTrue(UpstreamTestCaseDebugBuildCases.UsesDebugBuild(number, debugBuildPinned: true));
    }

    [TestMethod]
    public void UsesDebugBuild_ListedCaseOnAPlatformWithoutOne_IsFalse()
    {
        Assert.IsFalse(UpstreamTestCaseDebugBuildCases.UsesDebugBuild(2400, debugBuildPinned: false));
    }

    [TestMethod]
    public void UsesDebugBuild_CaseOutsideTheSix_IsFalse()
    {
        Assert.IsFalse(UpstreamTestCaseDebugBuildCases.UsesDebugBuild(1700, debugBuildPinned: true));
    }
}
