using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// A connection whose client sends <c>request</c> and then stays silent, whose first write
/// (the <c>CONNACK</c>) completes, and whose later writes wait until they are cancelled, as a
/// client that stopped reading would make them.
/// </summary>
internal sealed class Mqtt5StalledAfterConnackConnection(byte[] request) : IConnection
{
    private int requestOffset;
    private readonly TaskCompletionSource connectAcknowledged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int writes;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 1883);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession => null;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (requestOffset >= request.Length)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        var count = Math.Min(buffer.Length, request.Length - requestOffset);
        request.AsMemory(requestOffset, count).CopyTo(buffer);
        requestOffset += count;
        return count;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref writes) > 1)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        connectAcknowledged.TrySetResult();
    }

    /// <summary>Completes once the <c>CONNACK</c> has been written.</summary>
    public Task ConnectAcknowledged => connectAcknowledged.Task;

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort()
    {
    }

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
