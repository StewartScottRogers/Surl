using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Dict;

/// <summary>
/// An <see cref="InMemoryConnection"/> on which one write, counted from 1, fails with a chosen
/// <see cref="IOException"/>. Every other write is handed to the
/// <see cref="InMemoryConnection"/>, so what a server writes after the failure shows in
/// <see cref="WrittenBytes"/> only until it calls <see cref="Abort"/>; from then on the
/// <see cref="InMemoryConnection"/> refuses writes and records nothing more.
/// </summary>
internal sealed class OneWriteFailingConnection(
    IEnumerable<ReadOnlyMemory<byte>> inboundChunks,
    int failingWriteNumber,
    IOException failure) : IConnection
{
    private readonly InMemoryConnection inner = new(inboundChunks);
    private int writeCount;

    public byte[] WrittenBytes => inner.WrittenBytes;

    public bool Aborted => inner.Aborted;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        inner.ReadAsync(buffer, cancellationToken);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        ++writeCount == failingWriteNumber
            ? ValueTask.FromException(failure)
            : inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
