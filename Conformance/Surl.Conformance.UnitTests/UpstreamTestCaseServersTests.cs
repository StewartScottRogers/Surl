namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseServersTests
{
    [TestMethod]
    public void Find_NullName_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseServers.Find(null!));
    }

    [TestMethod]
    [DataRow("http-ipv6", "http", true)]
    [DataRow("https-mtls", "https", true)]
    [DataRow("smb", "smb", true)]
    [DataRow("http/2", "http", false)]
    [DataRow("dns", "http", false)]
    public void Find_KnownName_GivesItsRow(string name, string protocol, bool isBuilt)
    {
        var server = UpstreamTestCaseServers.Find(name);

        Assert.AreEqual(new UpstreamTestCaseServer(name, protocol, isBuilt), server);
    }

    [TestMethod]
    public void Find_HttpUnix_IsBuiltAndMapsHttpUnixPath()
    {
        Assert.AreEqual(
            new UpstreamTestCaseServer("http-unix", "http", IsBuilt: true, UnixSocketPathVariable: "HTTPUNIXPATH"),
            UpstreamTestCaseServers.Find("http-unix"));
    }

    [TestMethod]
    public void Find_HttpProxy_IsBuiltAndMapsProxyPort()
    {
        Assert.AreEqual(
            new UpstreamTestCaseServer("http-proxy", "http", IsBuilt: true, PortVariable: "PROXYPORT"),
            UpstreamTestCaseServers.Find("http-proxy"));
    }

    [TestMethod]
    public void Find_HttpsProxy_IsBuiltAndMapsHttpsProxyPort()
    {
        Assert.AreEqual(
            new UpstreamTestCaseServer("https-proxy", "https", IsBuilt: true, PortVariable: "HTTPSPROXYPORT"),
            UpstreamTestCaseServers.Find("https-proxy"));
    }

    [TestMethod]
    public void All_OnlyTheProxySocksAndTlsSrpServers_MapAPortVariable()
    {
        var mapped = UpstreamTestCaseServers.All
            .Where(server => server.PortVariable is not null)
            .Select(server => $"{server.Name} %{server.PortVariable}");

        CollectionAssert.AreEqual(new[] { "http-proxy %PROXYPORT", "https-proxy %HTTPSPROXYPORT", "socks4 %SOCKSPORT", "socks5 %SOCKSPORT", "httptls+srp %HTTPTLSPORT", "httptls+srp-ipv6 %HTTPTLS6PORT" }, mapped.ToArray());
        Assert.IsTrue(UpstreamTestCaseServers.All.Where(server => server.PortVariable is not null)
            .All(server => UpstreamTestCaseVariables.Names.Contains(server.PortVariable!)));
    }

    [TestMethod]
    public void All_OnlyTheUnixSocketServers_MapAPathVariable()
    {
        var mapped = UpstreamTestCaseServers.All
            .Where(server => server.UnixSocketPathVariable is not null)
            .Select(server => $"{server.Name} %{server.UnixSocketPathVariable}");

        CollectionAssert.AreEqual(new[] { "socks5unix %SOCKSUNIXPATH", "http-unix %HTTPUNIXPATH" }, mapped.ToArray());
        Assert.IsTrue(UpstreamTestCaseServers.All.Where(server => server.UnixSocketPathVariable is not null)
            .All(server => UpstreamTestCaseVariables.Names.Contains(server.UnixSocketPathVariable!)));
    }

    [TestMethod]
    [DataRow("httptls+srp", "HTTPTLSPORT")]
    [DataRow("httptls+srp-ipv6", "HTTPTLS6PORT")]
    public void Find_TlsSrpName_IsBuiltOverHttpsAndMapsItsPortVariable(string name, string portVariable)
    {
        var server = UpstreamTestCaseServers.Find(name);

        Assert.AreEqual(new UpstreamTestCaseServer(name, "https", IsBuilt: true, PortVariable: portVariable), server);
    }

    [TestMethod]
    public void Find_UnknownOrNoServerHalfName_GivesNull()
    {
        Assert.IsNull(UpstreamTestCaseServers.Find("quic"));
        Assert.IsNull(UpstreamTestCaseServers.Find("file"));
    }

    [TestMethod]
    public void All_TheUnbuiltRows_AreTheAdrsUnbuiltServers()
    {
        var unbuilt = UpstreamTestCaseServers.All.Where(server => !server.IsBuilt).Select(server => server.Name);

        CollectionAssert.AreEquivalent(
            new[] { "http/2", "http/3", "dns" },
            unbuilt.ToArray());
    }
}
