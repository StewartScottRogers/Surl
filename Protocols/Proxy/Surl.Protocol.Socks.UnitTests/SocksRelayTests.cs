using static Surl.Protocol.Socks.SocksTestKit;

namespace Surl.Protocol.Socks;

[TestClass]
public sealed class SocksRelayTests
{
    [TestMethod]
    public async Task RelayAsync_BothSidesHalfClose_RelaysEachWayAndReturnsTrue()
    {
        var client = ConnectionOf([1, 2], [3]);
        var origin = ConnectionOf([9]);

        var closedCleanly = await SocksRelay.RelayAsync(client, new SocksClientReader(client), origin, CancellationToken.None);

        Assert.IsTrue(closedCleanly);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, origin.WrittenBytes);
        CollectionAssert.AreEqual(new byte[] { 9 }, client.WrittenBytes);
    }

    [TestMethod]
    public async Task RelayAsync_ClientResets_AbortsBothAndReturnsFalse()
    {
        var inner = ConnectionOf();
        var client = new FailingConnection(inner, resetsInsteadOfClosing: true);
        var origin = HeldOpenConnectionOf();

        var closedCleanly = await SocksRelay.RelayAsync(client, new SocksClientReader(client), origin, CancellationToken.None);

        Assert.IsFalse(closedCleanly);
        Assert.IsTrue(inner.Aborted);
        Assert.IsTrue(origin.Aborted);
    }
}
