using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// An <see cref="InMemoryConnection"/> that runs an action once, right after the first write
/// holding a given text - so another session can change the store between two of this
/// session's commands, as it would between the client's reads.
/// </summary>
internal sealed class ActAfterWriteConnection(IEnumerable<ReadOnlyMemory<byte>> inboundChunks, string trigger, Action action) : IConnection
{
    private readonly InMemoryConnection inner = new(inboundChunks);
    private bool acted;

    public byte[] WrittenBytes => inner.WrittenBytes;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        inner.ReadAsync(buffer, cancellationToken);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await inner.WriteAsync(bytes, cancellationToken);
        if (!acted && Encoding.UTF8.GetString(bytes.Span).Contains(trigger, StringComparison.Ordinal))
        {
            acted = true;
            action();
        }
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
