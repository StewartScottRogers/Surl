namespace Surl.Authentication;

/// <summary>
/// SASL <c>XOAUTH2</c>, Google's format (ADR-0049, section 5): the response
/// <c>user=&lt;name&gt;\x01auth=Bearer &lt;token&gt;\x01\x01</c>. The <c>user</c> pair must be
/// there; its value is not matched.
/// </summary>
internal sealed class XOAuth2SaslExchange(SaslExchangeContext context) : BearerTokenSaslExchange(context)
{
    /// <inheritdoc/>
    protected override string? ReadToken(string message) =>
        ReadPairs(message) is { } pairs && pairs.ContainsKey("user") ? ReadBearerToken(pairs) : null;
}
