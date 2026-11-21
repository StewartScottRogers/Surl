using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// An <see cref="IConnection"/> that writes what it is given one byte at a time, waiting
/// <see cref="ByteInterval"/> on the injected <see cref="TimeProvider"/> after each byte: upstream's
/// <c>SLOWDOWN</c> server command, "0.01 sec delay between each byte" (FILEFORMAT.md at tag
/// <c>curl-8_21_0</c>, BL-1137). Everything else goes straight to the connection it wraps.
/// </summary>
public sealed class BytePacedConnection : IConnection
{
    /// <summary>The wait after each byte written: 10 milliseconds.</summary>
    public static readonly TimeSpan ByteInterval = TimeSpan.FromMilliseconds(10);

    private readonly IConnection inner;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Wraps <paramref name="inner"/>.
    /// </summary>
    /// <param name="inner">The connection whose writes are paced.</param>
    /// <param name="timeProvider">The clock each wait runs on.</param>
    public BytePacedConnection(IConnection inner, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.inner = inner;
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    /// <inheritdoc />
    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    /// <inheritdoc />
    public TlsSession? TlsSession => inner.TlsSession;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken cancellationToken) =>
        inner.PeekByteAsync(wait, cancellationToken);

    /// <inheritdoc />
    public ValueTask<PeerWatchOutcome> WatchForPeerCloseAsync(int maxBufferedBytes, CancellationToken cancellationToken) =>
        inner.WatchForPeerCloseAsync(maxBufferedBytes, cancellationToken);

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        for (var index = 0; index < bytes.Length; index++)
        {
            await inner.WriteAsync(bytes.Slice(index, 1), cancellationToken);
            await Task.Delay(ByteInterval, timeProvider, cancellationToken);
        }
    }

    /// <inheritdoc />
    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) =>
        inner.CompleteWritesAsync(cancellationToken);

    /// <inheritdoc />
    public void Abort() => inner.Abort();

    /// <inheritdoc />
    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        inner.UpgradeToTlsAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
