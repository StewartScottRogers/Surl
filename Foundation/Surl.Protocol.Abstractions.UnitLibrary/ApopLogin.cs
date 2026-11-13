namespace Surl.Protocol.Abstractions;

/// <summary>
/// A POP3 <c>APOP</c> login (RFC 1939), as the server hands it to
/// <see cref="IMailAuthenticationPolicy.CheckApopLoginAsync"/> (ADR-0049, section 6).
/// </summary>
/// <param name="Scheme">The listen URL's scheme, for the log.</param>
/// <param name="UserName">The user name as sent.</param>
/// <param name="Timestamp">The timestamp this connection's greeting carried, angle brackets included.</param>
/// <param name="Digest">The digest as sent.</param>
/// <param name="TlsSession">
/// The connection's <see cref="IConnection.TlsSession"/> now: <see langword="null"/> means unencrypted.
/// </param>
/// <param name="Exchange">The exchange the login was made in, or <see langword="null"/> when unknown.</param>
public sealed record ApopLogin(
    string Scheme,
    string UserName,
    string Timestamp,
    string Digest,
    TlsSession? TlsSession,
    LoginExchange? Exchange = null);
