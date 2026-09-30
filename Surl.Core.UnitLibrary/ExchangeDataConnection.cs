using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// A data connection handed out by an exchange's <see cref="ExchangeDataConnections"/>
/// (ADR-0052, decision 9): passes every call through, and on its first dispose notes
/// <c>Data connection closed.</c> in the exchange's log and stops being tracked.
/// </summary>
/// <param name="connection">The data connection, already recording and restarting the idle clock.</param>
/// <param name="log">The exchange's log.</param>
/// <param name="owner">The exchange's data connections.</param>
internal sealed class ExchangeDataConnection(IConnection connection, IExchangeLog log, ExchangeDataConnections owner)
    : IConnection
{
    private int disposed;

    /// <inheritdoc/>
    public EndPoint LocalEndPoint => connection.LocalEndPoint;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

    /// <inheritdoc/>
    public TlsSession? TlsSession => connection.TlsSession;

    /// <inheritdoc/>
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        connection.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc/>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        connection.WriteAsync(bytes, cancellationToken);

    /// <inheritdoc/>
    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) =>
        connection.CompleteWritesAsync(cancellationToken);

    /// <inheritdoc/>
    public void Abort() => connection.Abort();

    /// <inheritdoc/>
    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        connection.UpgradeToTlsAsync(cancellationToken);

    /// <summary>
    /// Disposes the connection; the first call also notes that it closed.
    /// </summary>
    /// <returns>A task that completes once the connection is disposed.</returns>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            owner.Forget(this);
            log.Note("Data connection closed.");
        }

        return connection.DisposeAsync();
    }
}
