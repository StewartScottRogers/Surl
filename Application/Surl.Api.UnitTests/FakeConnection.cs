using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A plaintext connection with no network behind it: the client writes <paramref name="clientBytes"/>
/// and then half-closes, and every byte the server writes is kept in <see cref="WrittenToClient"/>.
/// <see cref="Disposed"/> completes once the serving engine disposes it, after the exchange ends.
/// </summary>
/// <param name="clientBytes">Everything the client sends.</param>
internal sealed class FakeConnection(byte[] clientBytes) : IConnection
{
    private readonly MemoryStream writtenToClient = new();
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int readOffset;

    /// <summary>The client's endpoint.</summary>
    public static IPEndPoint Client { get; } = new(IPAddress.Loopback, 50123);

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, FakeListenerFactory.BoundPort);

    public EndPoint RemoteEndPoint => Client;

    public TlsSession? TlsSession => null;

    /// <summary>Completes once the connection has been disposed.</summary>
    public Task Disposed => disposed.Task;

    /// <summary>Every byte the server wrote, in order.</summary>
    public byte[] WrittenToClient
    {
        get
        {
            lock (writtenToClient)
            {
                return writtenToClient.ToArray();
            }
        }
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var count = Math.Min(buffer.Length, clientBytes.Length - readOffset);
        clientBytes.AsMemory(readOffset, count).CopyTo(buffer);
        readOffset += count;
        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        lock (writtenToClient)
        {
            writtenToClient.Write(bytes.Span);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort()
    {
    }

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("The fake connection carries no TLS.");

    public ValueTask DisposeAsync()
    {
        disposed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
