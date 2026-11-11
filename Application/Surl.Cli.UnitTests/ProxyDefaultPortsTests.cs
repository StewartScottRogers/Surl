namespace Surl.Cli;

[TestClass]
public sealed class ProxyDefaultPortsTests
{
    [TestMethod]
    public void Schemes_AreHttpHttpsAndTheFourSocksSchemes()
    {
        var schemes = ProxyDefaultPorts.Schemes.Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(new[] { "http", "https", "socks4", "socks4a", "socks5", "socks5h" }, schemes);
    }

    [TestMethod]
    [DataRow("http", 1080)]
    [DataRow("https", 443)]
    [DataRow("socks4", 1080)]
    [DataRow("socks4a", 1080)]
    [DataRow("socks5", 1080)]
    [DataRow("socks5h", 1080)]
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
