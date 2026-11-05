namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortMeasuredRunTests
{
    [TestMethod]
    public void Of_NullArguments_ThrowArgumentNullException()
    {
        var surl = new CurlPortSurlSide(1, string.Empty, new Dictionary<string, byte[]>());
        var build = new PinnedUpstreamCurlBuild("win-x64", "/curl", "AA", "curl 8.21.0", [], UpstreamCurlBuildRole.Reference);

        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortMeasuredRun.Of((UpstreamCurlRunResult)null!, surl));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortMeasuredRun.Of(UpstreamCurlRunResult.Exited(build, 0, [], string.Empty), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortMeasuredRun.Of((CurlPortClientRunResult)null!, surl));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortMeasuredRun.Of(
            CurlPortClientRunResult.Exited(CurlPortClientLocatorTests.Pin("/curl-port/abc123/curl", [0x01]), 0, [], string.Empty), null!));
    }
}
