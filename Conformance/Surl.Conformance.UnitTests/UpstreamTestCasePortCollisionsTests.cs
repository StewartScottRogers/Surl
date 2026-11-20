namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCasePortCollisionsTests
{
    [TestMethod]
    public void AnyAcrossHosts_TlsOnIPv4SharesPortWithPlaintextOnIPv6_IsTrue()
    {
        var ports = new Dictionary<string, int> { ["wss 127.0.0.1"] = 4000, ["http ::1"] = 4000, ["http 127.0.0.1"] = 4001 };

        Assert.IsTrue(UpstreamTestCasePortCollisions.AnyAcrossHosts(ports));
    }

    [TestMethod]
    public void AnyAcrossHosts_OneSchemeSharesPortAcrossHosts_IsFalse()
    {
        var ports = new Dictionary<string, int> { ["http 127.0.0.1"] = 4000, ["http ::1"] = 4000, ["wss 127.0.0.1"] = 4001 };

        Assert.IsFalse(UpstreamTestCasePortCollisions.AnyAcrossHosts(ports));
    }

    [TestMethod]
    public void AnyAcrossHosts_UnboundListenersAtPortZero_AreIgnored()
    {
        var ports = new Dictionary<string, int> { ["http unix"] = 0, ["wss 127.0.0.1"] = 0, ["ftp ::1"] = 0 };

        Assert.IsFalse(UpstreamTestCasePortCollisions.AnyAcrossHosts(ports));
    }

    [TestMethod]
    public void AnyAcrossHosts_NullPorts_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePortCollisions.AnyAcrossHosts(null!));
}
