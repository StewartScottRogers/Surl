using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The key the object metadata index keeps an object under: the file's path relative to the
/// served root, a <c>--bucket</c> name replaced by its content path (ADR-0100, decision 2.3;
/// ADR-0102, decision 2.4).
/// </summary>
internal static class GcsIndexKey
{
    /// <summary>
    /// The index key of the object at <paramref name="rawBucket"/> and <paramref name="rawKey"/>
    /// as the client sent them, still percent-encoded.
    /// </summary>
    /// <param name="store">The content store, whose shares and bucket names are read.</param>
    /// <param name="rawBucket">The bucket as sent.</param>
    /// <param name="rawKey">The key as sent.</param>
    /// <returns>The index key, or <see langword="null"/> as <see cref="Of"/> gives it.</returns>
    public static string? OfRaw(ContentStore store, string rawBucket, string rawKey) =>
        Of(store, Uri.UnescapeDataString(rawBucket), Uri.UnescapeDataString(rawKey));

    /// <summary>
    /// The index key of the object at <paramref name="bucket"/> and <paramref name="key"/>.
    /// </summary>
    /// <param name="store">The content store, whose shares and bucket names are read.</param>
    /// <param name="bucket">The bucket, decoded.</param>
    /// <param name="key">The key, decoded.</param>
    /// <returns>The index key, or <see langword="null"/> for a file in a share, which lies outside
    /// the served root the index keys, or a key the index cannot hold.</returns>
    public static string? Of(ContentStore store, string bucket, string key)
    {
        var folder = store.Buckets.FirstOrDefault(named => named.Name == bucket)?.ContentPath.Trim('/') ?? bucket;
        var indexKey = $"{folder}/{key}";
        var firstSegment = indexKey[..indexKey.IndexOf('/')];

        return store.Shares.Any(share => share.Name == firstSegment) || !ObjectMetadataIndex.IsValidKey(indexKey) ? null : indexKey;
    }
}
