using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// An <see cref="InMemoryConnection"/> that runs an action just before it hands the server its
/// second chunk, so a test can change the store while the server is reading a request body.
/// </summary>
/// <param name="inner">The connection whose chunks are replayed.</param>
/// <param name="beforeSecondChunk">What runs before the second read.</param>
internal sealed class UnitTestActingConnection(InMemoryConnection inner, Action beforeSecondChunk) : IConnection
{
    private int reads;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (++reads == 2)
        {
            beforeSecondChunk();
        }

        return inner.ReadAsync(buffer, cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
