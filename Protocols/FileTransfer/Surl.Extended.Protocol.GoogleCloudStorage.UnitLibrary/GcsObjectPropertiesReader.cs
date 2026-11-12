using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Reads what an object answers with (ADR-0110, decision 2; ADR-0100, decision 2.3): the object
/// metadata index's properties for a file it keys, and the index's defaults, computed here, for a
/// file in a share, which the index does not key; and the object's digests for <c>x-goog-hash</c>.
/// </summary>
/// <param name="store">The content store the object is read from.</param>
/// <param name="index">The object metadata index of the store's served root.</param>
/// <param name="log">Where a failure to rewrite the index file is noted.</param>
internal sealed class GcsObjectPropertiesReader(ContentStore store, ObjectMetadataIndex index, IExchangeLog log)
{
    /// <summary>
    /// The object's properties.
    /// </summary>
    /// <param name="mapping">The object's mapped location.</param>
    /// <param name="indexKey">The object's index key (<see cref="GcsIndexKey"/>), or <see langword="null"/>
    /// when the index does not key it.</param>
    /// <param name="cancellationToken">Cancels hashing the file.</param>
    /// <returns>The properties, or <see langword="null"/> when no file is there.</returns>
    public async Task<ObjectProperties?> ReadAsync(ContentPathMapping mapping, string? indexKey, CancellationToken cancellationToken)
    {
        if (store.GetFileStatus(mapping) is not { } status)
        {
            return null;
        }

        if (indexKey is null)
        {
            var digests = await HashAsync(mapping, status.Length, cancellationToken);

            return new ObjectProperties(
                ContentMediaTypes.Of(mapping.Location!), null, null, null, null, [], digests.ETag, GcsGeneration.Of(status.LastModifiedUtc), status.Length, status.LastModifiedUtc)
            {
                Crc32c = digests.Crc32cValue,
            };
        }

        var reading = await index.ReadAsync(indexKey, cancellationToken);
        if (reading.StorageFailure is { } failure)
        {
            log.Note($"The object metadata index could not be written: {failure}");
        }

        return reading.Properties;
    }

    /// <summary>
    /// Every folder a client put as an object whose folder still exists, by its index key
    /// (<see cref="GcsIndexKey.OfFolderObject"/>).
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for the index.</param>
    /// <returns>Each folder object's properties.</returns>
    public Task<IReadOnlyDictionary<string, ObjectProperties>> ReadFolderObjectsAsync(CancellationToken cancellationToken) =>
        index.ReadFolderObjectsAsync(cancellationToken);

    /// <summary>
    /// The object's digests for <c>x-goog-hash</c>: the CRC-32C and MD5 its properties cache
    /// (<see cref="GcsObjectDigests.Cached"/>), else read from its bytes.
    /// </summary>
    /// <param name="mapping">The object's mapped location.</param>
    /// <param name="found">The object's properties, as <see cref="ReadAsync"/> answered.</param>
    /// <param name="cancellationToken">Cancels reading the file.</param>
    /// <returns>The digests.</returns>
    public async Task<GcsObjectDigests> DigestsAsync(ContentPathMapping mapping, ObjectProperties found, CancellationToken cancellationToken) =>
        GcsObjectDigests.Cached(found.ETag, found.Crc32c) ?? await HashAsync(mapping, found.Length, cancellationToken);

    /// <summary>
    /// The object's CRC-32C and MD5, read from its bytes.
    /// </summary>
    /// <param name="mapping">The object's mapped location.</param>
    /// <param name="length">The object's length.</param>
    /// <param name="cancellationToken">Cancels reading the file.</param>
    /// <returns>The digests.</returns>
    public async Task<GcsObjectDigests> HashAsync(ContentPathMapping mapping, long length, CancellationToken cancellationToken)
    {
        using var hashes = new GcsObjectHashes();
        await using (var sink = new GcsHashingStream(hashes, null))
        {
            await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(length), sink, cancellationToken);
        }

        return hashes.Finish();
    }
}
