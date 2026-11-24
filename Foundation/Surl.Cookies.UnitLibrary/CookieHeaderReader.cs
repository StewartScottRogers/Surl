namespace Surl.Cookies;

/// <summary>
/// Reads the value of a request's <c>Cookie</c> header into name and value pairs, in header order, the way upstream curl 8.21.0
/// writes it: <c>name=value</c> pairs separated by <c>; </c> (ADR-0189 decision 5; https://curl.se/docs/http-cookies.html).
/// </summary>
/// <remarks>
/// Pairs split at each <c>;</c> and again at the first <c>=</c>; whitespace around a name or value is dropped; an empty
/// value is a pair with an empty value; a segment with no <c>=</c>, an empty name or nothing at all is skipped.
/// </remarks>
public static class CookieHeaderReader
{
    /// <summary>Reads every pair in one <c>Cookie</c> header value.</summary>
    public static IReadOnlyList<CookiePair> Read(string? headerValue)
    {
        List<CookiePair> pairs = [];
        foreach (string segment in (headerValue ?? string.Empty).Split(';'))
        {
            int equals = segment.IndexOf('=', StringComparison.Ordinal);
            string name = equals < 0 ? string.Empty : segment[..equals].Trim();
            if (name.Length > 0)
            {
                pairs.Add(new CookiePair(name, segment[(equals + 1)..].Trim()));
            }
        }

        return pairs;
    }
}
