namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// A request target read path style or virtual-hosted style ("XML API overview"; ADR-0110,
/// decision 2): path style, the path's first segment is the bucket and the rest the object name;
/// virtual-hosted, the bucket comes from the <c>Host</c> field and the whole path is the name. Both
/// are still percent-encoded as the client sent them.
/// </summary>
/// <param name="Path">The path, before any <c>?</c>, as sent.</param>
/// <param name="Query">The query after the <c>?</c>, or <see langword="null"/> when there is none.</param>
/// <param name="RawBucket">The bucket as sent, or empty when the request names the service.</param>
/// <param name="RawName">The object name as sent, or empty when the request names no object.</param>
internal sealed record GcsRequestTarget(string Path, string? Query, string RawBucket, string RawName)
{
    /// <summary>
    /// Whether the request names the service: no bucket.
    /// </summary>
    public bool NamesTheService => RawBucket.Length == 0;

    /// <summary>
    /// Whether the request names no object: only its bucket, or the service.
    /// </summary>
    public bool NamesNoObject => RawName.Length == 0;

    /// <summary>
    /// The object's path-style path, <c>/&lt;bucket&gt;/&lt;name&gt;</c>, as the content store maps it.
    /// </summary>
    public string ObjectPath => $"/{RawBucket}/{RawName}";

    /// <summary>
    /// The query's parameters, decoded; none when there is no query.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Parameters => Query is null ? [] : GcsQueryParameters.Parse(Query);

    /// <summary>
    /// Reads a request target, splitting off the query: virtual-hosted style when
    /// <paramref name="hostBucket"/> is given, else path style.
    /// </summary>
    /// <param name="target">The request target as the client sent it.</param>
    /// <param name="hostBucket">The bucket the <c>Host</c> field names, or <see langword="null"/>.</param>
    /// <returns>The target's parts; a target that is not a rooted path names the service.</returns>
    public static GcsRequestTarget Parse(string target, string? hostBucket)
    {
        var queryStart = target.IndexOf('?');
        var path = queryStart < 0 ? target : target[..queryStart];
        var query = queryStart < 0 ? null : target[(queryStart + 1)..];
        if (!path.StartsWith('/'))
        {
            return new GcsRequestTarget(path, query, string.Empty, string.Empty);
        }

        if (hostBucket is not null)
        {
            return new GcsRequestTarget(path, query, hostBucket, path[1..]);
        }

        var bucketEnd = path.IndexOf('/', 1);

        return bucketEnd < 0
            ? new GcsRequestTarget(path, query, path[1..], string.Empty)
            : new GcsRequestTarget(path, query, path[1..bucketEnd], path[(bucketEnd + 1)..]);
    }
}
