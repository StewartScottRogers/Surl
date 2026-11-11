using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers <c>storage.objects.compose</c> (ADR-0148, decision 7): one to 32 source objects of the bucket, in order, become one new
/// generation of the destination named in the path, stored with the body's <c>destination</c> metadata (<c>contentType</c>
/// <c>application/octet-stream</c> unless it names one).
/// </summary>
/// <remarks>
/// Needs <c>--allow-uploads</c>. A body without sources is <c>400 required</c>, past 32 or malformed <c>400 invalid</c>; a source that is
/// missing, unreadable to the caller or not at the <c>generation</c> named is <c>404</c>; one whose
/// <c>objectPreconditions.ifGenerationMatch</c> fails is <c>412</c>. The request's <c>ifGenerationMatch</c> and
/// <c>ifMetagenerationMatch</c> apply to the destination. The destination may be one of the sources.
/// </remarks>
/// <param name="store">The content store whose bucket is read and written.</param>
/// <param name="properties">Reads the sources.</param>
/// <param name="assembly">Writes the destination.</param>
internal sealed class GcsJsonObjectCompose(ContentStore store, GcsObjectPropertiesReader properties, GcsJsonObjectAssembly assembly)
{
    private const string DefaultContentType = "application/octet-stream";

    /// <summary>
    /// Answers one compose.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">Its target, whose query is read.</param>
    /// <param name="route">Its route, naming the bucket and the destination.</param>
    /// <param name="caller">Who reads and writes, on <c>gs</c>.</param>
    /// <param name="resources">Writes the answer.</param>
    /// <param name="cancellationToken">Cancels reading and writing.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.JsonError(GcsJsonError.Forbidden);
        }

        if (GcsJsonConditions.Read(target.Parameters) is not { } conditions)
        {
            return reply.JsonError(GcsJsonError.InvalidParameter);
        }

        var (composed, parseRefusal) = await ReadBodyAsync(request, cancellationToken);
        if (parseRefusal is not null)
        {
            return reply.JsonError(parseRefusal);
        }

        var (sources, refusal) = await FindSourcesAsync(composed!, route, caller, cancellationToken);
        if (refusal is not null)
        {
            return reply.JsonError(refusal);
        }

        var metadata = composed!.Destination?.Compose(null) ?? new ObjectMetadata();
        metadata = metadata with { ContentType = metadata.ContentType ?? DefaultContentType };
        var destination = new GcsJsonObjectAssembly.Destination(route.RawBucket, route.RawName, metadata, conditions);
        var (writeRefusal, stored, digests) = await assembly.WriteAsync(destination, sources!, caller, request.Fields, reply.Now, cancellationToken);

        return writeRefusal is not null
            ? reply.JsonError(writeRefusal)
            : reply.Json(resources.Object(Uri.UnescapeDataString(route.RawBucket), Uri.UnescapeDataString(route.RawName), stored!, digests!));
    }

    // The body, at most 1 MiB, as a compose request.
    private static async Task<(GcsJsonComposeRequest? Request, GcsJsonError? Refusal)> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var body = new GcsBoundedBodyStream(GcsJsonObjectUpdate.MaxBodyBytes);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(body, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;

        return body.IsTooLarge || outcome != HttpRequestBodyDiscardOutcome.Discarded
            ? (null, GcsJsonError.Invalid)
            : GcsJsonComposeRequest.Read(body.ToArray());
    }

    // Each source in the bucket, as the caller may read it, at the generation named, meeting its own precondition.
    private async Task<(List<GcsJsonObjectAssembly.SourceObject>? Sources, GcsJsonError? Refusal)> FindSourcesAsync(GcsJsonComposeRequest composed, GcsJsonRoute route, ContentCaller caller, CancellationToken cancellationToken)
    {
        var sources = new List<GcsJsonObjectAssembly.SourceObject>();
        foreach (var source in composed.Sources)
        {
            var rawName = Uri.EscapeDataString(source.Name).Replace("%2F", "/", StringComparison.Ordinal);
            var mapping = store.MapBucketRequestPathForCaller($"/{route.RawBucket}/{rawName}", caller);
            var found = mapping.IsMapped ? await properties.ReadAsync(mapping, GcsIndexKey.OfRaw(store, route.RawBucket, rawName), cancellationToken) : null;
            if (found is null || (source.Generation is { } generation && generation != found.Generation))
            {
                return (null, GcsJsonError.NotFound);
            }

            if (source.IfGenerationMatch is { } match && match != found.Generation)
            {
                return (null, GcsJsonError.ConditionNotMet);
            }

            sources.Add(new GcsJsonObjectAssembly.SourceObject(mapping, found));
        }

        return (sources, null);
    }
}
