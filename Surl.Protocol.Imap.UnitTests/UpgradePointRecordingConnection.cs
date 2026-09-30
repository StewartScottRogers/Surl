using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// An <see cref="InMemoryConnection"/> that remembers how many bytes the server had written and
/// the client had sent when the server upgraded to TLS, and refuses to hand out a byte of a
/// chunk past <c>plaintextChunkCount</c> before the upgrade, as a real TLS client sends nothing
/// in plaintext after <c>STARTTLS</c>.
/// </summary>
internal sealed class UpgradePointRecordingConnection(IEnumerable<ReadOnlyMemory<byte>> inboundChunks, int plaintextChunkCount) : IConnection
{
    private readonly InMemoryConnection inner = new(inboundChunks);
    private int readCount;

    /// <summary>
    /// How many bytes were written before the upgrade, or <see langword="null"/> without one.
    /// </summary>
    public int? WrittenBytesAtUpgrade { get; private set; }

    public byte[] WrittenBytes => inner.WrittenBytes;

    public bool WritesCompleted => inner.WritesCompleted;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (++readCount > plaintextChunkCount && WrittenBytesAtUpgrade is null)
        {
            throw new InvalidOperationException("The server read past STARTTLS before upgrading.");
        }

        return inner.ReadAsync(buffer, cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken)
    {
        WrittenBytesAtUpgrade = inner.WrittenBytes.Length;
        return inner.UpgradeToTlsAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
