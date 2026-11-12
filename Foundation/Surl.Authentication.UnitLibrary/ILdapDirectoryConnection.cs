namespace Surl.Authentication;

/// <summary>
/// One open connection to a directory (ADR-0149, decision 11); disposing it closes it.
/// </summary>
public interface ILdapDirectoryConnection : IAsyncDisposable
{
    /// <summary>
    /// The connection's stream, which carries TLS once <see cref="StartTlsAsync"/> has run.
    /// </summary>
    Stream Stream { get; }

    /// <summary>
    /// Runs the TLS client handshake on the open connection, after StartTLS's success, and replaces
    /// <see cref="Stream"/> with the encrypted one.
    /// </summary>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>A task that ends with the handshake.</returns>
    /// <exception cref="System.Security.Authentication.AuthenticationException">The handshake or the identity check failed.</exception>
    ValueTask StartTlsAsync(CancellationToken cancellationToken);
}
