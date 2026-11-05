using System.Globalization;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// How an origin's response body is framed (RFC 9112 section 6.3), so the proxy relays it with
/// the origin's own framing (ADR-0088 decision 3).
/// </summary>
/// <param name="Kind">The framing.</param>
/// <param name="Length">The body's length when <paramref name="Kind"/> is <see cref="OriginResponseBodyKind.Length"/>; otherwise 0.</param>
internal readonly record struct OriginResponseBody(OriginResponseBodyKind Kind, long Length)
{
    /// <summary>
    /// The framing of <paramref name="response"/>, sent in answer to a
    /// <paramref name="requestMethod"/> request: none for <c>HEAD</c>, <c>1xx</c>, <c>204</c> and
    /// <c>304</c>; chunked when <c>Transfer-Encoding</c> ends in <c>chunked</c>; until the
    /// origin closes for any other <c>Transfer-Encoding</c> or no length; otherwise the one
    /// <c>Content-Length</c>, and invalid when the lengths are malformed or disagree.
    /// </summary>
    /// <param name="requestMethod">The request's method.</param>
    /// <param name="response">The origin's response head.</param>
    /// <returns>The framing.</returns>
    public static OriginResponseBody Of(string requestMethod, OriginResponseHead response)
    {
        if (requestMethod == "HEAD" || response.HasNoBody)
        {
            return new(OriginResponseBodyKind.None, 0);
        }

        var transferCodings = Values(response, "Transfer-Encoding").ToList();
        if (transferCodings.Count > 0)
        {
            return new(string.Equals(transferCodings[^1], "chunked", StringComparison.OrdinalIgnoreCase) ? OriginResponseBodyKind.Chunked : OriginResponseBodyKind.UntilClose, 0);
        }

        var lengths = Values(response, "Content-Length").Distinct(StringComparer.Ordinal).ToList();

        return lengths.Count switch
        {
            0 => new(OriginResponseBodyKind.UntilClose, 0),
            1 when long.TryParse(lengths[0], NumberStyles.None, CultureInfo.InvariantCulture, out var length) => new(OriginResponseBodyKind.Length, length),
            _ => new(OriginResponseBodyKind.Invalid, 0),
        };
    }

    private static IEnumerable<string> Values(OriginResponseHead response, string name) =>
        response.Fields
            .Where(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(field => field.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
