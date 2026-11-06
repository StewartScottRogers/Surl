using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

[TestClass]
public sealed class FaultListenUrlTests
{
    [TestMethod]
    [DataRow("127.0.0.1", 2121, null, "ldap://127.0.0.1:2121/")]
    [DataRow("::1", 0, 40021, "ldap://[::1]:40021/")]
    [DataRow("fe80::1%eth0", 21, null, "ldap://[fe80::1%25eth0]:21/")]
    public void Text_ListenUrl_IsTheFormTheServingEngineReports(string host, int port, int? boundPort, string expected)
    {
        var listenUrl = new ListenUrl("ldap", host, port) { BoundPort = boundPort };

        var text = FaultListenUrl.Text(listenUrl);

        Assert.AreEqual(expected, text);
    }
}
