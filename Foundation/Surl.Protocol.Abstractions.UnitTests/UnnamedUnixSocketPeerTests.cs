using System.Net;
using System.Net.Sockets;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class UnnamedUnixSocketPeerTests
{
    [TestMethod]
    public void Instance_IsOneSharedUnixEndPointWithNoIpAddress()
    {
        EndPoint peer = UnnamedUnixSocketPeer.Instance;

        Assert.AreSame(UnnamedUnixSocketPeer.Instance, peer);
        Assert.AreEqual(AddressFamily.Unix, peer.AddressFamily);
        Assert.IsNotInstanceOfType<IPEndPoint>(peer);
    }

    [TestMethod]
    public void ToString_IsUnixPeer()
    {
        Assert.AreEqual("unix peer", UnnamedUnixSocketPeer.Instance.ToString());
    }
}
