namespace Surl.Protocol.Abstractions;

/// <summary>
/// Who may log in over SASL, on any protocol that carries it (ADR-0049, section 6, as ADR-0072
/// decision 4 amends it): the mechanisms a connection offers and one exchange per login. The mail
/// servers reach it through <see cref="IMailAuthenticationPolicy"/>, which adds their mail-only
/// members; the LDAP server uses it as is. The server owns the framing; the policy owns every
/// mechanism.
/// </summary>
public interface ISaslAuthenticationPolicy
{
    /// <summary>
    /// The SASL mechanisms a connection in this state offers, in ADR-0049 section 2's order, with
    /// <c>GSS-SPNEGO</c> after <c>GSSAPI</c> for LDAP (ADR-0072, decision 4).
    /// </summary>
    /// <param name="request">The connection's scheme and TLS state.</param>
    /// <returns>The mechanisms' registered names, upper case; empty when none is offered.</returns>
    IReadOnlyList<string> GetSaslMechanisms(SaslOfferRequest request);

    /// <summary>
    /// Starts one SASL exchange (SMTP's and POP3's <c>AUTH</c>, IMAP's <c>AUTHENTICATE</c>, an LDAP
    /// SASL bind). It lives until the login ends, and holds a mechanism's state from one step to the next.
    /// </summary>
    /// <param name="start">The mechanism the client named, its initial response and the connection's state.</param>
    /// <returns>The exchange, whose <see cref="ISaslExchange.BeginAsync"/> the server calls first.</returns>
    ISaslExchange StartSaslExchange(SaslExchangeStart start);
}
