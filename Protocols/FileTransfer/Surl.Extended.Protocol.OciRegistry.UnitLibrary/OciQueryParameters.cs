namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Reads a request's query parameters: <c>&amp;</c>-separated <c>name=value</c> pairs, each side
/// percent-decoded once. A <c>+</c> stays a <c>+</c> (RFC 3986 gives it no meaning in a query),
/// because media types such as <c>application/vnd.oci.image.manifest.v1+json</c> carry one.
/// </summary>
internal static class OciQueryParameters
{
    /// <summary>
    /// The value of the first parameter named <paramref name="name"/>.
    /// </summary>
    /// <param name="query">The query after the <c>?</c>, still percent-encoded.</param>
    /// <param name="name">The parameter's name.</param>
    /// <returns>The value, empty for a parameter with no <c>=</c>; <see langword="null"/> when none is named so.</returns>
    public static string? First(string query, string name)
    {
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            var pairName = Uri.UnescapeDataString(equals < 0 ? pair : pair[..equals]);
            if (pairName == name)
            {
                return equals < 0 ? string.Empty : Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
        }

        return null;
    }
}
