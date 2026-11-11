using System.Net;

namespace Surl.Cli;

[TestClass]
public sealed class ProxyAllowedOriginTests
{
    [TestMethod]
    public void EveryAddress_AdmitsEveryAddressAndReadsAsStar()
    {
        var origin = ProxyAllowedOrigin.EveryAddress;

        Assert.IsTrue(origin.AdmitsEveryAddress);
        Assert.IsNull(origin.Network);
        Assert.AreEqual(0, origin.PrefixLength);
        Assert.AreEqual("*", origin.ToString());
    }

    [TestMethod]
    public void Range_Ipv4Address_KeepsAddressAndPrefix()
    {
        var origin = ProxyAllowedOrigin.Range(IPAddress.Parse("10.0.0.0"), 8);

        Assert.IsFalse(origin.AdmitsEveryAddress);
        Assert.AreEqual(IPAddress.Parse("10.0.0.0"), origin.Network);
        Assert.AreEqual(8, origin.PrefixLength);
        Assert.AreEqual("10.0.0.0/8", origin.ToString());
    }

    [TestMethod]
    public void Range_Ipv4MappedAddress_IsTakenAsIpv4()
    {
        var origin = ProxyAllowedOrigin.Range(IPAddress.Parse("::ffff:192.0.2.1"), 32);

        Assert.AreEqual(IPAddress.Parse("192.0.2.1"), origin.Network);
    }

    [TestMethod]
    public void Range_SameRangeTwice_IsEqual() =>
        Assert.AreEqual(ProxyAllowedOrigin.Range(IPAddress.IPv6Loopback, 128), ProxyAllowedOrigin.Range(IPAddress.Parse("::1"), 128));

    [TestMethod]
    [DataRow("192.0.2.0", -1, DisplayName = "Negative")]
    [DataRow("192.0.2.0", 33, DisplayName = "Past IPv4's 32")]
    [DataRow("2001:db8::", 129, DisplayName = "Past IPv6's 128")]
    public void Range_PrefixOutsideItsFamily_Throws(string network, int prefixLength) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ProxyAllowedOrigin.Range(IPAddress.Parse(network), prefixLength));

    [TestMethod]
    public void Range_NullNetwork_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ProxyAllowedOrigin.Range(null!, 0));
}
