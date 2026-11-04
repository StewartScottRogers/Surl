using System.Net;

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
