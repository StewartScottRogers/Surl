using System.Security.Cryptography;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Reads what an object answers with (ADR-0100, decision 2.3): the object metadata index's
/// properties for a file it keys, and the index's defaults, computed here, for a file in a share,
/// which the index does not key.
/// </summary>
/// <param name="store">The content store the object is read from.</param>
/// <param name="index">The object metadata index of the store's served root.</param>
/// <param name="log">Where a failure to rewrite the index file is noted.</param>
internal sealed class S3ObjectPropertiesReader(ContentStore store, ObjectMetadataIndex index, IExchangeLog log)
{
    /// <summary>
    /// The object's properties.
    /// </summary>
    /// <param name="mapping">The object's mapped location.</param>
    /// <param name="indexKey">The object's index key (<see cref="S3IndexKey"/>), or <see langword="null"/>
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
