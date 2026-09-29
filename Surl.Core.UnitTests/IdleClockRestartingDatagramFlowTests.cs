using System.Net;
using System.Text;

namespace Surl.Core;

[TestClass]
public sealed class IdleClockRestartingDatagramFlowTests
{
    private static readonly ConnectionLimits TenSecondsIdle = new(0, 0, TimeSpan.FromSeconds(10), TimeSpan.Zero);

    [TestMethod]
    public void EndPointsAndFirstDatagram_AreTheFlows()
    {
        var local = new IPEndPoint(IPAddress.IPv6Loopback, 69);
        var remote = new IPEndPoint(IPAddress.IPv6Loopback, 50001);
        using var deadlines = new ExchangeDeadlines(TenSecondsIdle, new ManualTimeProvider(), CancellationToken.None);
        var watched = new IdleClockRestartingDatagramFlow(new FakeDatagramFlow("rrq"u8.ToArray(), local, remote), deadlines);

        Assert.AreEqual(local, watched.LocalEndPoint);
        Assert.AreEqual(remote, watched.RemoteEndPoint);
        Assert.AreEqual("rrq", Encoding.ASCII.GetString(watched.FirstDatagram.Span));
    }

    [TestMethod]
    public async Task ReceiveAsync_RestartsTheIdleClock()
    {
        var time = new ManualTimeProvider();
        using var deadlines = new ExchangeDeadlines(TenSecondsIdle, time, CancellationToken.None);
        var flow = new FakeDatagramFlow([]);
        flow.Arrive("ack"u8.ToArray());
        var watched = new IdleClockRestartingDatagramFlow(flow, deadlines);
        time.Advance(TimeSpan.FromSeconds(9));

        await watched.ReceiveAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(9));

        Assert.IsNull(deadlines.Reason);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(ExchangeCancellation.IdleTimeout, deadlines.Reason);
    }

    [TestMethod]
    public async Task SendAsync_RestartsTheIdleClock()
    {
        var time = new ManualTimeProvider();
        using var deadlines = new ExchangeDeadlines(TenSecondsIdle, time, CancellationToken.None);
        var flow = new FakeDatagramFlow([]);
        var watched = new IdleClockRestartingDatagramFlow(flow, deadlines);
        time.Advance(TimeSpan.FromSeconds(9));

        await watched.SendAsync("data"u8.ToArray(), CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(9));

        Assert.IsNull(deadlines.Reason);
        Assert.AreEqual("data", Encoding.ASCII.GetString(flow.Sent.Single()));
    }

    [TestMethod]
    public async Task MoveToNewLocalPortAsync_AndDisposeAsync_ReachTheFlow()
    {
        var flow = new FakeDatagramFlow([]);
        using var deadlines = new ExchangeDeadlines(TenSecondsIdle, new ManualTimeProvider(), CancellationToken.None);
        var watched = new IdleClockRestartingDatagramFlow(flow, deadlines);

        await watched.MoveToNewLocalPortAsync(CancellationToken.None);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 40001), watched.LocalEndPoint);

        await watched.DisposeAsync();
        Assert.IsTrue(flow.Disposed);
    }
}
