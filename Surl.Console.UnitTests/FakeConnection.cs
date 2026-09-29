using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// A plaintext connection from <see cref="RemoteEndPoint"/> that delivers
/// <paramref name="request"/> in one read and then the end of the stream, keeps what the
/// server writes, and completes <see cref="Disposed"/> once the engine is done with it.
/// </summary>
/// <param name="request">The bytes the client sends before closing its side.</param>
internal sealed class FakeConnection(byte[] request) : IConnection
{
    private bool delivered;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, FakeListenerFactory.BoundPort);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession => null;

    /// <summary>Completes when the engine disposes the connection: its exchange has ended and been logged.</summary>
    public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (delivered)
        {
            return ValueTask.FromResult(0);
        }

        delivered = true;
        request.CopyTo(buffer);
        return ValueTask.FromResult(request.Length);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort()
    {
    }

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("The fake connection is plaintext only.");

    public ValueTask DisposeAsync()
    {
        Disposed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
