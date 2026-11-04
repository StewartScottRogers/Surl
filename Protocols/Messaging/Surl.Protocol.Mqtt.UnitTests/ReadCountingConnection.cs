using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// An <see cref="InMemoryConnection"/> that counts every byte the server has read from it, so
/// a test can prove how far into a packet the server read before it refused it.
/// </summary>
internal sealed class ReadCountingConnection(InMemoryConnection inner) : IConnection
{
    /// <summary>How many bytes the server's reads have returned.</summary>
    public int BytesRead { get; private set; }

    public InMemoryConnection Inner => inner;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        BytesRead += read;

        return read;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
