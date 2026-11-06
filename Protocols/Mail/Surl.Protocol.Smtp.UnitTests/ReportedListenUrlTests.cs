using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The listen URL a fault point names, in the form the serving engine reports it (ADR-0124, decision 4).
/// </summary>
[TestClass]
public sealed class ReportedListenUrlTests
{
    [TestMethod]
    [DataRow("127.0.0.1", 18025, 18025, "smtp://127.0.0.1:18025/", DisplayName = "IPv4, bound")]
    [DataRow("127.0.0.1", 0, null, "smtp://127.0.0.1:0/", DisplayName = "unbound")]
    [DataRow("fe80::1%eth0", 25, 2525, "smtp://[fe80::1%25eth0]:2525/", DisplayName = "IPv6 with a zone")]
    public void Format_ListenUrl_IsAsTheInstanceReportsIt(string host, int port, int? boundPort, string expected)
    {
        var listenUrl = new ListenUrl("smtp", host, port);

        var formatted = ReportedListenUrl.Format(boundPort is { } bound ? listenUrl.WithBoundPort(bound) : listenUrl);

        Assert.AreEqual(expected, formatted);
    }
}
