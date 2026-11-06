using System.Security.Cryptography;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Reads what a blob answers with (ADR-0100, decision 2.3): the object metadata index's properties
/// for a file it keys - its defaults for a file another protocol wrote - and those defaults,
/// computed here, for a file in a share, which the index does not key.
/// </summary>
/// <param name="store">The content store the blob is read from.</param>
/// <param name="index">The object metadata index of the store's served root.</param>
/// <param name="log">Where a failure to rewrite the index file is noted.</param>
internal sealed class AzureBlobPropertiesReader(ContentStore store, ObjectMetadataIndex index, IExchangeLog log)
{
    /// <summary>
    /// The blob's properties.
    /// </summary>
    /// <param name="mapping">The blob's mapped location.</param>
    /// <param name="indexKey">The blob's index key (<see cref="AzureIndexKey"/>), or <see langword="null"/>
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
            return await DefaultPropertiesAsync(mapping, status, cancellationToken);
        }

        var reading = await index.ReadAsync(indexKey, cancellationToken);
        if (reading.StorageFailure is { } failure)
        {
            log.Note($"The object metadata index could not be written: {failure}");
        }

        return reading.Properties;
    }

    private async Task<ObjectProperties> DefaultPropertiesAsync(ContentPathMapping mapping, ContentFileStatus status, CancellationToken cancellationToken)
    {
        using var md5 = MD5.Create();
        await using (var hashing = new CryptoStream(Stream.Null, md5, CryptoStreamMode.Write))
        {
            await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(status.Length), hashing, cancellationToken);
        }

        return new ObjectProperties(
            ContentMediaTypes.Of(mapping.Location!), null, null, null, null, [], $"\"{Convert.ToHexStringLower(md5.Hash!)}\"", 0, status.Length, status.LastModifiedUtc);
    }
}
