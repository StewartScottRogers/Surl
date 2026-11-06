using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// A connection whose client sends <c>request</c> and then half-closes, and whose every write
/// waits until it is cancelled, as a client that never reads would make it. It opens nothing.
/// </summary>
internal sealed class Mqtt5StalledWriteConnection(byte[] request) : IConnection
{
    private int requestOffset;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 8083);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession => null;

    // Hands the request over in as many reads as the reader's buffers need.
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var count = Math.Min(buffer.Length, request.Length - requestOffset);
        request.AsMemory(requestOffset, count).CopyTo(buffer);
        requestOffset += count;
        return ValueTask.FromResult(count);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort()
    {
    }

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
