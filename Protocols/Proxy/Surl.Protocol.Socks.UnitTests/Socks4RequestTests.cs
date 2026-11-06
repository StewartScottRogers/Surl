using System.Net;

namespace Surl.Protocol.Socks;

[TestClass]
public sealed class Socks4RequestTests
{
    [TestMethod]
    public void Host_Socks4Request_IsTheAddressLiteral()
    {
        var request = new Socks4Request(Socks4Request.ConnectCommand, 80, IPAddress.Parse("10.1.2.3"), "alice", null);

        Assert.AreEqual("10.1.2.3", request.Host);
    }

    [TestMethod]
    public void Host_Socks4aRequest_IsTheHostName()
    {
        var request = new Socks4Request(Socks4Request.ConnectCommand, 80, null, string.Empty, "origin.test");

        Assert.AreEqual("origin.test", request.Host);
    }
}
