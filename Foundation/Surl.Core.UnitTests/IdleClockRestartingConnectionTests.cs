using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

[TestClass]
public sealed class IdleClockRestartingConnectionTests
{
    private static readonly ConnectionLimits TenSecondsIdle = new(0, 0, TimeSpan.FromSeconds(10), TimeSpan.Zero);

    [TestMethod]
    public void EndPoints_AreTheConnections()
    {
        var local = new IPEndPoint(IPAddress.IPv6Loopback, 8080);
        var remote = new IPEndPoint(IPAddress.IPv6Loopback, 50001);
        using var deadlines = new ExchangeDeadlines(TenSecondsIdle, new ManualTimeProvider(), CancellationToken.None);
        var watched = new IdleClockRestartingConnection(new InMemoryConnection([], local, remote), deadlines);

        Assert.AreEqual(local, watched.LocalEndPoint);
        Assert.AreEqual(remote, watched.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReadAsync_AtEndOfStream_LeavesTheIdleClockRunning()
    {
        var time = new ManualTimeProvider();
        using var deadlines = new ExchangeDeadlines(TenSecondsIdle, time, CancellationToken.None);
        var watched = new IdleClockRestartingConnection(new InMemoryConnection([]), deadlines);
        time.Advance(TimeSpan.FromSeconds(5));

        var count = await watched.ReadAsync(new byte[4], CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.AreEqual(0, count);
        Assert.AreEqual(ExchangeCancellation.IdleTimeout, deadlines.Reason);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_UpgradesTheConnection()
    {
        var connection = new InMemoryConnection([]);
        using var deadlines = new ExchangeDeadlines(TenSecondsIdle, new ManualTimeProvider(), CancellationToken.None);
        var watched = new IdleClockRestartingConnection(connection, deadlines);
        Assert.IsNull(watched.TlsSession);

        var session = await watched.UpgradeToTlsAsync(CancellationToken.None);

        Assert.IsTrue(connection.UpgradeRequested);
        Assert.AreSame(session, watched.TlsSession);
    }

    [TestMethod]
    public async Task CompleteWritesAsync_Abort_AndDisposeAsync_ReachTheConnection()
    {
        var connection = new InMemoryConnection([]);
        using var deadlines = new ExchangeDeadlines(TenSecondsIdle, new ManualTimeProvider(), CancellationToken.None);
        var watched = new IdleClockRestartingConnection(connection, deadlines);

        await watched.CompleteWritesAsync(CancellationToken.None);
        Assert.IsTrue(connection.WritesCompleted);

        watched.Abort();
        Assert.IsTrue(connection.Aborted);

        await watched.DisposeAsync();
        Assert.IsTrue(connection.Disposed);
    }
}
