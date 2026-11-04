using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// A hand-written <see cref="IConnection"/> that reads its request from an
/// <see cref="InMemoryConnection"/> and accepts only the first write: every later write
/// throws <see cref="IOException"/>, as on a peer that reset the connection after the head.
/// </summary>
internal sealed class FailingWriteConnection(byte[] request) : IConnection
{
    private readonly InMemoryConnection inner = new([request]);
    private bool firstWriteDone;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => null;

    public bool Aborted { get; private set; }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (firstWriteDone)
        {
            throw new IOException("The peer reset the connection.");
        }

        firstWriteDone = true;

        return ValueTask.CompletedTask;
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort() => Aborted = true;

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("This connection never upgrades.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
