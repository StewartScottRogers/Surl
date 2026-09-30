namespace Surl.Protocol.Abstractions;

/// <summary>
/// Who may log in to a mail server, and how (ADR-0049, section 6): the SASL mechanisms SMTP's
/// <c>AUTH</c>, IMAP's <c>AUTHENTICATE</c> and POP3's <c>AUTH</c> offer and run, and POP3's
/// <c>APOP</c>. It sits beside <see cref="IAuthenticationPolicy"/>, which still judges IMAP's
/// <c>LOGIN</c> and POP3's <c>USER</c>/<c>PASS</c>; <c>Surl.Console</c> passes the same object to
/// the mail servers as both. The server owns the framing (base64, <c>=</c>, <c>*</c>, the line
/// limits and when a login is allowed); the policy owns every mechanism.
/// </summary>
public interface IMailAuthenticationPolicy
{
    /// <summary>
    /// What a mail server advertises on a connection in this TLS state (ADR-0049, section 2).
    /// </summary>
    /// <param name="tlsSession">
    /// The connection's <see cref="IConnection.TlsSession"/> as it stands: <see langword="null"/>
    /// means unencrypted.
    /// </param>
    /// <returns>The SASL mechanisms, and whether the clear-password login and <c>APOP</c> are offered.</returns>
    MailLoginOffer GetMailLoginOffer(TlsSession? tlsSession);

    /// <summary>
    /// Starts one <c>AUTH</c> (IMAP: <c>AUTHENTICATE</c>) exchange. It lives until the command
    /// ends, and holds a mechanism's state from one step to the next.
    /// </summary>
    /// <param name="start">The mechanism the client named, its initial response and the connection's encryption.</param>
    /// <returns>The exchange, whose <see cref="ISaslExchange.BeginAsync"/> the server calls first.</returns>
    ISaslExchange StartSaslExchange(SaslExchangeStart start);

    /// <summary>
    /// Judges a POP3 <c>APOP</c> login in one step, never a <see cref="MailLoginOutcome.Challenge"/>.
    /// </summary>
    /// <param name="login">The login as the client sent it, with the timestamp the greeting carried.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Whether the login is accepted, and if not, why.</returns>
    ValueTask<MailLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken);
}
