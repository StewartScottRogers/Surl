using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// An <see cref="IConnection"/> over an <see cref="InMemoryConnection"/> that fails as a
/// network does: a write past <c>writesBeforeFailure</c> throws <see cref="IOException"/>, and
/// with <c>resetsInsteadOfClosing</c> the peer's close is reported as a reset instead of a
/// zero-byte read.
/// </summary>
internal sealed class FailingConnection(InMemoryConnection inner, int writesBeforeFailure = int.MaxValue, bool resetsInsteadOfClosing = false) : IConnection
{
    private int writes;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);

        return read == 0 && resetsInsteadOfClosing ? throw new IOException("The connection was reset.") : read;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        ++writes > writesBeforeFailure ? throw new IOException("The connection was reset.") : inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
