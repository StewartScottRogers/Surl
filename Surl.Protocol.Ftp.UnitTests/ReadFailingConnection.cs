using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// An <see cref="InMemoryConnection"/> whose client resets it once its inbound bytes are read:
/// the next read throws <see cref="IOException"/>, as a read from a reset socket does.
/// </summary>
internal sealed class ReadFailingConnection(IEnumerable<ReadOnlyMemory<byte>> inboundChunks) : IConnection
{
    private readonly InMemoryConnection inner = new(inboundChunks);

    public byte[] WrittenBytes => inner.WrittenBytes;

    public bool Aborted => inner.Aborted;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        return read > 0 ? read : throw new IOException("The connection was reset.");
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
