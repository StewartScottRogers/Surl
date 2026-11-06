using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Protocol.Http;

/// <summary>
/// NTLM (ADR-0041) and Negotiate (ADR-0044): logins bound to the connection, which neither HTTP/2
/// (RFC 9113 section 8.2.3; ADR-0081, decision 7) nor HTTP/3 (ADR-0082, decision 9) carries. The
/// HTTP/2 and HTTP/3 stream writers ask these questions so the answerer never asks which version
/// carried a request.
/// </summary>
internal static class ConnectionBoundLogins
{
    /// <summary>Whether <paramref name="fields"/> hold an <c>authorization</c> whose auth-scheme is NTLM or Negotiate.</summary>
    /// <param name="fields">The request head's regular fields, their names lowercase.</param>
    /// <returns><see langword="true"/> when the request carries a login bound to the connection.</returns>
    public static bool AreCarriedBy(IEnumerable<KeyValuePair<string, string>> fields) =>
        fields.Any(field => field.Key == "authorization" && IsTheSchemeOf(field.Value));

    /// <summary>Whether <paramref name="answer"/> is a <c>401</c> whose every <c>WWW-Authenticate</c> challenge is NTLM or Negotiate.</summary>
    /// <param name="answer">The answer the answerer decided.</param>
    /// <returns><see langword="true"/> when the only logins the client could answer with are bound to the connection.</returns>
    public static bool AreTheOnlyLoginsOfferedBy(HttpAnswer answer)
    {
        var challenges = answer.Fields
            .Where(field => string.Equals(field.Key, "WWW-Authenticate", StringComparison.OrdinalIgnoreCase))
            .Select(field => field.Value)
            .ToList();

        return answer.Status == HttpStatus.Unauthorized && challenges.Count > 0 && challenges.All(IsTheSchemeOf);
    }

    // Whether a credentials or challenge value's auth-scheme (RFC 9110, section 11.1) is NTLM or Negotiate.
    private static bool IsTheSchemeOf(string value)
    {
        var scheme = value.TrimStart().Split(' ', 2)[0];

        return scheme.Equals("NTLM", StringComparison.OrdinalIgnoreCase) || scheme.Equals("Negotiate", StringComparison.OrdinalIgnoreCase);
    }
}
