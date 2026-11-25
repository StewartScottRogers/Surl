using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class BoundProxyEndPointsTests
{
    [TestMethod]
    public void Record_AProxyListenUrlThatBound_KeepsItsAddressAndPort()
    {
        var bound = new BoundProxyEndPoints();

        bound.Record((new ListenUrl("http", "127.0.0.1", 0) { IsProxy = true }).WithBoundPort(4001));

        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Loopback, 4001) }, bound.Snapshot().ToArray());
    }

    [TestMethod]
    public void Record_ASocksListenUrlWhoseHostIsNotAnAddress_KeepsEveryAddress()
    {
        var bound = new BoundProxyEndPoints();

        bound.Record(new ListenUrl("socks5", "localhost", 0).WithBoundPort(4002));

        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Any, 4002) }, bound.Snapshot().ToArray());
    }

    [TestMethod]
    public void Record_AListenUrlThatIsNotAProxy_KeepsNothing()
    {
        var bound = new BoundProxyEndPoints();

        bound.Record(new ListenUrl("http", "127.0.0.1", 0).WithBoundPort(4003));

        Assert.AreEqual(0, bound.Snapshot().Count);
    }

    [TestMethod]
    public void Record_AProxyListenUrlThatHasNotBound_KeepsNothing()
    {
        var bound = new BoundProxyEndPoints();

        bound.Record(new ListenUrl("socks5", "127.0.0.1", 1080));

        Assert.AreEqual(0, bound.Snapshot().Count);
    }
}
