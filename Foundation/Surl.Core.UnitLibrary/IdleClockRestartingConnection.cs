using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Wraps the connection a protocol server is handed and restarts the exchange's idle clock
/// each time a read returns bytes or a write completes, so the exchange is idle only while no
/// byte moves (ADR-0006, section 1). A write that makes no progress therefore counts as idle.
/// </summary>
/// <param name="connection">The connection to wrap.</param>
/// <param name="deadlines">The exchange's clocks.</param>
internal sealed class IdleClockRestartingConnection(IConnection connection, ExchangeDeadlines deadlines) : IConnection
{
    /// <inheritdoc/>
    public EndPoint LocalEndPoint => connection.LocalEndPoint;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

    /// <inheritdoc/>
    public TlsSession? TlsSession => connection.TlsSession;

    /// <summary>
    /// Reads from the connection and restarts the idle clock when any byte arrived.
    /// </summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>How many bytes were read; 0 once the peer has half-closed.</returns>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var count = await connection.ReadAsync(buffer, cancellationToken);

        if (count > 0)
        {
            deadlines.RestartIdleClock();
        }

        return count;
    }

    /// <summary>
    /// Peeks at the client's next byte on the wrapped connection and leaves the idle clock
    /// alone: the peek only observes the byte, and the read that later consumes it restarts the
    /// clock, so a peek and its read count as one piece of client activity.
    /// </summary>
    /// <param name="wait">The longest to wait; <see cref="TimeSpan.Zero"/> only looks at what has already arrived.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The next byte, or <see langword="null"/> when none arrived within <paramref name="wait"/>.</returns>
    public ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken cancellationToken) =>
        connection.PeekByteAsync(wait, cancellationToken);

    /// <summary>
    /// Writes to the connection, then restarts the idle clock.
    /// </summary>
    /// <param name="bytes">The bytes to send.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes once every byte has been handed to the transport.</returns>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(bytes, cancellationToken);
        deadlines.RestartIdleClock();
    }

    /// <inheritdoc/>
    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) =>
        connection.CompleteWritesAsync(cancellationToken);

    /// <inheritdoc/>
    public void Abort() => connection.Abort();

    /// <inheritdoc/>
    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        connection.UpgradeToTlsAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
