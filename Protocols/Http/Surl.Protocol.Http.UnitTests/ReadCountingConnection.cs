using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// A hand-written <see cref="IConnection"/> that passes everything to an
/// <see cref="InMemoryConnection"/> and counts the bytes the server read before its first
/// write, so a test can tell what was read to answer a request from what the drain before the
/// close read afterwards.
/// </summary>
internal sealed class ReadCountingConnection(InMemoryConnection inner) : IConnection
{
    private long bytesRead;

    /// <summary>
    /// How many bytes the server had read when it first wrote; every byte it read when it never wrote.
    /// </summary>
    public long BytesReadBeforeFirstWrite => BytesReadAtFirstWrite ?? bytesRead;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    private long? BytesReadAtFirstWrite { get; set; }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        bytesRead += read;

        return read;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        BytesReadAtFirstWrite ??= bytesRead;

        return inner.WriteAsync(bytes, cancellationToken);
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
