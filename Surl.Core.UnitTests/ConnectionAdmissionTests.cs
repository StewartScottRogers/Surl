using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

[TestClass]
public sealed class ConnectionAdmissionTests
{
    private static readonly IPEndPoint First = new(IPAddress.Parse("192.0.2.1"), 50000);
    private static readonly IPEndPoint FirstAgain = new(IPAddress.Parse("192.0.2.1"), 50001);
    private static readonly IPEndPoint Second = new(IPAddress.Parse("192.0.2.2"), 50000);

    [TestMethod]
    public void TryAdmit_PastTheTotal_RefusesTooManyConnections()
    {
        var admission = new ConnectionAdmission(maxConnections: 2, maxConnectionsPerAddress: 0);

        Assert.IsNull(admission.TryAdmit(First));
        Assert.IsNull(admission.TryAdmit(Second));
        Assert.AreEqual(ConnectionRefusal.TooManyConnections, admission.TryAdmit(new IPEndPoint(IPAddress.Parse("192.0.2.3"), 1)));
    }

    [TestMethod]
    public void TryAdmit_PastBothLimits_RefusesTooManyConnectionsFirst()
    {
        var admission = new ConnectionAdmission(maxConnections: 1, maxConnectionsPerAddress: 1);

        Assert.IsNull(admission.TryAdmit(First));
        Assert.AreEqual(ConnectionRefusal.TooManyConnections, admission.TryAdmit(FirstAgain));
    }

    [TestMethod]
    public void TryAdmit_PastThePerAddressLimit_RefusesThatAddressOnly()
    {
        var admission = new ConnectionAdmission(maxConnections: 0, maxConnectionsPerAddress: 1);

        Assert.IsNull(admission.TryAdmit(First));
        Assert.AreEqual(ConnectionRefusal.TooManyConnectionsFromAddress, admission.TryAdmit(FirstAgain));
        Assert.IsNull(admission.TryAdmit(Second));
    }

    [TestMethod]
    public void TryAdmit_IPv4MappedIPv6Address_CountsAsItsIPv4Address()
    {
        var admission = new ConnectionAdmission(maxConnections: 0, maxConnectionsPerAddress: 1);

        Assert.IsNull(admission.TryAdmit(First));
        Assert.AreEqual(
            ConnectionRefusal.TooManyConnectionsFromAddress,
            admission.TryAdmit(new IPEndPoint(First.Address.MapToIPv6(), 50002)));
    }

    [TestMethod]
    public void TryAdmit_EndPointWithNoAddress_CountsOnlyAgainstTheTotal()
    {
        var admission = new ConnectionAdmission(maxConnections: 2, maxConnectionsPerAddress: 1);
        var named = new DnsEndPoint("peer.example", 50000);

        Assert.IsNull(admission.TryAdmit(named));
        Assert.IsNull(admission.TryAdmit(named));
        Assert.AreEqual(ConnectionRefusal.TooManyConnections, admission.TryAdmit(named));

        admission.Release(named);

        Assert.IsNull(admission.TryAdmit(named));
    }

    [TestMethod]
    public void Release_FreesTheSlotInTheTotalAndForTheAddress()
    {
        var admission = new ConnectionAdmission(maxConnections: 2, maxConnectionsPerAddress: 1);

        Assert.IsNull(admission.TryAdmit(First));
        Assert.IsNull(admission.TryAdmit(Second));
        admission.Release(First);

        Assert.IsNull(admission.TryAdmit(FirstAgain));
        Assert.AreEqual(ConnectionRefusal.TooManyConnections, admission.TryAdmit(new IPEndPoint(IPAddress.Parse("192.0.2.3"), 1)));
    }

    [TestMethod]
    public void Release_OneOfTwoFromAnAddress_KeepsCountingTheOther()
    {
        var admission = new ConnectionAdmission(maxConnections: 0, maxConnectionsPerAddress: 2);

        Assert.IsNull(admission.TryAdmit(First));
        Assert.IsNull(admission.TryAdmit(FirstAgain));
        admission.Release(First);

        Assert.IsNull(admission.TryAdmit(First));
        Assert.AreEqual(ConnectionRefusal.TooManyConnectionsFromAddress, admission.TryAdmit(First));
    }

    [TestMethod]
    public void TryAdmit_NoLimits_AdmitsEveryConnection()
    {
        var admission = new ConnectionAdmission(maxConnections: 0, maxConnectionsPerAddress: 0);

        for (var index = 0; index < 5000; index++)
        {
            Assert.IsNull(admission.TryAdmit(First));
        }
    }
}
