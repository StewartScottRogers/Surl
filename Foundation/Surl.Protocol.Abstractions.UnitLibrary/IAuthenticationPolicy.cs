namespace Surl.Protocol.Abstractions;

/// <summary>
/// Who may log in, and how, as a protocol server asks it (ADR-0032, section 6).
/// <c>Surl.Authentication</c> implements it and <c>Surl.Composition</c> passes that
/// implementation to each server that takes a policy, over the accounts allowed on the server's
/// scheme (ADR-0102, decision 4.3), so no protocol server references <c>Surl.Authentication</c>.
/// The HTTP and SOCKS proxies instead get one over the proxy accounts; with neither <c>-U</c> nor
/// <c>--proxy-user-file</c> they get <c>Surl.Composition</c>'s own
/// <c>NoAccountProxyAuthenticationPolicy</c>.
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

    /// <summary>
    /// Starts the authentication session for one HTTP connection made in <paramref name="exchange"/>,
    /// so the logins it judges name that exchange and its scheme (BL-871). The default ignores the
    /// exchange and starts the session as <see cref="StartHttpConnection(TlsSession?)"/> does.
    /// </summary>
    /// <param name="tlsSession">The connection's TLS session as it stands; <see langword="null"/> means unencrypted.</param>
    /// <param name="exchange">The connection's exchange, or <see langword="null"/> when unknown.</param>
    /// <returns>The session that judges every request on the connection.</returns>
    IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession, LoginExchange? exchange) =>
        StartHttpConnection(tlsSession);

    /// <summary>
    /// Whether <paramref name="name"/> is exactly the name of a named account in the account set,
    /// its password unused: how the Azure Storage server tells a path-style request's first
    /// segment, <c>/&lt;account&gt;/&lt;container&gt;/&lt;blob&gt;</c>, from a container (ADR-0109,
    /// decision 2). The empty name, a bearer token's account, never matches. A policy with no
    /// named accounts keeps this default, which matches nothing.
    /// </summary>
    /// <param name="name">The name, compared ordinally.</param>
    /// <returns><see langword="true"/> only when a named account has that name.</returns>
    bool NamesAccount(string name) => false;

    /// <summary>
    /// Judges a Kerberos login whose token the server has already checked (ADR-0165, decision 8).
    /// The default publishes nothing and returns <see cref="KerberosLogin.IsTokenAccepted"/>.
    /// </summary>
    /// <param name="login">The checked login.</param>
    /// <param name="cancellationToken">Cancels the judgement.</param>
    /// <returns><see langword="true"/> when the login stands; <see langword="false"/> when it is refused.</returns>
    ValueTask<bool> JudgeKerberosLoginAsync(KerberosLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);

        return ValueTask.FromResult(login.IsTokenAccepted);
    }
}
