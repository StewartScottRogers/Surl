using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers <c>storage.objects.copy</c> and <c>storage.objects.rewrite</c> (ADR-0148, decision 7): the source's bytes become a
/// new generation of the destination, in the same bucket or another, with metageneration 1 and the source's stored fields unless
/// the request's body names new metadata. A rewrite always finishes in one call, so a <c>rewriteToken</c> or
/// <c>maxBytesRewrittenPerCall</c> sent changes nothing.
/// </summary>
/// <remarks>
/// Needs <c>--allow-uploads</c>. The source must be one the caller may read (else <c>404</c>), at <c>sourceGeneration</c> when
/// that is sent, and meet the <c>ifSource*</c> conditions (<c>412</c>); the destination meets the <c>if*</c> conditions as an insert's
/// does and is one the caller may write (<c>403</c>). A copy onto itself writes a new generation of the same object.
/// </remarks>
/// <param name="store">The content store whose buckets are read and written.</param>
/// <param name="properties">Reads the source.</param>
/// <param name="assembly">Writes the destination.</param>
internal sealed class GcsJsonObjectCopy(ContentStore store, GcsObjectPropertiesReader properties, GcsJsonObjectAssembly assembly)
{
    /// <summary>
    /// Answers one copy or rewrite.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">Its target, whose query is read.</param>
    /// <param name="route">Its route, naming the source and the destination.</param>
    /// <param name="caller">Who reads and writes, on <c>gs</c>.</param>
    /// <param name="resources">Writes the answer.</param>
    /// <param name="cancellationToken">Cancels reading and writing.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken)
    {
        var (refusal, source, destination) = await ResolveAsync(request, target, route, caller, cancellationToken);
        if (refusal is not null)
        {
            return reply.JsonError(refusal);
        }

        var (writeRefusal, stored, digests) = await assembly.WriteAsync(destination!, [source!], caller, request.Fields, reply.Now, cancellationToken);
        if (writeRefusal is not null)
        {
            return reply.JsonError(writeRefusal);
        }

        var bucket = Uri.UnescapeDataString(route.RawDestinationBucket);
        var name = Uri.UnescapeDataString(route.RawDestinationName);

        return reply.Json(route.Operation == GcsJsonOperation.RewriteObject ? resources.Rewrite(bucket, name, stored!, digests!) : resources.Object(bucket, name, stored!, digests!));
    }

    // Uploads on, the query, the destination bucket, the source and its conditions, then the body's metadata.
    private async Task<(GcsJsonError? Refusal, GcsJsonObjectAssembly.SourceObject? Source, GcsJsonObjectAssembly.Destination? Destination)> ResolveAsync(HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return (GcsJsonError.Forbidden, null, null);
        }

        if (CopyQuery.Read(target.Parameters) is not { } query)
        {
            return (GcsJsonError.InvalidParameter, null, null);
        }

        if (store.GetEntryStatus(store.MapBucketRequestPathForCaller($"/{route.RawDestinationBucket}/", caller)) is not { Kind: ContentEntryKind.Directory })
        {
            return (GcsJsonError.NotFound, null, null);
        }

        var mapping = store.MapBucketRequestPathForCaller($"/{route.RawBucket}/{route.RawName}", caller);
        var found = mapping.IsMapped ? await properties.ReadAsync(mapping, GcsIndexKey.OfRaw(store, route.RawBucket, route.RawName), cancellationToken) : null;
        if (found is null || (query.SourceGeneration is not null && query.SourceGeneration != found.Generation))
        {
            return (GcsJsonError.NotFound, null, null);
        }

        if (query.SourceConditions.Evaluate([], found) != GcsConditionOutcome.Serve)
        {
            return (GcsJsonError.ConditionNotMet, null, null);
        }

        var (metadata, bodyRefusal) = await MetadataAsync(request, found, cancellationToken);

        return bodyRefusal is not null
            ? (bodyRefusal, null, null)
            : (null, new GcsJsonObjectAssembly.SourceObject(mapping, found), new GcsJsonObjectAssembly.Destination(route.RawDestinationBucket, route.RawDestinationName, metadata!, query.Conditions));
    }

    // The destination's conditions, the source's, and the source's generation.
    private sealed record CopyQuery(GcsJsonConditions Conditions, GcsJsonConditions SourceConditions, long? SourceGeneration)
    {
        public static CopyQuery? Read(IReadOnlyList<KeyValuePair<string, string>> parameters)
        {
            var text = GcsQueryParameters.Value(parameters, "sourceGeneration");
            var isNumber = long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number);

            return GcsJsonConditions.Read(parameters) is { } conditions && GcsJsonConditions.ReadSource(parameters) is { } sourceConditions && (text is null || isNumber)
                ? new CopyQuery(conditions, sourceConditions, isNumber ? number : null)
                : null;
        }
    }

    // The source's stored fields, or, when the request has a body, the metadata the body names (the type falling back to the source's).
    private static async Task<(ObjectMetadata? Metadata, GcsJsonError? Refusal)> MetadataAsync(HttpRequest request, ObjectProperties found, CancellationToken cancellationToken)
    {
        if (!request.Body.IsAnnounced)
        {
            return (new ObjectMetadata
            {
                ContentType = found.ContentType,
                ContentEncoding = found.ContentEncoding,
                ContentDisposition = found.ContentDisposition,
                CacheControl = found.CacheControl,
                ContentLanguage = found.ContentLanguage,
                UserMetadata = found.UserMetadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            }, null);
        }

        using var body = new GcsBoundedBodyStream(GcsJsonObjectUpdate.MaxBodyBytes);
        var outcome = await request.Body.CopyToAsync(body, cancellationToken);
        if (body.IsTooLarge || outcome != HttpRequestBodyDiscardOutcome.Discarded || GcsJsonObjectFields.Read(body.ToArray()) is not { } fields)
        {
            return (null, GcsJsonError.Invalid);
        }

        var named = fields.Compose(null);

        return (named with { ContentType = named.ContentType ?? found.ContentType }, null);
    }
}
