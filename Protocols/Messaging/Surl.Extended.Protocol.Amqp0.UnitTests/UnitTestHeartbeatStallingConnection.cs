using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// A connection over an <see cref="InMemoryConnection"/> whose write of a heartbeat frame never
/// completes until it is cancelled, and then completes asynchronously - a heartbeat stuck in a
/// full send buffer when the connection ends.
/// </summary>
internal sealed class UnitTestHeartbeatStallingConnection(InMemoryConnection inner) : IConnection
{
    private volatile bool heartbeatStalled;
    private volatile bool heartbeatCancelled;

    public bool HeartbeatStalled => heartbeatStalled;

    public bool HeartbeatCancelled => heartbeatCancelled;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (!bytes.Span.SequenceEqual(Amqp0TestBytes.Heartbeat))
        {
            await inner.WriteAsync(bytes, cancellationToken);
            return;
        }

        var stalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = cancellationToken.Register(() => stalled.TrySetCanceled(cancellationToken));
        heartbeatStalled = true;
        try
        {
            await stalled.Task;
        }
        finally
        {
            heartbeatCancelled = true;
        }
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
