using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

[TestClass]
public sealed class RecordingConnectionTests
{
    [TestMethod]
    public void EndPoints_AreTheConnections()
    {
        var local = new IPEndPoint(IPAddress.IPv6Loopback, 8080);
        var remote = new IPEndPoint(IPAddress.IPv6Loopback, 50001);
        var recording = new RecordingConnection(new InMemoryConnection([], local, remote), new RecordingExchangeLog());

        Assert.AreEqual(local, recording.LocalEndPoint);
        Assert.AreEqual(remote, recording.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReadAsync_AtEndOfStream_LogsNothing()
    {
        var log = new RecordingExchangeLog();
        var recording = new RecordingConnection(new InMemoryConnection([]), log);

        var count = await recording.ReadAsync(new byte[4], CancellationToken.None);

        Assert.AreEqual(0, count);
        Assert.IsEmpty(log.Entries);
    }

    [TestMethod]
    public async Task CompleteWritesAsync_Abort_AndDisposeAsync_ReachTheConnection()
    {
        var connection = new InMemoryConnection([]);
        var recording = new RecordingConnection(connection, new RecordingExchangeLog());

        await recording.CompleteWritesAsync(CancellationToken.None);
        Assert.IsTrue(connection.WritesCompleted);

        recording.Abort();
        Assert.IsTrue(connection.Aborted);

        await recording.DisposeAsync();
        Assert.IsTrue(connection.Disposed);
    }
}
