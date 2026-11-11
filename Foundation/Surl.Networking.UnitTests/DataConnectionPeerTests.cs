using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
public sealed class DataConnectionPeerTests
{
    private static readonly IPEndPoint Peer = new(IPAddress.Parse("192.0.2.7"), 50000);

    [TestMethod]
    public void Normalize_Ipv4MappedAddress_IsIpv4()
    {
        Assert.AreEqual(IPAddress.Parse("192.0.2.7"), DataConnectionPeer.Normalize(IPAddress.Parse("::ffff:192.0.2.7")));
    }

    [TestMethod]
    [DataRow("192.0.2.7")]
    [DataRow("2001:db8::7")]
    public void Normalize_OtherAddress_IsItself(string address)
    {
        Assert.AreEqual(IPAddress.Parse(address), DataConnectionPeer.Normalize(IPAddress.Parse(address)));
    }

    [TestMethod]
    [DataRow("192.0.2.7", true)]
    [DataRow("::ffff:192.0.2.7", true)]
    [DataRow("192.0.2.8", false)]
    [DataRow("2001:db8::7", false)]
    public void IsPeer_ComparesNormalizedAddressesAndIgnoresPorts(string candidate, bool expected)
    {
        Assert.AreEqual(expected, DataConnectionPeer.IsPeer(Peer, new IPEndPoint(IPAddress.Parse(candidate), 1)));
    }

    [TestMethod]
    public void IsPeer_MappedControlRemote_MatchesIpv4Candidate()
    {
        var mappedPeer = new IPEndPoint(IPAddress.Parse("::ffff:192.0.2.7"), 50000);

        Assert.IsTrue(DataConnectionPeer.IsPeer(mappedPeer, new IPEndPoint(IPAddress.Parse("192.0.2.7"), 2000)));
    }

    [TestMethod]
    public void IsPeer_NonIpEndPoints_IsFalse()
    {
        Assert.IsFalse(DataConnectionPeer.IsPeer(new DnsEndPoint("localhost", 21), Peer));
        Assert.IsFalse(DataConnectionPeer.IsPeer(Peer, new DnsEndPoint("localhost", 21)));
    }

    [TestMethod]
    [DataRow("127.0.0.1", true)]
    [DataRow("127.0.0.9", true)]
    [DataRow("::1", true)]
    [DataRow("::ffff:127.0.0.1", true)]
    [DataRow("192.0.2.7", false)]
    public void IsPeer_UnixSocketPeer_IsAnyLoopbackAddress(string candidate, bool expected)
    {
        var candidateEndPoint = new IPEndPoint(IPAddress.Parse(candidate), 2000);

        Assert.AreEqual(expected, DataConnectionPeer.IsPeer(UnnamedUnixSocketPeer.Instance, candidateEndPoint));
        Assert.AreEqual(expected, DataConnectionPeer.IsPeer(new UnixDomainSocketEndPoint("/tmp/client.sock"), candidateEndPoint));
    }

    [TestMethod]
    public void IsAllowedActiveTarget_UnixSocketPeer_IsLoopbackFromPort1024()
    {
        Assert.IsTrue(DataConnectionPeer.IsAllowedActiveTarget(UnnamedUnixSocketPeer.Instance, new IPEndPoint(IPAddress.Loopback, 1024)));
        Assert.IsFalse(DataConnectionPeer.IsAllowedActiveTarget(UnnamedUnixSocketPeer.Instance, new IPEndPoint(IPAddress.Loopback, 1023)));
        Assert.IsFalse(DataConnectionPeer.IsAllowedActiveTarget(UnnamedUnixSocketPeer.Instance, new IPEndPoint(IPAddress.Parse("192.0.2.7"), 2000)));
    }
    [TestMethod]
    [DataRow("192.0.2.7", 1024, true)]
    [DataRow("192.0.2.7", 65535, true)]
    [DataRow("192.0.2.7", 1023, false)]
    [DataRow("192.0.2.7", 21, false)]
    [DataRow("198.51.100.1", 2000, false)]
    public void IsAllowedActiveTarget_OnlyThePeersAddressFromPort1024(string address, int port, bool expected)
    {
        Assert.AreEqual(
            expected, DataConnectionPeer.IsAllowedActiveTarget(Peer, new IPEndPoint(IPAddress.Parse(address), port)));
    }
}
