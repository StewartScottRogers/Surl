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
    /// Judges a login made with a user name and clear password, with a token
    /// (<see cref="PasswordLogin.IsToken"/>), or with no credentials (ADR-0032, sections 5 and
    /// 6). Servers call it for logins their protocols carry in those forms, such as MQTT's
    /// <c>CONNECT</c>, FTP's <c>PASS</c>, LDAP's simple bind and the SOCKS5 user name and
    /// password. A server also calls it with no credentials to ask whether a client that has not
    /// logged in is let through (<c>--allow-anonymous</c>), as SMTP does before <c>MAIL</c>; the
    /// OCI registry calls it with an empty password over plain text to ask whether a Bearer token
    /// may arrive that way. SASL, SSH and SMB logins have their own contracts,
    /// <see cref="ISaslAuthenticationPolicy"/>, <see cref="ISshAuthenticationPolicy"/> and
    /// <see cref="ISmbAuthenticationPolicy"/>, and a request handed to an HTTP session is judged
    /// by <see cref="IHttpAuthenticationSession.JudgeAsync"/>.
    /// </summary>
    /// <param name="login">
    /// The credentials the client sent, if any, or those of the server's own question, with the
    /// connection's encryption.
    /// </param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Whether the login is accepted, and if not, why.</returns>
    ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(
        PasswordLogin login, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the authentication session for one HTTP or RTSP connection, which holds that
    /// connection's NTLM and Negotiate handshakes (ADR-0032, section 6). A server that has its
    /// requests judged by a session calls
    /// <see cref="StartHttpConnection(TlsSession?, LoginExchange?)"/>, whose default calls this
    /// member, at most once for each connection and before any request on it is judged: the HTTP
    /// server (over HTTP/3, once for each QUIC connection, not for each stream), the HTTP proxy
    /// and the RTSP and S3 servers, for example. A server keeps the session no longer than the
    /// connection. An implementation whose session holds no state, such as
    /// <see cref="AnonymousAuthenticationPolicy"/>, may return the same one for every connection.
    /// </summary>
    /// <param name="tlsSession">
    /// The connection's TLS session as it stands, <see langword="null"/> meaning unencrypted: its
    /// <see cref="IConnection.TlsSession"/>, or over QUIC one the server builds from the
    /// handshake.
    /// </param>
    /// <returns>The connection's session, which judges the requests the server hands it.</returns>
    IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession);

    /// <summary>
    /// Starts the authentication session for one HTTP or RTSP connection made in <paramref name="exchange"/>,
    /// so the logins it judges name that exchange and its scheme (BL-871). The default ignores the
    /// exchange and starts the session as <see cref="StartHttpConnection(TlsSession?)"/> does.
    /// </summary>
    /// <param name="tlsSession">The connection's TLS session as it stands; <see langword="null"/> means unencrypted.</param>
    /// <param name="exchange">The connection's exchange, or <see langword="null"/> when unknown.</param>
    /// <returns>The connection's session, which judges the requests the server hands it.</returns>
    IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession, LoginExchange? exchange) =>
        StartHttpConnection(tlsSession);

    /// <summary>
    /// Whether <paramref name="name"/> is exactly the name of a named account in the account set,
    /// its password unused. The Azure Storage server asks it to tell a path-style request's first
    /// segment, <c>/&lt;account&gt;/&lt;container&gt;/&lt;blob&gt;</c>, from a container (ADR-0109,
    /// decision 2). Whether a token issued to an account is still honoured is a different question,
    /// <see cref="HonoursTokenIssuedTo"/>'s, whose default answers with this member. The empty name,
    /// a bearer token's account, never matches. A policy with no named accounts keeps this default,
    /// which matches nothing.
    /// </summary>
    /// <param name="name">The name, compared ordinally.</param>
    /// <returns><see langword="true"/> only when a named account has that name.</returns>
    bool NamesAccount(string name) => false;

    /// <summary>
    /// Whether a token issued to <paramref name="accountName"/> is still honoured. It is the
    /// question for a server to ask when a token it issued to an account is used (ADR-0167,
    /// decision 4 and Amendment 3, which gives it to the OCI registry and MCP servers). The default
    /// is <see cref="NamesAccount"/>, so the token of a removed account is refused. A policy may
    /// also honour the token of a directory user, a name only an LDAP directory knows, as ADR-0167
    /// Amendment 3 decides. It judges no login: it publishes no event and asks no fault rule.
    /// </summary>
    /// <param name="accountName">The name the token was issued to, compared ordinally.</param>
    /// <returns><see langword="true"/> when the token is still honoured.</returns>
    bool HonoursTokenIssuedTo(string accountName)
    {
        ArgumentNullException.ThrowIfNull(accountName);

        return NamesAccount(accountName);
    }

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
