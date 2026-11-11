using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers <c>storage.objects.patch</c> and <c>storage.objects.update</c> (ADR-0148, decision 7): a patch merges the JSON body
/// into the object's writable metadata (a <c>null</c> removes a member; <c>metadata</c> merges by key), an update replaces
/// all of it with the body's. Both raise the metageneration by one and leave the bytes and the generation alone.
/// </summary>
/// <remarks>
/// The object has been found and its conditions met by the caller. A body is JSON of at most
/// <see cref="MaxBodyBytes"/>, else <c>400 invalid</c>. A file the index does not key - one in a share - has no entry to keep
/// the metadata in, so it is <c>403 forbidden</c>.
/// </remarks>
/// <param name="store">The content store, whose shares and buckets key the index.</param>
/// <param name="index">The object metadata index of the store's served root.</param>
/// <param name="properties">Reads the digests of the object answered.</param>
internal sealed class GcsJsonObjectUpdate(ContentStore store, ObjectMetadataIndex index, GcsObjectPropertiesReader properties)
{
    /// <summary>
    /// The most bytes of JSON body a patch or update takes (ADR-0148, decision 8).
    /// </summary>
    public const int MaxBodyBytes = 1024 * 1024;

    /// <summary>
    /// Applies a patch or an update to an object.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="route">Its route, naming the bucket and object.</param>
    /// <param name="mapping">Where the object's file is.</param>
    /// <param name="found">The object as it is now.</param>
    /// <param name="isPatch"><see langword="true"/> for a patch, <see langword="false"/> for an update.</param>
    /// <param name="resources">Writes the answer.</param>
    /// <param name="cancellationToken">Cancels reading the body and writing the index.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(GcsReply reply, HttpRequest request, GcsJsonRoute route, ContentPathMapping mapping, ObjectProperties found, bool isPatch, GcsJsonResources resources, CancellationToken cancellationToken)
    {
        if (GcsIndexKey.OfRaw(store, route.RawBucket, route.RawName) is not { } indexKey)
        {
            return reply.JsonError(GcsJsonError.Forbidden);
        }

        using var body = new GcsBoundedBodyStream(MaxBodyBytes);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(body, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        if (body.IsTooLarge || outcome != HttpRequestBodyDiscardOutcome.Discarded || GcsJsonObjectFields.Read(body.ToArray()) is not { } fields)
        {
            return reply.JsonError(GcsJsonError.Invalid);
        }

        var metadata = fields.Compose(isPatch ? found : null) with
        {
            ContentMd5 = found.ContentMd5,
            ETag = found.ETag,
            Generation = found.Generation,
            Metageneration = found.Metageneration + 1,
        };
        if (GcsPutObject.UserMetadataBytes(metadata.UserMetadata) > GcsPutObject.MaxUserMetadataBytes)
        {
            return reply.JsonError(GcsJsonError.Invalid);
        }

        var stored = await index.StoreAsync(indexKey, metadata, cancellationToken);

        // The file was found a moment ago, so what the index refuses is the fields' size or its own room.
        return stored.Properties is { } updated
            ? reply.Json(resources.Object(Uri.UnescapeDataString(route.RawBucket), Uri.UnescapeDataString(route.RawName), updated, await properties.HashAsync(mapping, updated.Length, cancellationToken)))
            : reply.JsonError(stored.Outcome == ObjectMetadataStoreOutcome.MetadataTooLarge ? GcsJsonError.Invalid : GcsJsonError.QuotaExceeded);
    }
}
