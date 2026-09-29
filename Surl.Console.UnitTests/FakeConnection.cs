using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// A connection from <see cref="RemoteEndPoint"/> that delivers <paramref name="request"/>, as
/// much as each read's buffer holds, and then the end of the stream, keeps what the server writes in
/// <see cref="WrittenBytes"/>, completes a TLS handshake when asked for one (an <c>https</c> or
/// <c>mqtts</c> listen URL's), and completes <see cref="Disposed"/> once the engine is done with it.
/// </summary>
/// <param name="request">The bytes the client sends before closing its side.</param>
internal sealed class FakeConnection(byte[] request) : IConnection
{
    private readonly MemoryStream written = new();
    private int delivered;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, FakeListenerFactory.BoundPort);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    /// <summary>Null until a TLS handshake completed: the connection is plaintext.</summary>
    public TlsSession? TlsSession { get; private set; }

    /// <summary>Every byte the server wrote, in order.</summary>
    public byte[] WrittenBytes
    {
        get
        {
            lock (written)
            {
                return written.ToArray();
            }
        }
    }

    /// <summary>Completes when the engine disposes the connection: its exchange has ended and been logged.</summary>
    public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var count = Math.Min(buffer.Length, request.Length - delivered);
        request.AsSpan(delivered, count).CopyTo(buffer.Span);
        delivered += count;
        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        lock (written)
        {
            written.Write(bytes.Span);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort()
    {
    }

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken)
    {
        TlsSession = InMemoryConnection.DefaultUpgradeTlsSession;
        return ValueTask.FromResult(TlsSession);
    }

    public ValueTask DisposeAsync()
    {
        Disposed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
