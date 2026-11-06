using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// An <see cref="InMemoryConnection"/> replaying a script whose TLS handshake never finishes: an
/// <see cref="UpgradeToTlsAsync"/> waits until it is cancelled.
/// </summary>
internal sealed class UnitTestHangingHandshakeConnection(IEnumerable<ReadOnlyMemory<byte>> chunks) : IConnection
{
    private readonly InMemoryConnection inner = new(chunks, new IPEndPoint(IPAddress.Loopback, 5222), peerHalfClosesWhenExhausted: false);

    public bool UpgradeRequested { get; private set; }

    public byte[] WrittenBytes => inner.WrittenBytes;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public async ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken)
    {
        UpgradeRequested = true;
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("An infinite delay ended without being cancelled.");
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
