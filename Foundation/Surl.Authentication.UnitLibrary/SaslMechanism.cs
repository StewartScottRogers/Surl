using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// One SASL mechanism the mail servers can check: its registered name, the <c>--auth</c> method
/// that accepts it, and how an exchange of it starts (ADR-0049, sections 1 and 5).
/// </summary>
/// <param name="Name">The registered name, upper case, as offered and as the login note's method.</param>
/// <param name="Method">The method <c>--auth</c> accepts it by.</param>
/// <param name="Start">Starts one exchange.</param>
internal sealed record SaslMechanism(
    string Name,
    AuthenticationMethod Method,
    Func<SaslExchangeContext, ISaslExchange> Start)
{
    /// <summary>
    /// Every mechanism this build checks, in ADR-0049 section 2's offer order.
    /// </summary>
    public static IReadOnlyList<SaslMechanism> InOfferOrder { get; } =
    [
        new("GSSAPI", AuthenticationMethod.Gssapi, context => new GssapiSaslExchange(context)),
        new("DIGEST-MD5", AuthenticationMethod.DigestMd5, context => new DigestMd5SaslExchange(context)),
        new("CRAM-MD5", AuthenticationMethod.CramMd5, context => new CramMd5SaslExchange(context)),
        new("NTLM", AuthenticationMethod.Ntlm, context => new NtlmSaslExchange(context)),
        new("OAUTHBEARER", AuthenticationMethod.OAuthBearer, context => new OAuthBearerSaslExchange(context)),
        new("XOAUTH2", AuthenticationMethod.XOAuth2, context => new XOAuth2SaslExchange(context)),
        new("PLAIN", AuthenticationMethod.Plain, context => new PlainSaslExchange(context)),
        new("LOGIN", AuthenticationMethod.Login, context => new LoginSaslExchange(context)),
        new("EXTERNAL", AuthenticationMethod.External, context => new ExternalSaslExchange(context)),
    ];

    /// <summary>
    /// LDAP's <c>GSS-SPNEGO</c>, accepted by <c>negotiate</c> and run only where the server carries
    /// a security layer: a SPNEGO token selecting Kerberos is checked against <c>--keytab</c>, and
    /// a bare NTLM message, as <c>WinLDAP</c> sends it for an address, is answered as <c>NTLM</c>
    /// is (ADR-0072, decision 4, and its Amendment 1). It is not in <see cref="InOfferOrder"/>,
    /// since the mail servers never offer it.
    /// </summary>
    public static SaslMechanism GssSpnego { get; } =
        new("GSS-SPNEGO", AuthenticationMethod.Negotiate, context => new GssSpnegoSaslExchange(context));
}
