using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// An <see cref="InMemoryConnection"/> whose client stops reading after a number of writes:
/// every later write waits until it is cancelled, as a write to a full socket buffer does.
/// Once its inbound bytes are read, a read waits too, as a client that sends nothing more.
/// </summary>
internal sealed class WriteStallingConnection(IEnumerable<ReadOnlyMemory<byte>> inboundChunks, int writesBeforeStalling) : IConnection
{
    private readonly InMemoryConnection inner = new(inboundChunks, peerHalfClosesWhenExhausted: false);
    private readonly TaskCompletionSource writeStalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int writeCount;

    /// <summary>
    /// Completes when the first write starts to wait.
    /// </summary>
    public Task WriteStalled => writeStalled.Task;

    public byte[] WrittenBytes => inner.WrittenBytes;

    public bool WritesCompleted => inner.WritesCompleted;

    public bool Aborted => inner.Aborted;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        inner.ReadAsync(buffer, cancellationToken);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (++writeCount > writesBeforeStalling)
        {
            writeStalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        await inner.WriteAsync(bytes, cancellationToken);
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
