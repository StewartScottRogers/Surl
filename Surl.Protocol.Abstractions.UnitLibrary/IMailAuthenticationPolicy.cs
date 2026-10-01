namespace Surl.Protocol.Abstractions;

/// <summary>
/// Who may log in to a mail server, and how (ADR-0049, section 6, as ADR-0072 decision 4 amends
/// it): the <see cref="ISaslAuthenticationPolicy"/> SMTP's <c>AUTH</c>, IMAP's <c>AUTHENTICATE</c>
/// and POP3's <c>AUTH</c> run, with the mail-only offer and POP3's <c>APOP</c>. It sits beside
/// <see cref="IAuthenticationPolicy"/>, which still judges IMAP's <c>LOGIN</c> and POP3's
/// <c>USER</c>/<c>PASS</c>; <c>Surl.Console</c> passes the same object to the mail servers as both.
/// The server owns the framing (base64, <c>=</c>, <c>*</c>, the line limits and when a login is
/// allowed); the policy owns every mechanism.
/// </summary>
public interface IMailAuthenticationPolicy : ISaslAuthenticationPolicy
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
    /// Judges a POP3 <c>APOP</c> login in one step, never a <see cref="SaslLoginOutcome.Challenge"/>.
    /// </summary>
    /// <param name="login">The login as the client sent it, with the timestamp the greeting carried.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Whether the login is accepted, and if not, why.</returns>
    ValueTask<SaslLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken);
}
