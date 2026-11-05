namespace Surl.Quic;

/// <summary>
/// The protocol a <see cref="QuicDatagramFlowServer"/> hands a connection to once its handshake
/// is complete: HTTP/3, or DNS over QUIC (ADR-0086 decision 4).
/// </summary>
public interface IQuicApplicationProtocol
{
    /// <summary>
    /// Serves one connection: accepts the client's streams, reads and writes them, and may close
    /// the connection with its own error code. Returning while the connection is still open closes
    /// it with application error code 0.
    /// </summary>
    /// <param name="connection">The connection, its handshake complete and its ALPN protocol agreed.</param>
    /// <param name="cancellationToken">Cancelled once the connection has ended, or the server stops.</param>
    /// <returns>A task that completes when the application is done with the connection.</returns>
    Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken);
}
