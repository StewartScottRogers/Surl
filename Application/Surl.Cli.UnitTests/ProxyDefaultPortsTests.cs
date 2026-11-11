namespace Surl.Cli;

[TestClass]
public sealed class ProxyDefaultPortsTests
{
    [TestMethod]
    public void Schemes_AreHttpAndHttps()
    {
        var schemes = ProxyDefaultPorts.Schemes.Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(new[] { "http", "https" }, schemes);
    }

    [TestMethod]
    [DataRow("http", 1080)]
    [DataRow("https", 443)]
    public void TryGetDefaultPort_ProxyScheme_ReturnsCurlsDefaultProxyPort(string scheme, int expected)
    {
        var found = ProxyDefaultPorts.TryGetDefaultPort(scheme, out var port);

        Assert.IsTrue(found);
        Assert.AreEqual(expected, port);
    }

    [TestMethod]
    public void TryGetDefaultPort_OtherScheme_ReturnsFalse()
    {
        var found = ProxyDefaultPorts.TryGetDefaultPort("ftp", out var port);

        Assert.IsFalse(found);
        Assert.AreEqual(0, port);
    }
}
