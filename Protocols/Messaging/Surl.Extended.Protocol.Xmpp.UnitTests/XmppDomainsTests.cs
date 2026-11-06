using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// The domains a connection serves (BL-536; ADR-0114 decision 1) and how a <c>to</c> is compared
/// with them (RFC 7622 section 3.2).
/// </summary>
[TestClass]
public sealed class XmppDomainsTests
{
    [TestMethod]
    [DataRow("im.example.com", "192.0.2.1", "im.example.com")]
    [DataRow("IM.Example.COM", "192.0.2.1", "im.example.com")]
    [DataRow("192.0.2.1", "192.0.2.1", "192.0.2.1")]
    [DataRow("2001:db8::1", "2001:db8::1", "[2001:db8::1]")]
    [DataRow("0.0.0.0", "192.0.2.7", "192.0.2.7")]
    [DataRow("::", "2001:db8::7", "[2001:db8::7]")]
    [DataRow("::", "::ffff:192.0.2.7", "192.0.2.7")]
    [DataRow("localhost", "127.0.0.1", "localhost,127.0.0.1")]
    [DataRow("localhost", "::1", "localhost,[::1]")]
    [DataRow("localhost", "192.0.2.1", "localhost")]
    [DataRow("127.0.0.1", "127.0.0.1", "localhost,127.0.0.1")]
    [DataRow("::1", "::1", "localhost,[::1]")]
    public void Served_ListenHost_ServesTheDecisionsDomains(string listenHost, string localAddress, string expected)
    {
        var domains = XmppDomains.Served(Listen(listenHost), new IPEndPoint(IPAddress.Parse(localAddress), 5222), []);

        Assert.AreEqual(expected, string.Join(',', domains));
    }

    [TestMethod]
    public void Served_MailDomains_FollowTheListenDomainsLowerCasedWithoutDuplicates()
    {
        var domains = XmppDomains.Served(
            Listen("localhost"), new IPEndPoint(IPAddress.Loopback, 5222), ["Example.COM", "localhost", "example.com."]);

        Assert.AreEqual("localhost,127.0.0.1,example.com", string.Join(',', domains));
    }

    [TestMethod]
    [DataRow("localhost")]
    [DataRow("0.0.0.0")]
    public void Served_OnAUnixSocket_ServesNoAddressLiteral(string listenHost)
    {
        var domains = XmppDomains.Served(Listen(listenHost), new UnixDomainSocketEndPoint("/tmp/surl.sock"), []);

        Assert.AreEqual(listenHost == "localhost" ? "localhost" : string.Empty, string.Join(',', domains));
    }

    [TestMethod]
    [DataRow("Example.COM.", "example.com")]
    [DataRow("[2001:DB8::1]", "[2001:db8::1]")]
    [DataRow("[not-an-address]", "[not-an-address]")]
    [DataRow("::1", "[::1]")]
    [DataRow("fe80::1%3", "[fe80::1]")]
    public void Normalized_Domain_IsTheComparedForm(string domain, string expected)
    {
        Assert.AreEqual(expected, XmppDomains.Normalized(domain));
    }

    private static ListenUrl Listen(string host) => new("xmpp", host, 5222);
}
