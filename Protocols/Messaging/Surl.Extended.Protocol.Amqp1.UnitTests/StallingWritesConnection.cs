using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// An <see cref="InMemoryConnection"/> whose writes, once <paramref name="stallAfter"/> is cancelled,
/// never finish until their own token is cancelled: a client that stopped reading.
/// </summary>
internal sealed class StallingWritesConnection(InMemoryConnection inner, CancellationToken stallAfter) : IConnection
{
    /// <summary>Whether a write has begun to stall.</summary>
    public bool IsStalled { get; private set; }

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (stallAfter.IsCancellationRequested)
        {
            IsStalled = true;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        await inner.WriteAsync(bytes, cancellationToken);
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
