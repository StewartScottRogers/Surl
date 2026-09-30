namespace Surl.Authentication;

/// <summary>
/// SASL <c>OAUTHBEARER</c>, RFC 7628 (ADR-0049, section 5): the response is the GS2 header
/// <c>n,[a=&lt;authzid&gt;],</c> (or <c>y,</c>; RFC 7628 section 3.1 allows no channel binding),
/// <c>\x01</c>, then <c>key=value\x01</c> pairs and a final <c>\x01</c>. The <c>a=</c> is not
/// matched, since a token account has no name, but names the owner an accepted login acts as,
/// the empty name when it is left out (ADR-0050, decision 2).
/// </summary>
internal sealed class OAuthBearerSaslExchange(SaslExchangeContext context) : BearerTokenSaslExchange(context)
{
    /// <inheritdoc/>
    protected override BearerLogin? ReadLogin(string message)
    {
        var separator = message.IndexOf('\x01', StringComparison.Ordinal);

        return separator >= 0 && ReadGs2Authzid(message[..separator]) is { } user
            && ReadBearerToken(ReadPairs(message[(separator + 1)..])) is { } token
            ? new BearerLogin(user, token)
            : null;
    }

    // gs2-cb-flag "," [authzid] "," with the flag n or y (RFC 5801 section 4, RFC 7628 section 3.1):
    // the authzid with its =2C and =3D decoded, empty when left out, or null when the header is malformed.
    private static string? ReadGs2Authzid(string header)
    {
        if (!(header.StartsWith("n,", StringComparison.Ordinal) || header.StartsWith("y,", StringComparison.Ordinal))
            || !header.EndsWith(','))
        {
            return null;
        }

        if (header.Length == 3)
        {
            return string.Empty;
        }

        return header[2..].StartsWith("a=", StringComparison.Ordinal)
            ? header[4..^1].Replace("=2C", ",", StringComparison.Ordinal).Replace("=3D", "=", StringComparison.Ordinal)
            : null;
    }
}
