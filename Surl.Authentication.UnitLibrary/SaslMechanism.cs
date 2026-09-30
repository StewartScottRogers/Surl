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
        new("DIGEST-MD5", AuthenticationMethod.DigestMd5, context => new DigestMd5SaslExchange(context)),
        new("CRAM-MD5", AuthenticationMethod.CramMd5, context => new CramMd5SaslExchange(context)),
        new("OAUTHBEARER", AuthenticationMethod.OAuthBearer, context => new OAuthBearerSaslExchange(context)),
        new("XOAUTH2", AuthenticationMethod.XOAuth2, context => new XOAuth2SaslExchange(context)),
        new("PLAIN", AuthenticationMethod.Plain, context => new PlainSaslExchange(context)),
        new("LOGIN", AuthenticationMethod.Login, context => new LoginSaslExchange(context)),
    ];
}
