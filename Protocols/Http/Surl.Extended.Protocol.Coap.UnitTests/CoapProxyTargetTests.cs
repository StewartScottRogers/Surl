using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapProxyTarget"/> and <see cref="CoapRequestOptions.ResolveLocalProxy"/>: a proxy
/// request that names the listener is served as local (RFC 7252 sections 5.7.2, 5.10.2 and 6.4;
/// ADR-0097 decision 3).
/// </summary>
[TestClass]
public sealed class CoapProxyTargetTests
{
    private static readonly ListenUrl Listener = new ListenUrl("coap", "127.0.0.1", 0).WithBoundPort(5683);

    [TestMethod]
    [DataRow("coap://127.0.0.1:5683/f.txt", "f.txt", "")]
    [DataRow("coap://127.0.0.1/f.txt", "f.txt", "")]
    [DataRow("COAP://LOCALHOST:5683/a/b%20c/?x=1&y=%32", "a|b c|", "x=1|y=2")]
    [DataRow("coap://[::1]:5683/", "", "")]
    [DataRow("coap://127.0.0.1:5683", "", "")]
    public void ResolveLocalProxy_ProxyUriNamingTheListener_IsServedFromItsPathAndQuery(string proxyUri, string path, string query)
    {
        var options = Read(CoapOption.FromString(CoapOptionNumber.ProxyUri, proxyUri), CoapOption.FromString(CoapOptionNumber.UriPath, "ignored"));

        var local = options.ResolveLocalProxy(Listener);

        Assert.IsFalse(local.NamesAProxy);
        Assert.AreEqual(path, string.Join('|', local.UriPath));
        Assert.AreEqual(query, string.Join('|', local.UriQuery));
    }

    [TestMethod]
    [DataRow("coap://127.0.0.1:5684/f.txt")]
    [DataRow("coap://10.0.0.9:5683/f.txt")]
    [DataRow("coaps://127.0.0.1:5683/f.txt")]
    [DataRow("coap://127.0.0.1:5683/f.txt#frag")]
    [DataRow("/f.txt")]
    public void ResolveLocalProxy_ProxyUriNamingAnother_StaysAProxyRequest(string proxyUri)
    {
        var options = Read(CoapOption.FromString(CoapOptionNumber.ProxyUri, proxyUri));

        Assert.AreSame(options, options.ResolveLocalProxy(Listener));
        Assert.IsTrue(options.NamesAProxy);
    }

    [TestMethod]
    public void ResolveLocalProxy_ProxySchemeWithoutAHost_IsServedFromTheUriPath()
    {
        var options = Read(CoapOption.FromString(CoapOptionNumber.ProxyScheme, "coap"), CoapOption.FromString(CoapOptionNumber.UriPath, "f.txt"));

        var local = options.ResolveLocalProxy(Listener);

        Assert.IsFalse(local.NamesAProxy);
        CollectionAssert.AreEqual(new[] { "f.txt" }, local.UriPath.ToArray());
    }

    [TestMethod]
    public void ResolveLocalProxy_ProxySchemeWithTheListenersHostAndPort_IsServed()
    {
        var options = Read(
            CoapOption.FromString(CoapOptionNumber.ProxyScheme, "coap"),
            CoapOption.FromString(CoapOptionNumber.UriHost, "127.0.0.1"),
            CoapOption.FromUInt(CoapOptionNumber.UriPort, 5683));

        Assert.IsFalse(options.ResolveLocalProxy(Listener).NamesAProxy);
    }

    [TestMethod]
    public void ResolveLocalProxy_ProxySchemeWithAHostAndNoPort_UsesTheSchemesDefaultPort()
    {
        var onDefault = new ListenUrl("coap", "127.0.0.1", 5683);
        var secure = new ListenUrl("coaps", "127.0.0.1", 5684);
        var host = CoapOption.FromString(CoapOptionNumber.UriHost, "127.0.0.1");

        Assert.IsFalse(Read(CoapOption.FromString(CoapOptionNumber.ProxyScheme, "coap"), host).ResolveLocalProxy(onDefault).NamesAProxy);
        Assert.IsFalse(Read(CoapOption.FromString(CoapOptionNumber.ProxyScheme, "coaps"), host).ResolveLocalProxy(secure).NamesAProxy);
        Assert.IsTrue(Read(CoapOption.FromString(CoapOptionNumber.ProxyScheme, "coap"), host).ResolveLocalProxy(Listener with { BoundPort = 9 }).NamesAProxy);
    }

    [TestMethod]
    public void ResolveLocalProxy_ProxySchemeNamingAnotherSchemeOrPort_StaysAProxyRequest()
    {
        Assert.IsTrue(Read(CoapOption.FromString(CoapOptionNumber.ProxyScheme, "coaps")).ResolveLocalProxy(Listener).NamesAProxy);
        Assert.IsTrue(Read(CoapOption.FromString(CoapOptionNumber.ProxyScheme, "coap"), CoapOption.FromUInt(CoapOptionNumber.UriPort, 1)).ResolveLocalProxy(Listener).NamesAProxy);
    }

    [TestMethod]
    public void ResolveLocalProxy_WithoutAProxyOption_ReturnsTheSameOptions()
    {
        var options = Read(CoapOption.FromString(CoapOptionNumber.UriPath, "f.txt"));

        Assert.AreSame(options, options.ResolveLocalProxy(Listener));
    }

    [TestMethod]
    public void ResolveLocalProxy_ListenerUnbound_UsesTheAskedPort()
    {
        var options = Read(CoapOption.FromString(CoapOptionNumber.ProxyUri, "coap://127.0.0.1:5683/x"));

        Assert.IsFalse(options.ResolveLocalProxy(new ListenUrl("coap", "127.0.0.1", 5683)).NamesAProxy);
    }

    [TestMethod]
    public void ResolveLocalProxy_ProxySchemeWithoutAHostOnAnUnboundListener_UsesTheAskedPort()
    {
        var options = Read(CoapOption.FromString(CoapOptionNumber.ProxyScheme, "coap"));

        Assert.IsFalse(options.ResolveLocalProxy(new ListenUrl("coap", "127.0.0.1", 5683)).NamesAProxy);
    }

    private static CoapRequestOptions Read(params CoapOption[] options) =>
        CoapRequestOptions.Read(new CoapMessage(CoapMessageType.Confirmable, CoapCode.Get, 1, default, options));
}
