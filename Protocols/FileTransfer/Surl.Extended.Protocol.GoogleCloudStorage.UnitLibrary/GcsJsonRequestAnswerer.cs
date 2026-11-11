using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Decides the answer to each Cloud Storage JSON API request on one connection (ADR-0148): <c>buckets.list</c>,
/// <c>buckets.get</c>, <c>objects.list</c>, <c>objects.get</c> (the resource or <c>alt=media</c>) and
/// <c>objects.delete</c>, made on the content store as the caller the login names, and <c>501 notImplemented</c> for
/// every other request on a JSON path.
/// </summary>
/// <remarks>
/// The order is decision 4's and 5's: the login (Bearer only; <see cref="GcsLoginJudge"/>), the operation (a request
/// no operation names is <c>501</c>), a chosen-error fault rule (<see cref="GcsJsonFaults"/>), the bucket
/// (<c>404 notFound</c>), the name's length (<c>400 invalid</c>), and then the operation itself.
/// </remarks>
/// <param name="store">The content store whose top-level folders are the buckets.</param>
/// <param name="properties">Reads what an object answers with.</param>
/// <param name="listBuckets">The enumeration of buckets both grammars share.</param>
/// <param name="login">The judge of the Bearer login, answering in JSON.</param>
/// <param name="context">The exchange: its listen URL, log and fault plan.</param>
/// <param name="isOverTls">Whether the connection is TLS, for the links the resources carry.</param>
internal sealed class GcsJsonRequestAnswerer(ContentStore store, GcsObjectPropertiesReader properties, GcsListBuckets listBuckets, GcsLoginJudge login, ExchangeContext context, bool isOverTls)
{
    private static readonly HttpStatus NoContent = new(204, "No Content");

    /// <summary>
    /// Answers one request on a JSON path.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="target">Its target, whose path begins one of the JSON prefixes.</param>
    /// <param name="reply">The request's answers.</param>
    /// <param name="cancellationToken">Cancels the store's work.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, GcsRequestTarget target, GcsReply reply, CancellationToken cancellationToken)
    {
        var route = GcsJsonRoute.Recognise(request.Method, target);
        var loggedIn = await login.LogInAsync(request, reply, cancellationToken);
        if (loggedIn.Refusal is { } refusal)
        {
            return refusal;
        }

        var caller = loggedIn.Caller!;
        if (!route.IsListed)
        {
            context.Log.Note($"{request.Method} is no operation the Cloud Storage JSON API serves here; answered 501 notImplemented.");

            return reply.JsonError(GcsJsonError.NotImplemented);
        }

        if (await GcsJsonFaults.FindChosenErrorAsync(context, route.MethodId, Uri.UnescapeDataString(target.Path), caller) is { } chosenError)
        {
            return reply.JsonError(chosenError);
        }

        var resources = new GcsJsonResources($"{(isOverTls ? "https" : "http")}://{request.Authority ?? context.ListenUrl.Host}");

        return route.Operation == GcsJsonOperation.ListBuckets
            ? ListBuckets(reply, target, caller, resources, cancellationToken)
            : await AnswerInBucketAsync(reply, request, target, route, caller, resources, cancellationToken);
    }

    // buckets.list: project is required and ignored; each top-level folder the caller may read.
    private HttpAnswer ListBuckets(GcsReply reply, GcsRequestTarget target, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(GcsQueryParameters.Value(target.Parameters, "project")))
        {
            return reply.JsonError(GcsJsonError.Required);
        }

        if (!IsJsonAlt(target))
        {
            return reply.JsonError(GcsJsonError.InvalidParameter);
        }

        return listBuckets.Entries(caller, cancellationToken) is { } buckets
            ? reply.Json(resources.Buckets(buckets))
            : reply.JsonError(GcsJsonError.Forbidden);
    }

    // The bucket, then the name's length, then the operation.
    private async Task<HttpAnswer> AnswerInBucketAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken)
    {
        var bucket = store.MapBucketRequestPathForCaller($"/{route.RawBucket}/", caller);
        if (!bucket.IsMapped || store.GetEntryStatus(bucket) is not { Kind: ContentEntryKind.Directory } folder)
        {
            return reply.JsonError(GcsJsonError.NotFound);
        }

        if (GcsObjectName.IsTooLong(route.RawName))
        {
            return reply.JsonError(GcsJsonError.Invalid);
        }

        return route.Operation switch
        {
            GcsJsonOperation.GetBucket => IsJsonAlt(target)
                ? reply.Json(resources.Bucket(Uri.UnescapeDataString(route.RawBucket), folder.LastModifiedUtc))
                : reply.JsonError(GcsJsonError.InvalidParameter),
            GcsJsonOperation.ListObjects => await ListObjectsAsync(reply, target, route, caller, resources, cancellationToken),
            GcsJsonOperation.GetObject => await GetObjectAsync(reply, request, target, route, caller, resources, cancellationToken),
            _ => await DeleteObjectAsync(reply, request, target, route, caller, cancellationToken),
        };
    }

    // objects.list: a listing needs --list-directories; prefix, delimiter, offsets, maxResults and pageToken.
    private async Task<HttpAnswer> ListObjectsAsync(GcsReply reply, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.ListDirectories)
        {
            return reply.JsonError(GcsJsonError.Forbidden);
        }

        if (!IsJsonAlt(target) || GcsJsonListRequest.Read(target.Parameters) is not { } request)
        {
            return reply.JsonError(GcsJsonError.InvalidParameter);
        }

        var files = new GcsBucketWalk(store, caller).Files(route.RawBucket, request.List.Prefix, cancellationToken).Where(file => request.Includes(file.Key));
        var page = GcsListPage.Take(files, request.List);
        var bucket = Uri.UnescapeDataString(route.RawBucket);
        var objects = new List<(string Name, ObjectProperties Properties, GcsObjectDigests Digests)>();
        foreach (var file in page.Contents)
        {
            if (await properties.ReadAsync(file.Mapping, GcsIndexKey.Of(store, bucket, file.Key), cancellationToken) is { } found)
            {
                objects.Add((file.Key, found, await properties.HashAsync(file.Mapping, found.Length, cancellationToken)));
            }
        }

        return reply.Json(resources.Objects(bucket, page, objects));
    }

    // objects.get: the object, the generation it names, the conditions, then the resource or the bytes.
    private async Task<HttpAnswer> GetObjectAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken)
    {
        if (GcsJsonObjectQuery.Read(target.Parameters, route.IsDownload) is not { } query)
        {
            return reply.JsonError(GcsJsonError.InvalidParameter);
        }

        var (mapping, found) = await FindObjectAsync(route, caller, query, cancellationToken);
        if (found is null)
        {
            return reply.JsonError(GcsJsonError.NotFound);
        }

        var outcome = query.Conditions.Evaluate(request.Fields, found);
        if (outcome != GcsConditionOutcome.Serve)
        {
            return ConditionRefusal(reply, outcome, found);
        }

        var digests = await properties.HashAsync(mapping, found.Length, cancellationToken);
        if (!query.IsMedia)
        {
            return reply.Json(resources.Object(Uri.UnescapeDataString(route.RawBucket), Uri.UnescapeDataString(route.RawName), found, digests));
        }

        var range = GcsRangeSelection.Select(GcsRequestFields.Value(request.Fields, "Range"), found.Length);

        return range is { IsSatisfiable: false }
            ? reply.JsonError(GcsJsonError.RangeNotSatisfiable)
            : GcsObjectAnswer.ServeObject(reply, found, digests, new HttpAnswerBody.ContentFile(store, mapping, ContentByteRange.WholeFile(found.Length)), range);
    }

    // objects.delete: 204; needs --allow-uploads; a missing object or generation 404; a failing condition 412.
    private async Task<HttpAnswer> DeleteObjectAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.JsonError(GcsJsonError.Forbidden);
        }

        if (GcsJsonObjectQuery.Read(target.Parameters, isDownload: false) is not { IsMedia: false } query)
        {
            return reply.JsonError(GcsJsonError.InvalidParameter);
        }

        var (mapping, found) = await FindObjectAsync(route, caller, query, cancellationToken);
        if (found is null)
        {
            return reply.JsonError(GcsJsonError.NotFound);
        }

        if (query.Conditions.Evaluate(request.Fields, found) != GcsConditionOutcome.Serve)
        {
            return reply.JsonError(GcsJsonError.ConditionNotMet);
        }

        // The file was there a moment ago, so a delete that is not done is one share access refuses.
        return store.DeleteFile(mapping) == ContentChangeResult.Done
            ? reply.Answer(NoContent, [], HttpAnswerBody.None)
            : reply.JsonError(GcsJsonError.Forbidden);
    }

    // The object, or none when it is not there, the store cannot hold its name, or the generation named is not its current one.
    private async Task<(ContentPathMapping Mapping, ObjectProperties? Found)> FindObjectAsync(GcsJsonRoute route, ContentCaller caller, GcsJsonObjectQuery query, CancellationToken cancellationToken)
    {
        var mapping = store.MapBucketRequestPathForCaller($"/{route.RawBucket}/{route.RawName}", caller);
        var found = mapping.IsMapped
            ? await properties.ReadAsync(mapping, GcsIndexKey.OfRaw(store, route.RawBucket, route.RawName), cancellationToken)
            : null;

        return (mapping, found is not null && (query.Generation is null || query.Generation == found.Generation) ? found : null);
    }

    private static HttpAnswer ConditionRefusal(GcsReply reply, GcsConditionOutcome outcome, ObjectProperties found) =>
        outcome == GcsConditionOutcome.PreconditionFailed
            ? reply.JsonError(GcsJsonError.ConditionNotMet)
            : reply.JsonError(
                GcsJsonError.NotModified,
                new KeyValuePair<string, string>("Last-Modified", found.LastWriteTimeUtc.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("ETag", found.ETag));

    // alt, where the operation has no bytes to serve, is json or absent.
    private static bool IsJsonAlt(GcsRequestTarget target) =>
        GcsQueryParameters.Value(target.Parameters, "alt") is null or "json";
}
