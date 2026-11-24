using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// An <see cref="InMemoryConnection"/> whose every write yields to the scheduler before it is
/// recorded, so the server's awaited write completes asynchronously, as a socket write that
/// cannot finish at once does.
/// </summary>
internal sealed class YieldingWriteConnection(IEnumerable<ReadOnlyMemory<byte>> inboundChunks) : IConnection
{
    private readonly InMemoryConnection inner = new(inboundChunks);

    public byte[] WrittenBytes => inner.WrittenBytes;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        inner.ReadAsync(buffer, cancellationToken);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await Task.Yield();
        await inner.WriteAsync(bytes, cancellationToken);
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
