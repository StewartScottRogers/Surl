namespace Surl.Authentication;

/// <summary>
/// SASL <c>XOAUTH2</c>, Google's format (ADR-0049, section 5): the response
/// <c>user=&lt;name&gt;\x01auth=Bearer &lt;token&gt;\x01\x01</c>. The <c>user</c> pair must be
/// there; its value is not matched, but names the owner an accepted login acts as (ADR-0050,
/// decision 2).
/// </summary>
internal sealed class XOAuth2SaslExchange(SaslExchangeContext context) : BearerTokenSaslExchange(context)
{
    /// <inheritdoc/>
    protected override BearerLogin? ReadLogin(string message) =>
        ReadPairs(message) is { } pairs && pairs.GetValueOrDefault("user") is { } user && ReadBearerToken(pairs) is { } token
            ? new BearerLogin(user, token)
            : null;
}
