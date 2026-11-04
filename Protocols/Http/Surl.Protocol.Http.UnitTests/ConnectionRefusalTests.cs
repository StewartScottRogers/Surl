using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class ConnectionRefusalTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_WritesTheRecorded503AndCompletesWritesWithoutAbort(ConnectionRefusal refusal)
    {
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        await Server().WriteRefusalAsync(connection, refusal, TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedResponse("refusal-503"), connection.WrittenBytes);
        Assert.AreEqual("HTTP/1.1 503 Service Unavailable\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await Server().WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken));
    }

    [TestMethod]
    public void Server_IsAConnectionRefusalWriter()
    {
        Assert.IsInstanceOfType<IConnectionRefusalWriter>(Server());
    }
}
