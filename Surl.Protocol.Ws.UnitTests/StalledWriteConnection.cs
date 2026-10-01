using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

/// <summary>
/// A hand-written <see cref="IConnection"/> whose client sends one request, half-closes, and
/// stops reading after <paramref name="writesBeforeStalling"/> writes: every later write waits
/// until it is cancelled, as on a peer whose receive window is full.
/// </summary>
internal sealed class StalledWriteConnection(byte[] request, int writesBeforeStalling = 0) : IConnection
{
    private bool requestRead;
    private int writesLeft = writesBeforeStalling;
    private int writesStarted;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 80);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession => null;

    public bool WritesCompleted { get; private set; }

    public bool Aborted { get; private set; }

    /// <summary>How many writes have been started, the stalled one included.</summary>
    public int WritesStarted => Volatile.Read(ref writesStarted);

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
        Interlocked.Increment(ref writesStarted);
        if (writesLeft-- > 0)
        {
            return;
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

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
