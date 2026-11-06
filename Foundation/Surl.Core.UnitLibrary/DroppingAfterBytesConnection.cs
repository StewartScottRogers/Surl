using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Wraps a connection under a <see cref="FaultKind.Drop"/> rule with
/// <see cref="FaultRule.AfterBytes"/> (ADR-0124, decision 4): the server's writes pass through
/// until it has sent that many bytes, the write that reaches the count is cut at it, and the
/// connection is then aborted. Every read or write after the drop throws <see cref="IOException"/>,
/// as on a connection the peer reset.
/// </summary>
/// <param name="connection">The connection to wrap.</param>
/// <param name="afterBytes">How many bytes the server may send before the drop; 1 or more.</param>
/// <param name="onDropped">Called once, when the connection is dropped.</param>
internal sealed class DroppingAfterBytesConnection(IConnection connection, long afterBytes, Action onDropped) : IConnection
{
    private long sentByteCount;
    private bool dropped;

    /// <inheritdoc/>
    public EndPoint LocalEndPoint => connection.LocalEndPoint;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

    /// <inheritdoc/>
    public TlsSession? TlsSession => connection.TlsSession;

    /// <summary>
    /// Reads from the connection, unless it was dropped.
    /// </summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>How many bytes were read; 0 once the peer has half-closed.</returns>
    /// <exception cref="IOException">The connection was dropped.</exception>
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ThrowIfDropped();

        return connection.ReadAsync(buffer, cancellationToken);
    }

    /// <summary>
    /// Peeks at the client's next byte, unless the connection was dropped.
    /// </summary>
    /// <param name="wait">The longest to wait.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The next byte, or <see langword="null"/> when none arrived within <paramref name="wait"/>.</returns>
    /// <exception cref="IOException">The connection was dropped.</exception>
    public ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        ThrowIfDropped();

        return connection.PeekByteAsync(wait, cancellationToken);
    }

    /// <summary>
    /// Writes as much of <paramref name="bytes"/> as the count allows, and drops the connection
    /// once the count is reached.
    /// </summary>
    /// <param name="bytes">The bytes to send.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes once the bytes allowed have been handed to the transport.</returns>
    /// <exception cref="IOException">
    /// The connection was dropped before this write, or during it with bytes left unsent.
    /// </exception>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        ThrowIfDropped();

        var allowed = (int)Math.Min(bytes.Length, afterBytes - sentByteCount);
        await connection.WriteAsync(bytes[..allowed], cancellationToken);
        sentByteCount += allowed;

        if (sentByteCount == afterBytes)
        {
            dropped = true;
            connection.Abort();
            onDropped();
        }

        if (allowed < bytes.Length)
        {
            throw DroppedException();
        }
    }

    /// <inheritdoc/>
    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken)
    {
        ThrowIfDropped();

        return connection.CompleteWritesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public void Abort() => connection.Abort();

    /// <inheritdoc/>
    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        connection.UpgradeToTlsAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => connection.DisposeAsync();

    private void ThrowIfDropped()
    {
        if (dropped)
        {
            throw DroppedException();
        }
    }

    private IOException DroppedException() =>
        new($"The connection was dropped by a fault rule after {afterBytes} bytes sent.");
}
