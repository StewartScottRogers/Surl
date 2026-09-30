namespace Surl.Protocol.Abstractions;

/// <summary>
/// Who may log in, and how, as a protocol server asks it (ADR-0032, section 6).
/// <c>Surl.Authentication</c> implements it and <c>Surl.Console</c> passes that implementation
/// to each server, so no protocol server references <c>Surl.Authentication</c>.
/// </summary>
public interface IAuthenticationPolicy
{
    /// <summary>
    /// Judges a login by user name and clear password: MQTT's <c>CONNECT</c> now; FTP, IMAP,
    /// POP3, SMTP and LDAP later (ADR-0032, sections 5 and 6).
    /// </summary>
    /// <param name="login">The login as the client sent it, with the connection's encryption.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Whether the login is accepted, and if not, why.</returns>
    ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(
        PasswordLogin login, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the authentication session for one HTTP connection, which holds that connection's
    /// NTLM and Negotiate handshakes (ADR-0032, section 6). The HTTP server calls it once per
    /// connection, before its first request, and the session dies with the connection.
    /// </summary>
    /// <param name="tlsSession">
    /// The connection's <see cref="IConnection.TlsSession"/> as it stands: <see langword="null"/>
    /// means unencrypted.
    /// </param>
    /// <returns>The session that judges every request on the connection.</returns>
    IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession);
}
