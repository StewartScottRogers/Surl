using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// An <see cref="InMemoryConnection"/> whose peer cannot be watched, so a command-stage delay holds
/// its full length: the client that stays (ADR-0141 amendment 2). Everything else is the inner connection's.
/// </summary>
internal sealed class UnwatchedInMemoryConnection(InMemoryConnection inner) : IConnection
{
    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    public ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken cancellationToken) => inner.PeekByteAsync(wait, cancellationToken);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
