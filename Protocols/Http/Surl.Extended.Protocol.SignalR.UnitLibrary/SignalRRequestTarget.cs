namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A request target split into the path the hub compares byte for byte and the query parameters
/// it reads: <c>negotiateVersion</c>, <c>access_token</c> and <c>id</c>
/// (ADR-0119, decisions 1, 2 and 5).
/// </summary>
internal sealed class SignalRRequestTarget
{
    private readonly string query;

    private SignalRRequestTarget(string path, string query)
    {
        Path = path;
        this.query = query;
    }

    /// <summary>
    /// The path, without its query, as sent; for an absolute-form target, the path it names.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Splits <paramref name="target"/>, an origin-form (<c>/chat?id=1</c>) or absolute-form
    /// (<c>http://host/chat?id=1</c>) request target.
    /// </summary>
    /// <param name="target">The request target.</param>
    /// <returns>Its path and query.</returns>
    public static SignalRRequestTarget Parse(string target)
    {
        var pathAndQuery = !target.StartsWith('/') && Uri.TryCreate(target, UriKind.Absolute, out var uri)
            ? uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped)
            : target;
        var queryStart = pathAndQuery.IndexOf('?', StringComparison.Ordinal);

        return queryStart < 0
            ? new SignalRRequestTarget(pathAndQuery, string.Empty)
            : new SignalRRequestTarget(pathAndQuery[..queryStart], pathAndQuery[(queryStart + 1)..]);
    }

    /// <summary>
    /// The value of the first query parameter named <paramref name="name"/>, percent-decoded with
    /// <c>+</c> read as a space; <see langword="null"/> when the query has none.
    /// </summary>
    /// <param name="name">The parameter's name, compared after decoding.</param>
    /// <returns>Its value; empty for a parameter with no <c>=</c>.</returns>
    public string? QueryParameter(string name)
    {
        foreach (var parameter in query.Split('&'))
        {
            var equals = parameter.IndexOf('=', StringComparison.Ordinal);
            var parameterName = equals < 0 ? parameter : parameter[..equals];
            if (Decode(parameterName) == name)
            {
                return equals < 0 ? string.Empty : Decode(parameter[(equals + 1)..]);
            }
        }

        return null;
    }

    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
}
