using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Wraps the connection a protocol server is handed and reports every byte it reads or
/// writes to the exchange log, so a server cannot forget to log one (ADR-0004, section 5).
/// </summary>
/// <param name="connection">The accepted connection.</param>
/// <param name="log">The exchange's log.</param>
internal sealed class RecordingConnection(IConnection connection, IExchangeLog log) : IConnection
{
    /// <inheritdoc/>
    public EndPoint LocalEndPoint => connection.LocalEndPoint;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

    /// <summary>
    /// Reads from the connection and reports the bytes read, if any, as received.
    /// </summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>How many bytes were read; 0 once the peer has half-closed.</returns>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var count = await connection.ReadAsync(buffer, cancellationToken);

        if (count > 0)
        {
            log.BytesReceived(buffer.Span[..count]);
        }

        return count;
    }

    /// <summary>
    /// Writes to the connection, then reports the bytes as sent.
    /// </summary>
    /// <param name="bytes">The bytes to send.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes once every byte has been handed to the transport.</returns>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(bytes, cancellationToken);
        log.BytesSent(bytes.Span);
    }

    /// <inheritdoc/>
    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) =>
        connection.CompleteWritesAsync(cancellationToken);

    /// <inheritdoc/>
    public void Abort() => connection.Abort();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
