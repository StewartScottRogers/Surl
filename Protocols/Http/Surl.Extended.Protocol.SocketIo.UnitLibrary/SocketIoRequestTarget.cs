namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// A request target split into its path and its query, whose parameters are read by name
/// (EIO "Request path", "Query parameters"; ADR-0120, decision 1).
/// </summary>
internal sealed class SocketIoRequestTarget
{
    private readonly string query;

    private SocketIoRequestTarget(string path, string query)
    {
        Path = path;
        this.query = query;
    }

    /// <summary>
    /// The path, without the query, as sent.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Splits an origin-form target (<c>/socket.io/?EIO=4</c>) or an absolute-form one.
    /// </summary>
    public static SocketIoRequestTarget Parse(string target)
    {
        var pathAndQuery = !target.StartsWith('/') && Uri.TryCreate(target, UriKind.Absolute, out var uri)
            ? uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped)
            : target;
        var queryStart = pathAndQuery.IndexOf('?', StringComparison.Ordinal);

        return queryStart < 0
            ? new SocketIoRequestTarget(pathAndQuery, string.Empty)
            : new SocketIoRequestTarget(pathAndQuery[..queryStart], pathAndQuery[(queryStart + 1)..]);
    }

    /// <summary>
    /// The first query parameter named <paramref name="name"/>, percent-decoded, or
    /// <see langword="null"/> when there is none.
    /// </summary>
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
