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
/// <param name="insert">Answers <c>objects.insert</c>, media and multipart.</param>
/// <param name="resumable">Answers <c>objects.insert</c>, resumable: opening, chunks, status checks and cancels.</param>
/// <param name="update">Answers <c>objects.patch</c> and <c>objects.update</c>.</param>
/// <param name="copy">Answers <c>objects.copy</c> and <c>objects.rewrite</c>.</param>
/// <param name="compose">Answers <c>objects.compose</c>.</param>
/// <param name="isOverTls">Whether the connection is TLS, for the links the resources carry.</param>
internal sealed class GcsJsonRequestAnswerer(ContentStore store, GcsObjectPropertiesReader properties, GcsListBuckets listBuckets, GcsLoginJudge login, ExchangeContext context, GcsJsonObjectInsert insert, GcsJsonResumableUpload resumable, GcsJsonObjectUpdate update, GcsJsonObjectCopy copy, GcsJsonObjectCompose compose, bool isOverTls)
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
            GcsJsonOperation.InsertObject or GcsJsonOperation.ResumeUpload or GcsJsonOperation.CancelUpload => await AnswerUploadAsync(reply, request, target, route, caller, resources, cancellationToken),
            GcsJsonOperation.DeleteObject => await DeleteObjectAsync(reply, request, target, route, caller, cancellationToken),
            _ => await ChangeOrAssembleAsync(reply, request, target, route, caller, resources, cancellationToken),
        };
    }

    // objects.patch and objects.update change metadata; objects.copy, rewrite and compose write a new generation from other objects.
    private Task<HttpAnswer> ChangeOrAssembleAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken) => route.Operation switch
    {
        GcsJsonOperation.CopyObject or GcsJsonOperation.RewriteObject => copy.AnswerAsync(reply, request, target, route, caller, resources, cancellationToken),
        GcsJsonOperation.ComposeObject => compose.AnswerAsync(reply, request, target, route, caller, resources, cancellationToken),
        _ => ChangeMetadataAsync(reply, request, target, route, caller, resources, cancellationToken),
    };

    // objects.insert: media and multipart in one request; resumable opened, resumed or cancelled.
    private Task<HttpAnswer> AnswerUploadAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken) => route.Operation switch
    {
        GcsJsonOperation.ResumeUpload => resumable.ResumeAsync(reply, request, target, route, caller, resources, cancellationToken),
        GcsJsonOperation.CancelUpload => resumable.CancelAsync(reply, target, route, cancellationToken),
        _ => InsertAsync(reply, request, target, route, caller, resources, cancellationToken),
    };

    // objects.insert by POST: resumable opens an upload, media and multipart are one request.
    private async Task<HttpAnswer> InsertAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken)
    {
        if (GcsQueryParameters.Value(target.Parameters, "uploadType") == "resumable")
        {
            return await resumable.OpenAsync(reply, request, target, route, caller, cancellationToken);
        }

        return await insert.AnswerAsync(reply, request, target, route, caller, resources, cancellationToken);
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
                objects.Add((file.Key, found, await properties.DigestsAsync(file.Mapping, found, cancellationToken)));
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

        var digests = await properties.DigestsAsync(mapping, found, cancellationToken);
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
        var (refusal, mapping, _) = await FindForWriteAsync(reply, request, target, route, caller, cancellationToken);

        // The file was there a moment ago, so a delete that is not done is one share access refuses.
        return refusal ?? (store.DeleteFile(mapping!) == ContentChangeResult.Done
            ? reply.Answer(NoContent, [], HttpAnswerBody.None)
            : reply.JsonError(GcsJsonError.Forbidden));
    }

    // objects.patch and objects.update: the object found and its conditions met as for a delete, then the body's metadata.
    private async Task<HttpAnswer> ChangeMetadataAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken)
    {
        var (refusal, mapping, found) = await FindForWriteAsync(reply, request, target, route, caller, cancellationToken);

        return refusal ?? await update.AnswerAsync(reply, request, route, mapping!, found!, route.Operation == GcsJsonOperation.PatchObject, resources, cancellationToken);
    }

    // What a write to an existing object needs: --allow-uploads, a query that is no media read, the object itself, and its conditions met.
    private async Task<(HttpAnswer? Refusal, ContentPathMapping? Mapping, ObjectProperties? Found)> FindForWriteAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return (reply.JsonError(GcsJsonError.Forbidden), default, null);
        }

        if (GcsJsonObjectQuery.Read(target.Parameters, isDownload: false) is not { IsMedia: false } query)
        {
            return (reply.JsonError(GcsJsonError.InvalidParameter), default, null);
        }

        var (mapping, found) = await FindObjectAsync(route, caller, query, cancellationToken);
        if (found is null)
        {
            return (reply.JsonError(GcsJsonError.NotFound), mapping, null);
        }

        return query.Conditions.Evaluate(request.Fields, found) == GcsConditionOutcome.Serve
            ? (null, mapping, found)
            : (reply.JsonError(GcsJsonError.ConditionNotMet), mapping, null);
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
