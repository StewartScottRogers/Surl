using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// <see cref="AllowedOriginRule"/>: loopback by default, widened by ranges or to every address,
/// and never the proxy's own end point (ADR-0088, decisions 2 and 7).
/// </summary>
[TestClass]
public sealed class AllowedOriginRuleTests
{
    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("127.255.0.9")]
    [DataRow("::1")]
    [DataRow("::ffff:127.0.0.1")]
    public void Admits_LoopbackOnly_AdmitsLoopback(string address)
    {
        Assert.IsTrue(AllowedOriginRule.LoopbackOnly.Admits(new IPEndPoint(IPAddress.Parse(address), 80)));
    }

    [TestMethod]
    [DataRow("192.0.2.1")]
    [DataRow("10.0.0.1")]
    [DataRow("2001:db8::1")]
    [DataRow("::ffff:192.0.2.1")]
    public void Admits_LoopbackOnly_RefusesEveryOtherAddress(string address)
    {
        Assert.IsFalse(AllowedOriginRule.LoopbackOnly.Admits(new IPEndPoint(IPAddress.Parse(address), 80)));
    }

    [TestMethod]
    public void Admits_EveryAddress_AdmitsAnyAddress()
    {
        Assert.IsTrue(AllowedOriginRule.EveryAddress.Admits(new IPEndPoint(IPAddress.Parse("203.0.113.5"), 443)));
    }

    [TestMethod]
    [DataRow("192.0.2.0", 24, "192.0.2.200", true)]
    [DataRow("192.0.2.0", 24, "192.0.3.1", false)]
    [DataRow("192.0.2.0", 25, "192.0.2.127", true)]
    [DataRow("192.0.2.0", 25, "192.0.2.128", false)]
    [DataRow("192.0.2.7", 32, "192.0.2.7", true)]
    [DataRow("0.0.0.0", 0, "203.0.113.5", true)]
    [DataRow("2001:db8::", 32, "2001:db8:1::5", true)]
    [DataRow("2001:db8::", 32, "2001:db9::5", false)]
    [DataRow("192.0.2.0", 24, "2001:db8::5", false)]
    [DataRow("::ffff:192.0.2.0", 24, "192.0.2.9", true)]
    [DataRow("192.0.2.0", 24, "::ffff:192.0.2.9", true)]
    public void Admits_WithRange_AdmitsExactlyTheRange(string network, int prefixLength, string address, bool admitted)
    {
        var rule = AllowedOriginRule.LoopbackOnly.WithRange(IPAddress.Parse(network), prefixLength);

        Assert.AreEqual(admitted, rule.Admits(new IPEndPoint(IPAddress.Parse(address), 80)));
    }

    [TestMethod]
    public void WithRange_LeavesTheOriginalRuleUnchanged()
    {
        _ = AllowedOriginRule.LoopbackOnly.WithRange(IPAddress.Parse("192.0.2.0"), 24);

        Assert.IsFalse(AllowedOriginRule.LoopbackOnly.Admits(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 80)));
    }

    [TestMethod]
    [DataRow("192.0.2.0", -1)]
    [DataRow("192.0.2.0", 33)]
    [DataRow("2001:db8::", 129)]
    public void WithRange_PrefixOutsideTheFamily_Throws(string network, int prefixLength)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => AllowedOriginRule.LoopbackOnly.WithRange(IPAddress.Parse(network), prefixLength));
    }

    [TestMethod]
    [DataRow("127.0.0.1", 3128, "127.0.0.1", 3128, false)]
    [DataRow("127.0.0.1", 3128, "::ffff:127.0.0.1", 3128, false)]
    [DataRow("127.0.0.1", 3128, "127.0.0.2", 3128, true)]
    [DataRow("127.0.0.1", 3128, "127.0.0.1", 3129, true)]
    [DataRow("0.0.0.0", 3128, "127.0.0.5", 3128, false)]
    [DataRow("::", 3128, "::1", 3128, false)]
    [DataRow("::", 3128, "::1", 80, true)]
    public void Admits_WithProxyEndPoint_RefusesTheProxyItself(
        string proxyAddress, int proxyPort, string originAddress, int originPort, bool admitted)
    {
        var rule = AllowedOriginRule.EveryAddress.WithProxyEndPoint(new IPEndPoint(IPAddress.Parse(proxyAddress), proxyPort));

        Assert.AreEqual(admitted, rule.Admits(new IPEndPoint(IPAddress.Parse(originAddress), originPort)));
    }

    [TestMethod]
    public void Admits_RangeThenProxyEndPoint_KeepsBoth()
    {
        var rule = AllowedOriginRule.LoopbackOnly
            .WithRange(IPAddress.Parse("192.0.2.0"), 24)
            .WithProxyEndPoint(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 3128));

        Assert.IsTrue(rule.Admits(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 80)));
        Assert.IsFalse(rule.Admits(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 3128)));
    }

    [TestMethod]
    public void Members_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => AllowedOriginRule.LoopbackOnly.WithRange(null!, 8));
        Assert.ThrowsExactly<ArgumentNullException>(() => AllowedOriginRule.LoopbackOnly.WithProxyEndPoint(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => AllowedOriginRule.LoopbackOnly.Admits(null!));
    }
}
