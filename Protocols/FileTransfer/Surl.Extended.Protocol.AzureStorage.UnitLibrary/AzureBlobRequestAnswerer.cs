using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Decides the answer to each Blob endpoint request on one connection (ADR-0109): Get Blob, Get
/// Blob Properties, Put Blob and Delete Blob on <c>/&lt;container&gt;/&lt;blob&gt;</c> and List Blobs
/// on <c>/&lt;container&gt;?restype=container&amp;comp=list</c>, made on the content store as the
/// caller the login names, and the pages' own refusal for every other request.
/// </summary>
/// <remarks>
/// The order is decision 3.1's: the required headers, then the login, then the operation
/// (<see cref="AzureOperationRecognition"/>), the container (<c>404 ContainerNotFound</c>), the
/// name's length (<c>400 InvalidResourceName</c>), and then the operation itself.
/// </remarks>
internal sealed class AzureBlobRequestAnswerer : IHttpRequestHandler
{
    /// <summary>
    /// The longest blob name, in characters ("Naming and Referencing Containers, Blobs, and Metadata").
    /// </summary>
    public const int MaxBlobNameLength = 1024;

    /// <summary>
    /// <c>201 Created</c>, Put Blob's success.
    /// </summary>
    public static readonly HttpStatus Created = new(201, "Created");

    /// <summary>
    /// <c>202 Accepted</c>, Delete Blob's success.
    /// </summary>
    public static readonly HttpStatus Accepted = new(202, "Accepted");

    private readonly ContentStore store;
    private readonly AzureLoginJudge login;
    private readonly ExchangeContext context;
    private readonly AzureBlobPropertiesReader properties;
    private readonly AzurePutBlob putBlob;
    private readonly AzureListBlobs listBlobs;

    /// <summary>
    /// Creates the answerer for one exchange.
    /// </summary>
    /// <param name="store">The content store whose top-level folders are the containers.</param>
    /// <param name="index">The object metadata index of the store's served root.</param>
    /// <param name="authentication">The connection's authentication session.</param>
    /// <param name="context">The exchange: its clock and log.</param>
    /// <param name="endpointScheme">How the connection is served, <c>https</c> or <c>http</c>.</param>
    public AzureBlobRequestAnswerer(ContentStore store, ObjectMetadataIndex index, IHttpAuthenticationSession authentication, ExchangeContext context, string endpointScheme)
    {
        this.store = store;
        login = new AzureLoginJudge(authentication, AzureStorageProtocolServer.BlobScheme);
        this.context = context;
        properties = new AzureBlobPropertiesReader(store, index, context.Log);
        putBlob = new AzurePutBlob(store, index, context.Log);
        listBlobs = new AzureListBlobs(store, properties, endpointScheme);
    }

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var target = AzureBlobTarget.Parse(request.Target);
        var reply = new AzureReply(request, AzureReply.NewRequestId(), context.TimeProvider.GetUtcNow());
        if (AzureRequestChecks.CheckRequiredHeaders(request.Fields) is { } headerRefusal)
        {
            return reply.Error(headerRefusal);
        }

        var loggedIn = await login.LogInAsync(request, reply, cancellationToken);
        if (loggedIn.Refusal is not null)
        {
            return loggedIn.Refusal;
        }

        if (AzureOperationRecognition.Recognize(request, target, out var operation) is { } operationRefusal)
        {
            context.Log.Note($"{request.Method} {target.Path} is no operation the Azure Blob server serves; answered {operationRefusal.Error.Code}.");

            return reply.Error(operationRefusal);
        }

        return await AnswerOperationAsync(reply, request, target, operation, loggedIn.Caller!, cancellationToken);
    }

    // The container, then the name's length, then the operation.
    private async Task<HttpAnswer> AnswerOperationAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, AzureOperation operation, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!IsContainer(store.MapBucketRequestPathForCaller($"/{target.RawContainer}/", caller)))
        {
            return reply.Error(AzureRefusal.Of(AzureError.ContainerNotFound));
        }

        if (target.BlobName.Length > MaxBlobNameLength)
        {
            return reply.Error(AzureRefusal.Of(AzureError.InvalidResourceName));
        }

        return operation switch
        {
            AzureOperation.ListBlobs => await listBlobs.AnswerAsync(reply, request, target, caller, cancellationToken),
            AzureOperation.PutBlob => await putBlob.AnswerAsync(reply, request, target, caller, cancellationToken),
            AzureOperation.DeleteBlob => DeleteBlob(reply, target, caller),
            _ => await ReadBlobAsync(reply, request, target, caller, cancellationToken),
        };
    }

    // Get Blob and Get Blob Properties of a blob in an existing container.
    private async Task<HttpAnswer> ReadBlobAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        var found = mapping.IsMapped
            ? await properties.ReadAsync(mapping, AzureIndexKey.OfRaw(store, target.RawContainer, target.RawBlob), cancellationToken)
            : null;

        return found is null
            ? reply.Error(AzureRefusal.Of(AzureError.BlobNotFound))
            : await AzureBlobAnswer.AnswerAsync(reply, request.Fields, found, new HttpAnswerBody.ContentFile(store, mapping, ContentByteRange.WholeFile(found.Length)), cancellationToken);
    }

    // Delete Blob ("Delete Blob"; decision 5.6): 202; a missing blob 404 BlobNotFound; with uploads
    // off or share access refusing the write, 403 AuthorizationPermissionMismatch.
    private HttpAnswer DeleteBlob(AzureReply reply, AzureBlobTarget target, ContentCaller caller)
    {
        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        var deleted = mapping.IsMapped ? store.DeleteFile(mapping) : ContentChangeResult.Absent;

        return deleted switch
        {
            ContentChangeResult.Done => reply.Answer(Accepted, [new("Content-Length", "0")], HttpAnswerBody.None),
            ContentChangeResult.NotPermitted => reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch)),
            _ => reply.Error(AzureRefusal.Of(AzureError.BlobNotFound)),
        };
    }

    private bool IsContainer(ContentPathMapping container) =>
        container.IsMapped && store.GetEntryKind(container) == ContentEntryKind.Directory;
}
