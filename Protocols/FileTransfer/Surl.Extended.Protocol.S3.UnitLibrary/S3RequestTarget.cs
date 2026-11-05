namespace Surl.Extended.Protocol.S3;

/// <summary>
/// A request target read path style ("Virtual hosting of buckets"; ADR-0108, decision 2): the
/// path's first segment is the bucket and the rest the key, both still percent-encoded as the
/// client sent them.
/// </summary>
/// <param name="Path">The path, before any <c>?</c>, as sent.</param>
/// <param name="Query">The query after the <c>?</c>, or <see langword="null"/> when there is none.</param>
/// <param name="RawBucket">The bucket as sent, or empty when the path names the service.</param>
/// <param name="RawKey">The key as sent, or empty when the path names no key.</param>
internal sealed record S3RequestTarget(string Path, string? Query, string RawBucket, string RawKey)
{
    /// <summary>
    /// Whether the path names an object: a bucket and a key.
    /// </summary>
    public bool NamesAnObject => RawBucket.Length > 0 && RawKey.Length > 0;

    /// <summary>
    /// Reads a request target, splitting off the query and reading the path style.
    /// </summary>
    /// <param name="target">The request target as the client sent it.</param>
    /// <returns>The target's parts; a target that is not a rooted path names no bucket.</returns>
    public static S3RequestTarget Parse(string target)
    {
        var queryStart = target.IndexOf('?');
        var path = queryStart < 0 ? target : target[..queryStart];
        var query = queryStart < 0 ? null : target[(queryStart + 1)..];
        if (!path.StartsWith('/'))
        {
            return new S3RequestTarget(path, query, string.Empty, string.Empty);
        }

        var bucketEnd = path.IndexOf('/', 1);

        return bucketEnd < 0
            ? new S3RequestTarget(path, query, path[1..], string.Empty)
            : new S3RequestTarget(path, query, path[1..bucketEnd], path[(bucketEnd + 1)..]);
    }
}
