namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortVerdictTests
{
    [TestMethod]
    public void ToString_NamesEachVerdict()
    {
        Assert.AreEqual("agreement", CurlPortVerdict.Agreement().ToString());
        Assert.AreEqual("Surl defect: upstream exited 7", CurlPortVerdict.SurlDefect("upstream exited 7").ToString());
        Assert.AreEqual("not comparable: release difference: x", CurlPortVerdict.NotComparable("release difference: x").ToString());
    }

    [TestMethod]
    public void PortDefect_KeepsItsFindingAndShowsIt()
    {
        var run = new CurlPortMeasuredRun(0, [], string.Empty, new CurlPortSurlSide(1, string.Empty, new Dictionary<string, byte[]>()));
        var finding = new CurlPortFinding(
            "Test",
            "Mirrored.Test",
            "curl {surl}/",
            new PinnedUpstreamCurlBuild("win-x64", "/curl", "AA", "curl 8.21.0", [], UpstreamCurlBuildRole.Reference),
            CurlPortClientLocatorTests.Pin("/curl-port/abc123/curl", [0x01]),
            "commit",
            run,
            run,
            [new CurlPortDifference("exit code", "0", "1")]);

        var verdict = CurlPortVerdict.PortDefect(finding);

        Assert.AreEqual(CurlPortVerdictKind.PortDefect, verdict.Kind);
        Assert.AreSame(finding, verdict.Finding);
        Assert.AreEqual(string.Empty, verdict.Reason);
        Assert.AreEqual($"port defect\n{finding}", verdict.ToString());
    }

    [TestMethod]
    public void Factories_RefuseAMissingFindingOrReason()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortVerdict.PortDefect(null!));
        Assert.ThrowsExactly<ArgumentException>(() => CurlPortVerdict.SurlDefect(" "));
        Assert.ThrowsExactly<ArgumentException>(() => CurlPortVerdict.NotComparable(string.Empty));
    }
}
