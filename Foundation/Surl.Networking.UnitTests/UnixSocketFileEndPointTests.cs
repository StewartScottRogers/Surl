using System.Net.Sockets;

namespace Surl.Networking;

[TestClass]
public sealed class UnixSocketFileEndPointTests
{
    [TestMethod]
    public void Serialize_IsTheWrappedAddresssBytes()
    {
        var address = new UnixDomainSocketEndPoint("/tmp/surl.sock");
        var endPoint = new UnixSocketFileEndPoint(address);

        var serialized = endPoint.Serialize();

        Assert.AreEqual(address.Serialize(), serialized);
        Assert.AreEqual(AddressFamily.Unix, endPoint.AddressFamily);
    }

    [TestMethod]
    public void Create_ReportedAddress_IsAUnixDomainSocketEndPoint()
    {
        var address = new UnixDomainSocketEndPoint("/tmp/surl.sock");
        var endPoint = new UnixSocketFileEndPoint(address);

        var created = endPoint.Create(address.Serialize());

        Assert.IsInstanceOfType<UnixDomainSocketEndPoint>(created);
        Assert.AreEqual(address.ToString(), created.ToString());
    }

    [TestMethod]
    public void ToString_IsTheWrappedAddresssPath()
    {
        var endPoint = new UnixSocketFileEndPoint(new UnixDomainSocketEndPoint("/tmp/surl.sock"));

        Assert.AreEqual("/tmp/surl.sock", endPoint.ToString());
    }
}
