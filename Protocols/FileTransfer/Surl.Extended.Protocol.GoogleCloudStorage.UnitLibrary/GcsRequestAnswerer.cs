using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Decides the answer to each Cloud Storage XML API request on one connection (ADR-0110): List
/// Buckets, List Objects, GET Object, HEAD Object, PUT Object and DELETE Object, path style or
/// virtual-hosted style, made on the content store as the caller the login names, and
/// <c>400 NotImplemented</c> for every other request.
/// </summary>
/// <remarks>
/// The order is decision 3.1's: the addressing, the login (<see cref="GcsLoginJudge"/>), the
/// operation (<see cref="GcsOperationRecognition"/>), the bucket (<c>404 NoSuchBucket</c>), the
/// name's length (<c>400 KeyTooLong</c>), and then the operation itself.
/// </remarks>
internal sealed class GcsRequestAnswerer : IHttpRequestHandler
{
    private static readonly HttpStatus NoContent = new(204, "No Content");

    private readonly ContentStore store;
    private readonly ExchangeContext context;
    private readonly GcsLoginJudge login;
    private readonly GcsObjectPropertiesReader properties;
    private readonly GcsPutObject putObject;
    private readonly GcsListObjects listObjects;
    private readonly GcsListBuckets listBuckets;

    /// <summary>
    /// Creates the answerer for one exchange.
    /// </summary>
    /// <param name="store">The content store whose top-level folders are the buckets.</param>
    /// <param name="index">The object metadata index of the store's served root.</param>
    /// <param name="authentication">The connection's authentication session.</param>
    /// <param name="context">The exchange: its listen URL, clock and log.</param>
    public GcsRequestAnswerer(ContentStore store, ObjectMetadataIndex index, IHttpAuthenticationSession authentication, ExchangeContext context)
    {
        this.store = store;
        this.context = context;
        login = new GcsLoginJudge(authentication, context.Log);
        properties = new GcsObjectPropertiesReader(store, index, context.Log);
        putObject = new GcsPutObject(store, index, properties, login, context.Log);
        listObjects = new GcsListObjects(store, properties);
        listBuckets = new GcsListBuckets(store);
    }

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var target = GcsRequestTarget.Parse(request.Target, GcsVirtualHost.BucketOf(request.Authority, context.ListenUrl.Host));
        var reply = new GcsReply(request, context.TimeProvider.GetUtcNow());
        var loggedIn = await login.LogInAsync(request, reply, cancellationToken);
        if (loggedIn.Refusal is { } refusal)
        {
            return refusal;
        }

        var operation = GcsOperationRecognition.Of(request, target);
        if (await UnusedBodyRefusalAsync(request, operation, loggedIn.BodyCheck, cancellationToken) is { } bodyRefusal)
        {
            return reply.Error(bodyRefusal);
        }

        var caller = loggedIn.Caller!;

        return operation switch
        {
            GcsOperation.NotImplemented => NotImplemented(reply, request),
            GcsOperation.ListBuckets => listBuckets.Answer(reply, caller, cancellationToken),
            _ => await AnswerInBucketAsync(reply, request, target, operation, loggedIn, cancellationToken),
        };
    }

    // A signature binding a body that only PUT Object reads (ADR-0045): any other request reads
    // and discards it first, so its hash is checked before anything is answered.
    private async Task<GcsError?> UnusedBodyRefusalAsync(HttpRequest request, GcsOperation operation, IHttpRequestBodyCheck? bodyCheck, CancellationToken cancellationToken) =>
        operation == GcsOperation.PutObject || bodyCheck is null
            ? null
            : await login.JudgeDiscardedBodyAsync(request, bodyCheck, cancellationToken);

    private HttpAnswer NotImplemented(GcsReply reply, HttpRequest request)
    {
        context.Log.Note($"{request.Method} is no operation the Cloud Storage server serves; answered 400 NotImplemented.");

        return reply.Error(GcsError.NotImplemented);
    }

    // The bucket, then the name's length, then the operation.
    private async Task<HttpAnswer> AnswerInBucketAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsOperation operation, GcsLogin loggedIn, CancellationToken cancellationToken)
    {
        var caller = loggedIn.Caller!;
        var bucket = store.MapBucketRequestPathForCaller($"/{target.RawBucket}/", caller);
        if (!bucket.IsMapped || store.GetEntryKind(bucket) != ContentEntryKind.Directory)
        {
            return reply.Error(GcsError.NoSuchBucket);
        }

        if (GcsObjectName.IsTooLong(target.RawName))
        {
            return reply.Error(GcsError.KeyTooLong);
        }

        return operation switch
        {
            GcsOperation.ListObjects => await listObjects.AnswerAsync(reply, target, caller, cancellationToken),
            GcsOperation.PutObject => await putObject.AnswerAsync(reply, request, target, caller, loggedIn.BodyCheck, cancellationToken),
            GcsOperation.DeleteObject => await DeleteObjectAsync(reply, request, target, caller, cancellationToken),
            _ => await ReadObjectAsync(reply, request, target, caller, cancellationToken),
        };
    }

    // GET Object and HEAD Object: the object, then the generation it names, then the answer.
    private async Task<HttpAnswer> ReadObjectAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        var mapping = store.MapBucketRequestPathForCaller(target.ObjectPath, caller);
        var found = mapping.IsMapped
            ? await properties.ReadAsync(mapping, GcsIndexKey.OfRaw(store, target.RawBucket, target.RawName), cancellationToken)
            : null;
        if (found is null)
        {
            return reply.Error(GcsError.NoSuchKey);
        }

        if (GenerationRefusal(GcsQueryParameters.Value(target.Parameters, "generation"), found) is { } generationRefusal)
        {
            return reply.Error(generationRefusal);
        }

        var digests = await properties.HashAsync(mapping, found.Length, cancellationToken);

        return GcsObjectAnswer.Answer(reply, request.Fields, found, digests, new HttpAnswerBody.ContentFile(store, mapping, ContentByteRange.WholeFile(found.Length)));
    }

    // DELETE Object ("DELETE Object"; decision 1): 204; a missing object 404 NoSuchKey; with
    // uploads off or share access refusing the write, 403 AccessDenied.
    private async Task<HttpAnswer> DeleteObjectAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(GcsError.AccessDenied);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.ObjectPath, caller);
        if (!mapping.IsMapped || store.GetFileStatus(mapping) is null)
        {
            return reply.Error(GcsError.NoSuchKey);
        }

        if (GcsGenerationConditions.AreGiven(request.Fields)
            && GcsGenerationConditions.Check(request.Fields, await properties.ReadAsync(mapping, GcsIndexKey.OfRaw(store, target.RawBucket, target.RawName), cancellationToken)) is { } conditionRefusal)
        {
            return reply.Error(conditionRefusal);
        }

        // The file was there a moment ago, so a delete that is not done is one share access refuses.
        return store.DeleteFile(mapping) == ContentChangeResult.Done
            ? reply.Answer(NoContent, [], HttpAnswerBody.None)
            : reply.Error(GcsError.AccessDenied);
    }

    // The generation query parameter: a number, and the object's current one (decision 1).
    private static GcsError? GenerationRefusal(string? generationText, ObjectProperties found)
    {
        if (generationText is null)
        {
            return null;
        }

        if (!long.TryParse(generationText, NumberStyles.None, CultureInfo.InvariantCulture, out var generation))
        {
            return GcsError.InvalidArgument;
        }

        return generation == found.Generation ? null : GcsError.NoSuchKey;
    }
}
