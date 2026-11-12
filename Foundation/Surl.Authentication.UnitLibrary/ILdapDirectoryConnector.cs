namespace Surl.Authentication;

/// <summary>
/// The seam through which <see cref="LdapAccountSource"/> reaches a directory, so no socket is
/// constructed in this library and tests replay a directory in memory (ADR-0149, decision 11).
/// </summary>
public interface ILdapDirectoryConnector
{
    /// <summary>
    /// Opens a TCP connection to <paramref name="endpoint"/>, with TLS at once when
    /// <see cref="LdapDirectoryEndpoint.UseTls"/>.
    /// </summary>
    /// <param name="endpoint">Where to connect.</param>
    /// <param name="cancellationToken">Cancels the connection.</param>
    /// <returns>The open connection, which the caller disposes.</returns>
    /// <exception cref="IOException">The directory could not be reached.</exception>
    /// <exception cref="System.Security.Authentication.AuthenticationException">The TLS handshake or the directory's identity check failed.</exception>
    ValueTask<ILdapDirectoryConnection> ConnectAsync(LdapDirectoryEndpoint endpoint, CancellationToken cancellationToken);
}
