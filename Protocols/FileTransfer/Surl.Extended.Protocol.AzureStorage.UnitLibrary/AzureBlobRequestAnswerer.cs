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
    /// The challenge every <c>401</c> carries ("Authorize with Microsoft Entra ID", "Bearer Challenge").
    /// </summary>
    public const string BearerChallenge = "Bearer authorization_uri=https://login.microsoftonline.com/common/oauth2/authorize resource_id=https://storage.azure.com";

    /// <summary>
    /// <c>201 Created</c>, Put Blob's success.
    /// </summary>
    public static readonly HttpStatus Created = new(201, "Created");

    /// <summary>
    /// <c>202 Accepted</c>, Delete Blob's success.
    /// </summary>
    public static readonly HttpStatus Accepted = new(202, "Accepted");

    private readonly ContentStore store;
    private readonly IHttpAuthenticationSession authentication;
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
        this.authentication = authentication;
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

        var login = await LogInAsync(request, reply, cancellationToken);
        if (login.Refusal is not null)
        {
            return login.Refusal;
        }

        if (AzureOperationRecognition.Recognize(request, target, out var operation) is { } operationRefusal)
        {
            context.Log.Note($"{request.Method} {target.Path} is no operation the Azure Blob server serves; answered {operationRefusal.Error.Code}.");

            return reply.Error(operationRefusal);
        }

        return await AnswerOperationAsync(reply, request, target, operation, login.Caller!, cancellationToken);
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

    // Decision 3: another Authorization scheme is 400; a bearer request needs 2017-11-09 or later;
    // then the session judges. A refusal, and an anonymous write, is 401 with the Bearer challenge.
    private async Task<AzureLogin> LogInAsync(HttpRequest request, AzureReply reply, CancellationToken cancellationToken)
    {
        var authorization = AzureRequestFields.Value(request.Fields, "Authorization");
        if (AuthorizationRefusal(authorization, request.Fields) is { } refusal)
        {
            return AzureLogin.Refused(reply.Error(refusal));
        }

        var isWrite = request.Method is not ("GET" or "HEAD");
        var verdict = await authentication.JudgeAsync(new HttpAuthenticationRequest(request.Method, request.Target, isWrite, request.Fields), cancellationToken);

        return IsServed(verdict, isWrite)
            ? AzureLogin.As(CallerOf(verdict.AccountName))
            : AzureLogin.Refused(ChallengeAnswer(reply, authorization));
    }

    // 401 with the Bearer challenge: NoAuthenticationInformation without credentials, else
    // InvalidAuthenticationInfo.
    private static HttpAnswer ChallengeAnswer(AzureReply reply, string? authorization) =>
        reply.Error(
            AzureRefusal.Of(authorization is null ? AzureError.NoAuthenticationInformation : AzureError.InvalidBearerToken),
            [new("WWW-Authenticate", BearerChallenge)]);

    // Before the session: an Authorization that is no bearer token, or a bearer request naming a
    // version before 2017-11-09.
    private static AzureRefusal? AuthorizationRefusal(string? authorization, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        if (authorization is null)
        {
            return null;
        }

        if (!IsBearer(authorization))
        {
            return AzureRefusal.Of(AzureError.MalformedAuthorization);
        }

        return AzureRequestChecks.PrecedesBearer(AzureRequestFields.Value(fields, "x-ms-version")!)
            ? AzureRefusal.AuthenticationFailed("Bearer authorization needs x-ms-version 2017-11-09 or later.")
            : null;
    }

    // A write needs a login; the bearer method logs in as the empty name, anonymous as none.
    private static bool IsServed(HttpAuthenticationVerdict verdict, bool isWrite) =>
        verdict.Outcome == HttpAuthenticationOutcome.Proceed && !(isWrite && verdict.AccountName is null);

    private static ContentCaller CallerOf(string? accountName) =>
        string.IsNullOrEmpty(accountName)
            ? ContentCaller.Anonymous(AzureStorageProtocolServer.BlobScheme)
            : ContentCaller.ForAccount(accountName, AzureStorageProtocolServer.BlobScheme);

    private static bool IsBearer(string authorization) =>
        authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && authorization[7..].Trim().Length > 0;

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
