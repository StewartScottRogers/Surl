using System.Net;
using System.Text;

namespace Surl.Protocol.Smtp;

[TestClass]
public sealed class SmtpTraceFieldsTests
{
    [TestMethod]
    [DataRow("127.0.0.1", "[127.0.0.1]", DisplayName = "IPv4")]
    [DataRow("::1", "[IPv6:::1]", DisplayName = "IPv6")]
    [DataRow("::ffff:192.0.2.7", "[192.0.2.7]", DisplayName = "IPv4 mapped into IPv6")]
    public void AddressLiteral_IpEndPoint_IsRfc5321sLiteral(string address, string literal)
    {
        Assert.AreEqual(literal, SmtpTraceFields.AddressLiteral(new IPEndPoint(IPAddress.Parse(address), 50000)));
    }

    [TestMethod]
    public void AddressLiteral_EndPointThatIsNoIpAddress_IsUnknown()
    {
        Assert.AreEqual("[unknown]", SmtpTraceFields.AddressLiteral(new DnsEndPoint("client.example", 50000)));
    }

    [TestMethod]
    public void Build_WritesReturnPathAndReceived()
    {
        var receivedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));

        var fields = SmtpTraceFields.Build("ä@x", "client.example"u8.ToArray(), new IPEndPoint(IPAddress.IPv6Loopback, 1), "ESMTPS", receivedAt);

        Assert.AreEqual(
            "Return-Path: <ä@x>\r\nReceived: from client.example ([IPv6:::1]) by surl with ESMTPS; Fri, 02 Jan 2026 01:04:05 +0000\r\n",
            Encoding.UTF8.GetString(fields));
    }
}
