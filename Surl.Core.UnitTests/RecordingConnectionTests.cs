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
    public async Task UpgradeToTlsAsync_UpgradesTheConnection_AndReadsAfterItAreStillLogged()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([System.Text.Encoding.ASCII.GetBytes("EHLO x\r\n")]);
        var recording = new RecordingConnection(connection, log);
        Assert.IsNull(recording.TlsSession);

        var session = await recording.UpgradeToTlsAsync(CancellationToken.None);
        await recording.ReadAsync(new byte[16], CancellationToken.None);

        Assert.IsTrue(connection.UpgradeRequested);
        Assert.AreSame(session, recording.TlsSession);
        Assert.AreSame(connection.TlsSession, recording.TlsSession);
        Assert.HasCount(1, log.Entries);
    }

    [TestMethod]
    public void TlsSession_IsTheWrappedConnections()
    {
        var connection = new InMemoryConnection([], initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);
        var recording = new RecordingConnection(connection, new RecordingExchangeLog());

        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, recording.TlsSession);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_WritesAfterItAreStillLogged()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([]);
        var recording = new RecordingConnection(connection, log);

        await recording.UpgradeToTlsAsync(CancellationToken.None);
        await recording.WriteAsync("220 ready\r\n"u8.ToArray(), CancellationToken.None);

        Assert.HasCount(1, log.Entries);
        Assert.AreEqual(ExchangeLogEntryKind.BytesSent, log.Entries[0].Kind);
        CollectionAssert.AreEqual("220 ready\r\n"u8.ToArray(), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_FailedHandshake_ReachesTheCaller()
    {
        var recording = new RecordingConnection(new InMemoryConnection([], upgradeFails: true), new RecordingExchangeLog());

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() => recording.UpgradeToTlsAsync(CancellationToken.None).AsTask());
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
