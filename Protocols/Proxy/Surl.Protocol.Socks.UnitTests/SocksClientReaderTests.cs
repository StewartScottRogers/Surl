using static Surl.Protocol.Socks.SocksTestKit;

namespace Surl.Protocol.Socks;

[TestClass]
public sealed class SocksClientReaderTests
{
    [TestMethod]
    public async Task ReadAsync_HeldBytesLongerThanTheDestination_CopiesWhatFitsThenTheRest()
    {
        var reader = new SocksClientReader(ConnectionOf([1, 2, 3, 4]));
        await reader.ReadByteAsync(CancellationToken.None);
        var first = new byte[2];
        var second = new byte[4];

        var firstCount = await reader.ReadAsync(first, CancellationToken.None);
        var secondCount = await reader.ReadAsync(second, CancellationToken.None);

        Assert.AreEqual(2, firstCount);
        CollectionAssert.AreEqual(new byte[] { 2, 3 }, first);
        Assert.AreEqual(1, secondCount);
        Assert.AreEqual(4, second[0]);
    }

    [TestMethod]
    public async Task ReadByteAsync_AfterTheClientHalfClosed_KeepsReturningMinusOne()
    {
        var reader = new SocksClientReader(ConnectionOf([7]));

        var first = await reader.ReadByteAsync(CancellationToken.None);
        var second = await reader.ReadByteAsync(CancellationToken.None);
        var third = await reader.ReadByteAsync(CancellationToken.None);

        Assert.AreEqual(7, first);
        Assert.AreEqual(-1, second);
        Assert.AreEqual(-1, third);
    }

    [TestMethod]
    public async Task ReadAsync_NothingHeld_ReadsTheConnection()
    {
        var reader = new SocksClientReader(ConnectionOf([5, 6]));
        var destination = new byte[8];

        var count = await reader.ReadAsync(destination, CancellationToken.None);

        Assert.AreEqual(2, count);
    }
}
