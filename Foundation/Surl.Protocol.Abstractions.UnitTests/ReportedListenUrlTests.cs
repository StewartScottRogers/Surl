namespace Surl.Protocol.Abstractions;

/// <summary>
/// The listen URL as the instance reports it (ADR-0007, section 7; ADR-0124, decision 4).
/// </summary>
[TestClass]
public sealed class ReportedListenUrlTests
{
    [TestMethod]
    [DataRow("http", "::1", 8080, null, "http://[::1]:8080/", DisplayName = "IPv6, unbound")]
    [DataRow("http", "fe80::1%eth0", 8080, null, "http://[fe80::1%25eth0]:8080/", DisplayName = "IPv6 with a zone")]
    [DataRow("dict", "localhost", 2628, null, "dict://localhost:2628/", DisplayName = "host name")]
    [DataRow("smtp", "127.0.0.1", 0, null, "smtp://127.0.0.1:0/", DisplayName = "port 0, unbound")]
    [DataRow("http", "127.0.0.1", 0, 49731, "http://127.0.0.1:49731/", DisplayName = "IPv4, bound")]
    [DataRow("smtp", "fe80::1%eth0", 25, 2525, "smtp://[fe80::1%25eth0]:2525/", DisplayName = "IPv6 with a zone, bound")]
    public void Format_ListenUrl_IsAsTheInstanceReportsIt(string scheme, string host, int port, int? boundPort, string expected)
    {
        var listenUrl = new ListenUrl(scheme, host, port);

        var formatted = ReportedListenUrl.Format(boundPort is { } bound ? listenUrl.WithBoundPort(bound) : listenUrl);

        Assert.AreEqual(expected, formatted);
    }

    [TestMethod]
    public void Format_NoListenUrl_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ReportedListenUrl.Format(null!));
    }
}
