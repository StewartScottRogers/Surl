using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// A hand-written <see cref="IConnection"/> whose client sends one request and then stops
/// reading: every write waits until it is cancelled, as on a peer whose receive window is full.
/// </summary>
internal sealed class StalledWriteConnection(byte[] request) : IConnection
{
    private bool requestRead;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 80);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession => null;

    public bool WritesCompleted { get; private set; }

    public bool Aborted { get; private set; }

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

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken)
    {
        WritesCompleted = true;

        return ValueTask.CompletedTask;
    }

    public void Abort() => Aborted = true;

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("This connection never upgrades.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
