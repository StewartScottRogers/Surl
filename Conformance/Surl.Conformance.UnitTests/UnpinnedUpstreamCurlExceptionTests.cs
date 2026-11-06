namespace Surl.Conformance;

[TestClass]
public sealed class UnpinnedUpstreamCurlExceptionTests
{
    [TestMethod]
    public void Constructor_PathAndHash_GivesAssertPinnedUpstreamCurlsMessage()
    {
        var exception = new UnpinnedUpstreamCurlException("/port/curl", "ABCDEF");

        Assert.AreEqual("/port/curl", exception.Path);
        Assert.AreEqual("ABCDEF", exception.Sha256);
        Assert.AreEqual(
            "/port/curl (SHA-256 ABCDEF) is not a pinned upstream curl build. Surl is measured only against the builds in UpstreamCurlBuilds.json (ADR-0003); pinning another is a decision, and the Curl port is never one.",
            exception.Message);
        Assert.AreEqual(UpstreamCurlBuildKind.Curl, exception.Kind);
    }

    [TestMethod]
    public void Constructor_LibraryKind_NamesItAsLibcurl()
    {
        var exception = new UnpinnedUpstreamCurlException("/port/libcurl.dll", "ABCDEF", UpstreamCurlBuildKind.Library);

        Assert.AreEqual(UpstreamCurlBuildKind.Library, exception.Kind);
        Assert.StartsWith("/port/libcurl.dll (SHA-256 ABCDEF) is not a pinned upstream libcurl. Surl is measured", exception.Message);
    }
}
