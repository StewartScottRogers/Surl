using Surl.Protocol.Abstractions;
using static Surl.Protocol.Dict.DictTestExchange;

namespace Surl.Protocol.Dict;

[TestClass]
public sealed class ConnectionRefusalTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_Writes420AndCompletesWritesWithoutAborting(ConnectionRefusal refusal)
    {
        var connection = new InMemoryConnection([]);

        await Server().WriteRefusalAsync(connection, refusal, TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("refused", "stdout.bin"), connection.WrittenBytes);
        Assert.AreEqual("420 server temporarily unavailable\r\n", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public void RecordedRefusal_WasAcceptedByUpstreamCurl()
    {
        Assert.AreEqual("0", Utf8(RecordedFixture.ReadBytes("refused", "exitcode.txt")));
        Assert.IsEmpty(RecordedFixture.ReadBytes("refused", "stderr.txt"));
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => Server().WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken).AsTask());
    }
}
