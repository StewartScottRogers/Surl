namespace Surl.Conformance;

[TestClass]
public sealed class UnpinnedCurlPortClientExceptionTests
{
    [TestMethod]
    public void Constructor_NamesThePathTheHashAndThePinFile()
    {
        var exception = new UnpinnedCurlPortClientException("/curl-port/curl", "ABCD");

        Assert.AreEqual("/curl-port/curl", exception.Path);
        Assert.AreEqual("ABCD", exception.Sha256);
        StringAssert.StartsWith(exception.Message, "/curl-port/curl (SHA-256 ABCD) is not a pinned Curl port client.");
        StringAssert.Contains(exception.Message, "CurlPortBuilds.json");
    }

    [TestMethod]
    public void Type_IsNotTheUpstreamRefusal()
    {
        Assert.IsNotInstanceOfType<UnpinnedUpstreamCurlException>(new UnpinnedCurlPortClientException("p", "h"));
    }
}
