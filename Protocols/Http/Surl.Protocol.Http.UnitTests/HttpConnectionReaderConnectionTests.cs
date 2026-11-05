using System.Net;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// <see cref="HttpConnectionReaderConnection"/>: reads go through the HTTP/1.x reader, and
/// everything else goes to the connection itself (ADR-0091).
/// </summary>
[TestClass]
public sealed class HttpConnectionReaderConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void EndPointsAndTlsSession_AreTheConnections()
    {
        var local = new IPEndPoint(IPAddress.Loopback, 18050);
        var remote = new IPEndPoint(IPAddress.Loopback, 50001);
        var connection = new InMemoryConnection([], local, remote, initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        var readerConnection = Wrap(connection);

        Assert.AreSame(local, readerConnection.LocalEndPoint);
        Assert.AreSame(remote, readerConnection.RemoteEndPoint);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, readerConnection.TlsSession);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_UpgradesTheConnection()
    {
        var connection = new InMemoryConnection([]);

        var session = await Wrap(connection).UpgradeToTlsAsync(TestContext.CancellationToken);

        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, session);
        Assert.AreSame(session, connection.TlsSession);
    }

    [TestMethod]
    public async Task CompleteWritesAsync_CompletesTheConnectionsWrites()
    {
        var connection = new InMemoryConnection([]);

        await Wrap(connection).CompleteWritesAsync(TestContext.CancellationToken);

        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public void Abort_AbortsTheConnection()
    {
        var connection = new InMemoryConnection([]);

        Wrap(connection).Abort();

        Assert.IsTrue(connection.Aborted);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesTheConnection()
    {
        var connection = new InMemoryConnection([]);

        await Wrap(connection).DisposeAsync();

        Assert.IsTrue(connection.Disposed);
    }

    private static HttpConnectionReaderConnection Wrap(InMemoryConnection connection) =>
        new(connection, new HttpConnectionReader(connection, 8192));
}
