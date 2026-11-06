using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// A connection whose client sends <c>request</c> and then half-closes, and whose every write
/// waits until it is cancelled, as a client that never reads would make it. It opens nothing.
/// </summary>
internal sealed class Mqtt5StalledWriteConnection(byte[] request) : IConnection
{
    private bool requestRead;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 8083);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession => null;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (requestRead)
        {
            return ValueTask.FromResult(0);
        }

        requestRead = true;
        request.CopyTo(buffer);
        return ValueTask.FromResult(request.Length);
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
