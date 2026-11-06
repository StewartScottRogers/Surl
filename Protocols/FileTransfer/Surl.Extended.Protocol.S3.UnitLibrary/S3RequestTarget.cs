namespace Surl.Extended.Protocol.S3;

/// <summary>
/// A request target read path style or virtual-hosted style ("Virtual hosting of buckets";
/// ADR-0108, decision 2): path style, the path's first segment is the bucket and the rest the
/// key; virtual-hosted, the bucket comes from the <c>Host</c> field and the whole path is the key.
/// Both are still percent-encoded as the client sent them.
/// </summary>
/// <param name="Path">The path, before any <c>?</c>, as sent.</param>
/// <param name="Query">The query after the <c>?</c>, or <see langword="null"/> when there is none.</param>
/// <param name="RawBucket">The bucket as sent, or empty when the request names the service.</param>
/// <param name="RawKey">The key as sent, or empty when the request names no key.</param>
internal sealed record S3RequestTarget(string Path, string? Query, string RawBucket, string RawKey)
{
    /// <summary>
    /// Whether the request names an object: a bucket and a key.
    /// </summary>
    public bool NamesAnObject => RawBucket.Length > 0 && RawKey.Length > 0;

    /// <summary>
    /// Whether the request names the service itself: the path <c>/</c>, path style.
    /// </summary>
    public bool NamesTheService => RawBucket.Length == 0 && Path == "/";

    /// <summary>
    /// Whether the request names a bucket and no key.
    /// </summary>
    public bool NamesABucket => RawBucket.Length > 0 && RawKey.Length == 0;

    /// <summary>
    /// The object's path-style path, <c>/&lt;bucket&gt;/&lt;key&gt;</c>, as the content store maps it.
    /// </summary>
    public string ObjectPath => $"/{RawBucket}/{RawKey}";

    /// <summary>
    /// Reads a request target path style, splitting off the query.
    /// </summary>
    /// <param name="target">The request target as the client sent it.</param>
    /// <returns>The target's parts; a target that is not a rooted path names no bucket.</returns>
    public static S3RequestTarget Parse(string target) => Parse(target, null);

    /// <summary>
    /// Reads a request target, splitting off the query: virtual-hosted style when
    /// <paramref name="hostBucket"/> is given, else path style.
    /// </summary>
    /// <param name="target">The request target as the client sent it.</param>
    /// <param name="hostBucket">The bucket the <c>Host</c> field names, or <see langword="null"/>.</param>
    /// <returns>The target's parts; a target that is not a rooted path names no bucket.</returns>
    public static S3RequestTarget Parse(string target, string? hostBucket)
    {
        var queryStart = target.IndexOf('?');
        var path = queryStart < 0 ? target : target[..queryStart];
        var query = queryStart < 0 ? null : WithoutPresignedParameters(target[(queryStart + 1)..]);
        if (!path.StartsWith('/'))
        {
            return new S3RequestTarget(path, query, string.Empty, string.Empty);
        }

        if (hostBucket is not null)
        {
            return new S3RequestTarget(path, query, hostBucket, path[1..]);
        }

        var bucketEnd = path.IndexOf('/', 1);

        return bucketEnd < 0
            ? new S3RequestTarget(path, query, path[1..], string.Empty)
            : new S3RequestTarget(path, query, path[1..bucketEnd], path[(bucketEnd + 1)..]);
    }

    // A presigned URL's signature parameters ("Authenticating Requests: Using Query Parameters";
    // ADR-0108 decision 4.5) sign the request and name no operation, so the operation is read
    // from the rest; a query of nothing else is no query.
    private static string? WithoutPresignedParameters(string query)
    {
        var kept = query.Split('&').Where(parameter => !IsPresignedParameter(parameter)).ToArray();

        return kept.Length == 0 ? null : string.Join('&', kept);
    }

    private static bool IsPresignedParameter(string parameter) =>
        PresignedParameterNames.Contains(Uri.UnescapeDataString(parameter.Split('=', 2)[0]));

    private static readonly HashSet<string> PresignedParameterNames = new(StringComparer.Ordinal)
    {
        "X-Amz-Algorithm",
        "X-Amz-Credential",
        "X-Amz-Date",
        "X-Amz-Expires",
        "X-Amz-SignedHeaders",
        "X-Amz-Signature",
        "X-Amz-Security-Token",
    };
}
