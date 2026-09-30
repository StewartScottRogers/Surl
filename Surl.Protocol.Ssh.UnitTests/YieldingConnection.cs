using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// A connection whose client sends its bytes one chunk per read, each read yielding before it
/// completes, as a read from a socket whose bytes have not arrived yet does - so the server's
/// awaits on reads finish asynchronously rather than at once. Once the chunks are read, the
/// client has closed its side.
/// </summary>
internal sealed class YieldingConnection(IEnumerable<byte[]> inboundChunks) : IConnection
{
    private readonly Queue<byte[]> chunks = new(inboundChunks);
    private readonly InMemoryConnection writes = new([]);

    public byte[] WrittenBytes => writes.WrittenBytes;

    public EndPoint LocalEndPoint => writes.LocalEndPoint;

    public EndPoint RemoteEndPoint => writes.RemoteEndPoint;

    public TlsSession? TlsSession => writes.TlsSession;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await Task.Yield();
        if (!chunks.TryDequeue(out var chunk))
        {
            return 0;
        }

        var taken = Math.Min(chunk.Length, buffer.Length);
        chunk.AsSpan(0, taken).CopyTo(buffer.Span);
        if (taken < chunk.Length)
        {
            var rest = new Queue<byte[]>([chunk[taken..], .. chunks]);
            chunks.Clear();
            foreach (var remaining in rest)
            {
                chunks.Enqueue(remaining);
            }
        }

        return taken;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => writes.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => writes.CompleteWritesAsync(cancellationToken);

    public void Abort() => writes.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => writes.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => writes.DisposeAsync();
}
