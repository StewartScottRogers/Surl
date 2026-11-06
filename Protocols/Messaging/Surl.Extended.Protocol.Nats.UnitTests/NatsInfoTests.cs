using System.Net;
using System.Text;

namespace Surl.Extended.Protocol.Nats.UnitTests;

[TestClass]
public sealed class NatsInfoTests
{
    [TestMethod]
    public void Encode_IPv4_IsTheInfoLineWithTheAdrsFieldsInOrder()
    {
        var info = new NatsInfo("ID", new IPEndPoint(IPAddress.Loopback, 4222), new IPEndPoint(IPAddress.Parse("10.0.0.7"), 50000), 7, 1048576, true, false);

        Assert.AreEqual(
            "INFO {\"server_id\":\"ID\",\"server_name\":\"ID\",\"version\":\"2.10.0\",\"proto\":1,\"host\":\"127.0.0.1\",\"port\":4222,"
            + "\"headers\":true,\"max_payload\":1048576,\"client_id\":7,\"client_ip\":\"10.0.0.7\",\"auth_required\":true,"
            + "\"tls_required\":false,\"tls_verify\":false,\"tls_available\":false}\r\n",
            Encoding.UTF8.GetString(info.Encode()));
    }

    [TestMethod]
    public void Encode_TlsRequired_SaysTlsRequiredButNotAvailable()
    {
        var info = new NatsInfo("ID", new IPEndPoint(IPAddress.Loopback, 4222), new IPEndPoint(IPAddress.Loopback, 1), 1, 10, true, true);

        var text = Encoding.UTF8.GetString(info.Encode());

        StringAssert.EndsWith(text, "\"tls_required\":true,\"tls_verify\":false,\"tls_available\":false}\r\n");
    }

    [TestMethod]
    public void Encode_IPv6_HasNoBrackets()
    {
        var info = new NatsInfo("ID", new IPEndPoint(IPAddress.IPv6Loopback, 4222), new IPEndPoint(IPAddress.IPv6Loopback, 1), 1, 10, false, false);

        var text = Encoding.UTF8.GetString(info.Encode());

        StringAssert.Contains(text, "\"host\":\"::1\",\"port\":4222");
        StringAssert.Contains(text, "\"client_ip\":\"::1\",\"auth_required\":false");
    }

    [TestMethod]
    public void Encode_EndPointsWithoutAddresses_HaveEmptyHostsAndPortZero()
    {
        var info = new NatsInfo("ID", new DnsEndPoint("socket", 1), new DnsEndPoint("peer", 2), 1, 10, true, false);

        var text = Encoding.UTF8.GetString(info.Encode());

        StringAssert.Contains(text, "\"host\":\"\",\"port\":0");
        StringAssert.Contains(text, "\"client_ip\":\"\"");
    }
}
