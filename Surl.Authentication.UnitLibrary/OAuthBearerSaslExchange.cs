namespace Surl.Authentication;

/// <summary>
/// SASL <c>OAUTHBEARER</c>, RFC 7628 (ADR-0049, section 5): the response is the GS2 header
/// <c>n,[a=&lt;authzid&gt;],</c> (or <c>y,</c>; RFC 7628 section 3.1 allows no channel binding),
/// <c>\x01</c>, then <c>key=value\x01</c> pairs and a final <c>\x01</c>. The <c>a=</c> is not
/// matched, since a token account has no name.
/// </summary>
internal sealed class OAuthBearerSaslExchange(SaslExchangeContext context) : BearerTokenSaslExchange(context)
{
    /// <inheritdoc/>
    protected override string? ReadToken(string message)
    {
        var separator = message.IndexOf('\x01', StringComparison.Ordinal);

        return separator >= 0 && IsGs2Header(message[..separator]) ? ReadBearerToken(ReadPairs(message[(separator + 1)..])) : null;
    }

    // gs2-cb-flag "," [authzid] "," with the flag n or y (RFC 5801 section 4, RFC 7628 section 3.1).
    private static bool IsGs2Header(string header) =>
        (header.StartsWith("n,", StringComparison.Ordinal) || header.StartsWith("y,", StringComparison.Ordinal))
        && header.EndsWith(',')
        && (header.Length == 3 || header[2..].StartsWith("a=", StringComparison.Ordinal));
}
