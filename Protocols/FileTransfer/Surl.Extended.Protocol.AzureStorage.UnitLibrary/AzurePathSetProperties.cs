using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers Path - Update's <c>setProperties</c> ("Path - Update"; ADR-0109, decision 1): the file's
/// <c>x-ms-properties</c> replace its user metadata in the object metadata index - every property
/// not named is removed, and all of them when the field is not sent, as the page says - and each
/// <c>x-ms-content-type</c>, <c>-encoding</c>, <c>-language</c>, <c>-disposition</c> and
/// <c>x-ms-cache-control</c> sent replaces the stored one. <c>200</c> with <c>ETag</c> and
/// <c>Last-Modified</c>.
/// </summary>
/// <remarks>
/// Needs <c>--allow-uploads</c> (off: <c>403 AuthorizationPermissionMismatch</c>). An
/// <c>x-ms-properties</c> that does not read (<see cref="AzurePropertiesField.Parse"/>) is <c>400
/// InvalidHeaderValue</c>; no path is <c>404 PathNotFound</c>; a directory is <c>409
/// ResourceTypeMismatch</c>, because the index keeps metadata for files only; metadata past 8 KiB is
/// <c>400 MetadataTooLarge</c>. A file in a share, which the index does not key, keeps nothing, as
/// Put Blob keeps nothing for it.
/// </remarks>
/// <param name="store">The content store the file is in.</param>
/// <param name="index">The object metadata index of the store's served root.</param>
/// <param name="properties">Reads the file's stored properties.</param>
/// <param name="log">Where a failure to rewrite the index file is noted.</param>
internal sealed class AzurePathSetProperties(ContentStore store, ObjectMetadataIndex index, AzureBlobPropertiesReader properties, IExchangeLog log)
{
    /// <summary>
    /// Answers a <c>setProperties</c> on a path in an existing filesystem.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose fields name the properties.</param>
    /// <param name="target">The request's filesystem and path.</param>
    /// <param name="caller">Who writes, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels reading the file and writing the index.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch));
        }

        var sent = AzureRequestFields.Value(request.Fields, AzurePropertiesField.Name) ?? string.Empty;
        if (AzurePropertiesField.Parse(sent) is not { } userMetadata)
        {
            return reply.Error(AzureRefusal.InvalidHeaderValue(AzurePropertiesField.Name, sent));
        }

        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        var kind = mapping.IsMapped ? store.GetEntryKind(mapping) : ContentEntryKind.None;
        if (kind != ContentEntryKind.File)
        {
            return reply.Error(AzureRefusal.Of(kind == ContentEntryKind.Directory ? AzureError.ResourceTypeMismatch : AzureError.PathNotFound));
        }

        var indexKey = AzureIndexKey.OfRaw(store, target.RawContainer, target.RawBlob);
        var before = (await properties.ReadAsync(mapping, indexKey, cancellationToken))!;
        if (indexKey is not null && await StoreAsync(indexKey, MetadataOf(request.Fields, before, userMetadata), cancellationToken) is { } refusal)
        {
            return reply.Error(AzureRefusal.Of(refusal));
        }

        return reply.Answer(
            HttpStatus.Ok,
            [
                new("ETag", before.ETag),
                new("Last-Modified", before.LastWriteTimeUtc.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)),
                new("Content-Length", "0"),
            ],
            HttpAnswerBody.None);
    }

    // The sent header fields over the stored ones, and the sent properties in place of the stored ones.
    private static ObjectMetadata MetadataOf(IReadOnlyList<KeyValuePair<string, string>> fields, ObjectProperties before, Dictionary<string, string> userMetadata) => new()
    {
        ContentType = AzureRequestFields.Value(fields, "x-ms-content-type") ?? before.ContentType,
        ContentEncoding = AzureRequestFields.Value(fields, "x-ms-content-encoding") ?? before.ContentEncoding,
        ContentDisposition = AzureRequestFields.Value(fields, "x-ms-content-disposition") ?? before.ContentDisposition,
        CacheControl = AzureRequestFields.Value(fields, "x-ms-cache-control") ?? before.CacheControl,
        ContentLanguage = AzureRequestFields.Value(fields, "x-ms-content-language") ?? before.ContentLanguage,
        UserMetadata = userMetadata,
    };

    private async Task<AzureError?> StoreAsync(string indexKey, ObjectMetadata metadata, CancellationToken cancellationToken)
    {
        var stored = await index.StoreAsync(indexKey, metadata, cancellationToken);
        if (stored.StorageFailure is { } failure)
        {
            log.Note($"The object metadata index could not be written: {failure}");
        }

        return AzurePutBlob.MetadataRefusal(stored.Outcome);
    }
}
