using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The key the object metadata index keeps a blob under: the file's path relative to the served
/// root, a <c>--bucket</c> name replaced by its content path (ADR-0100, decision 2.3; ADR-0102,
/// decision 2.4).
/// </summary>
internal static class AzureIndexKey
{
    /// <summary>
    /// The index key of the blob at <paramref name="rawContainer"/> and <paramref name="rawBlob"/>
    /// as the client sent them, still percent-encoded.
    /// </summary>
    /// <param name="store">The content store, whose shares and bucket names are read.</param>
    /// <param name="rawContainer">The container as sent.</param>
    /// <param name="rawBlob">The blob name as sent.</param>
    /// <returns>The index key, or <see langword="null"/> as <see cref="Of"/> gives it.</returns>
    public static string? OfRaw(ContentStore store, string rawContainer, string rawBlob) =>
        Of(store, Uri.UnescapeDataString(rawContainer), Uri.UnescapeDataString(rawBlob));

    /// <summary>
    /// The index key of the blob at <paramref name="container"/> and <paramref name="blobName"/>.
    /// </summary>
    /// <param name="store">The content store, whose shares and bucket names are read.</param>
    /// <param name="container">The container, decoded.</param>
    /// <param name="blobName">The blob name, decoded.</param>
    /// <returns>The index key, or <see langword="null"/> for a file in a share, which lies outside
    /// the served root the index keys, or a name the index cannot hold.</returns>
    public static string? Of(ContentStore store, string container, string blobName)
    {
        var folder = store.Buckets.FirstOrDefault(named => named.Name == container)?.ContentPath.Trim('/') ?? container;
        var indexKey = $"{folder}/{blobName}";
        var firstSegment = indexKey[..indexKey.IndexOf('/')];

        return store.Shares.Any(share => share.Name == firstSegment) || !ObjectMetadataIndex.IsValidKey(indexKey) ? null : indexKey;
    }
}
