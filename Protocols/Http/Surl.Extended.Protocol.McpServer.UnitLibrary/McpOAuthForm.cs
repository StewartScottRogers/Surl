namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Reads the <c>application/x-www-form-urlencoded</c> parameters of an OAuth request: a query string or a token
/// request's body (RFC 6749 appendix B). A parameter given twice makes the request invalid (RFC 6749 section 3.1).
/// </summary>
internal static class McpOAuthForm
{
    /// <summary>Reads the parameters.</summary>
    /// <param name="text">The encoded parameters, without a leading <c>?</c>.</param>
    /// <returns>Each name and its decoded value, or <see langword="null" /> when a name is given twice.</returns>
    public static IReadOnlyDictionary<string, string>? Parse(string text)
    {
        Dictionary<string, string> parameters = new(StringComparer.Ordinal);
        foreach (string pair in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            string name = Decode(equals < 0 ? pair : pair[..equals]);
            string value = equals < 0 ? string.Empty : Decode(pair[(equals + 1)..]);
            if (!parameters.TryAdd(name, value))
            {
                return null;
            }
        }

        return parameters;
    }

    /// <summary>The query of a request target, without its <c>?</c>.</summary>
    /// <param name="target">The request target.</param>
    /// <returns>The query, or an empty string when the target has none.</returns>
    public static string QueryOf(string target)
    {
        int question = target.IndexOf('?', StringComparison.Ordinal);
        return question < 0 ? string.Empty : target[(question + 1)..];
    }

    private static string Decode(string encoded) => Uri.UnescapeDataString(encoded.Replace('+', ' '));
}
