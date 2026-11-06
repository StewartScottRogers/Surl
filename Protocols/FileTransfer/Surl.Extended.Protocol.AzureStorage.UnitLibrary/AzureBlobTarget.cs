namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// A Blob endpoint request target in the account-host form, <c>/&lt;container&gt;/&lt;blob&gt;</c>
/// (ADR-0109, decision 2): the path, the container and blob name as sent (still percent-encoded),
/// and the query's parameters, each name and value percent-decoded.
/// </summary>
/// <param name="Path">The path, without the query.</param>
/// <param name="RawContainer">The first path segment, or empty.</param>
/// <param name="RawBlob">The rest of the path after the container's <c>/</c>, or empty.</param>
/// <param name="QueryParameters">The query's parameters, in the order sent.</param>
internal sealed record AzureBlobTarget(string Path, string RawContainer, string RawBlob, IReadOnlyList<KeyValuePair<string, string>> QueryParameters)
{
    /// <summary>
    /// Whether the target names both a container and a blob.
    /// </summary>
    public bool NamesABlob => RawContainer.Length > 0 && RawBlob.Length > 0;

    /// <summary>
    /// The blob name, percent-decoded once as UTF-8 (ADR-0109, decision 2).
    /// </summary>
    public string BlobName => Uri.UnescapeDataString(RawBlob);

    /// <summary>
    /// Splits a request target.
    /// </summary>
    /// <param name="target">The request line's target.</param>
    /// <returns>The target's parts.</returns>
    public static AzureBlobTarget Parse(string target)
    {
        var queryStart = target.IndexOf('?');
        var path = queryStart < 0 ? target : target[..queryStart];
        var parameters = queryStart < 0 ? [] : ParseQuery(target[(queryStart + 1)..]);
        if (!path.StartsWith('/'))
        {
            return new AzureBlobTarget(path, string.Empty, string.Empty, parameters);
        }

        var containerEnd = path.IndexOf('/', 1);

        return containerEnd < 0
            ? new AzureBlobTarget(path, path[1..], string.Empty, parameters)
            : new AzureBlobTarget(path, path[1..containerEnd], path[(containerEnd + 1)..], parameters);
    }

    private static List<KeyValuePair<string, string>> ParseQuery(string query) =>
        query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(parameter => parameter.Split('=', 2))
            .Select(parts => new KeyValuePair<string, string>(Decode(parts[0]), parts.Length > 1 ? Decode(parts[1]) : string.Empty))
            .ToList();

    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
}
