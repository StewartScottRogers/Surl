using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

[TestClass]
public sealed class ExchangeDropBudgetGracefulTests
{
    [TestMethod]
    public async Task DropAsync_Graceful_DisposesTheLastJoinedFirst_AndNeverAborts()
    {
        var order = new List<string>();
        var drops = 0;
        var budget = new ExchangeDropBudget(2, () => drops++, graceful: true);
        budget.Join(new OrderRecordingConnection("control", order));
        budget.Join(new OrderRecordingConnection("data", order));

        await budget.DropAsync();

        CollectionAssert.AreEqual(new[] { "data disposed", "control disposed" }, order);
        Assert.AreEqual(1, drops);
    }

    [TestMethod]
    public async Task DropAsync_Reset_AbortsInJoinOrder_AndNeverDisposes()
    {
        var order = new List<string>();
        var budget = new ExchangeDropBudget(2, () => { });
        budget.Join(new OrderRecordingConnection("control", order));
        budget.Join(new OrderRecordingConnection("data", order));

        await budget.DropAsync();

        CollectionAssert.AreEqual(new[] { "control aborted", "data aborted" }, order);
    }

    private sealed class OrderRecordingConnection(string name, List<string> order) : IConnection
    {
        public EndPoint LocalEndPoint => new IPEndPoint(IPAddress.Loopback, 1);

        public EndPoint RemoteEndPoint => new IPEndPoint(IPAddress.Loopback, 2);

        public TlsSession? TlsSession => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken cancellationToken) => ValueTask.FromResult<byte?>(null);

        public ValueTask<PeerWatchOutcome> WatchForPeerCloseAsync(int maxBufferedBytes, CancellationToken cancellationToken) =>
            ValueTask.FromResult(PeerWatchOutcome.NotWatched);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public void Abort() => order.Add(name + " aborted");

        public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            order.Add(name + " disposed");
            return ValueTask.CompletedTask;
        }
    }
}
