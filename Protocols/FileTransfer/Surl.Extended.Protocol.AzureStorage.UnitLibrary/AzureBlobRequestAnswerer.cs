using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Decides the answer to each Blob endpoint request on one connection (ADR-0109): Get Blob and Get
/// Blob Properties on <c>/&lt;container&gt;/&lt;blob&gt;</c>, read from the content store as the
/// caller the login names, and the pages' own refusal for every other request.
/// </summary>
/// <remarks>
/// The order is decision 3.1's: the required headers, then the login, then the method
/// (<c>405 UnsupportedHttpVerb</c>), the query (<c>400 InvalidQueryParameterValue</c>), a path that
/// names no blob (<c>400 InvalidUri</c>), an unsupported header (<c>400 UnsupportedHeader</c>), the
/// container (<c>404 ContainerNotFound</c>), the name's length (<c>400 InvalidResourceName</c>) and
/// the blob (<c>404 BlobNotFound</c>).
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

    private readonly ContentStore store;
    private readonly ObjectMetadataIndex index;
    private readonly IHttpAuthenticationSession authentication;
    private readonly ExchangeContext context;

    /// <summary>
    /// Creates the answerer for one exchange.
    /// </summary>
    /// <param name="store">The content store whose top-level folders are the containers.</param>
    /// <param name="index">The object metadata index of the store's served root.</param>
    /// <param name="authentication">The connection's authentication session.</param>
    /// <param name="context">The exchange: its clock and log.</param>
    public AzureBlobRequestAnswerer(ContentStore store, ObjectMetadataIndex index, IHttpAuthenticationSession authentication, ExchangeContext context)
    {
        this.store = store;
        this.index = index;
        this.authentication = authentication;
        this.context = context;
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

        if (OperationRefusal(request, target) is { } operationRefusal)
        {
            context.Log.Note($"{request.Method} {target.Path} is no operation the Azure Blob server serves; answered {operationRefusal.Error.Code}.");

            return reply.Error(operationRefusal);
        }

        return await ReadBlobAsync(reply, request, target, login.Caller!, cancellationToken);
    }

    // The method, the query, a path naming no blob, then the unsupported headers.
    private static AzureRefusal? OperationRefusal(HttpRequest request, AzureBlobTarget target)
    {
        if (request.Method is not ("GET" or "HEAD"))
        {
            return AzureRefusal.Of(AzureError.UnsupportedHttpVerb);
        }

        if (AzureRequestChecks.CheckQuery(target) is { } queryRefusal)
        {
            return queryRefusal;
        }

        return target.NamesABlob
            ? AzureRequestChecks.CheckUnsupportedHeaders(request.Fields)
            : AzureRefusal.Of(AzureError.InvalidUri);
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

    // Get Blob and Get Blob Properties: the container, then the name's length, then the blob.
    private async Task<HttpAnswer> ReadBlobAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!IsContainer(store.MapBucketRequestPathForCaller($"/{target.RawContainer}/", caller)))
        {
            return reply.Error(AzureRefusal.Of(AzureError.ContainerNotFound));
        }

        if (target.BlobName.Length > MaxBlobNameLength)
        {
            return reply.Error(AzureRefusal.Of(AzureError.InvalidResourceName));
        }

        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        var properties = mapping.IsMapped ? await PropertiesAsync(mapping, target.Path, cancellationToken) : null;

        return properties is null
            ? reply.Error(AzureRefusal.Of(AzureError.BlobNotFound))
            : await AzureBlobAnswer.AnswerAsync(reply, request.Fields, properties, new HttpAnswerBody.ContentFile(store, mapping, ContentByteRange.WholeFile(properties.Length)), cancellationToken);
    }

    private bool IsContainer(ContentPathMapping container) =>
        container.IsMapped && store.GetEntryKind(container) == ContentEntryKind.Directory;

    // The index keys a file by its path relative to the served root; a file it does not find
    // there - one another protocol wrote, in a share or behind a bucket name - answers the
    // index's defaults (ADR-0100, decision 2.3).
    private async Task<ObjectProperties?> PropertiesAsync(ContentPathMapping mapping, string path, CancellationToken cancellationToken)
    {
        if (store.GetFileStatus(mapping) is not { } status)
        {
            return null;
        }

        var reading = await index.ReadAsync(Uri.UnescapeDataString(path[1..]), cancellationToken);
        if (reading.StorageFailure is { } failure)
        {
            context.Log.Note($"The object metadata index could not be written: {failure}");
        }

        return reading.Properties ?? await DefaultPropertiesAsync(mapping, status, cancellationToken);
    }

    private async Task<ObjectProperties> DefaultPropertiesAsync(ContentPathMapping mapping, ContentFileStatus status, CancellationToken cancellationToken)
    {
        using var md5 = MD5.Create();
        await using (var hashing = new CryptoStream(Stream.Null, md5, CryptoStreamMode.Write))
        {
            await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(status.Length), hashing, cancellationToken);
        }

        return new ObjectProperties(
            ObjectMetadataIndex.DefaultContentType, null, null, null, null, [], $"\"{Convert.ToHexStringLower(md5.Hash!)}\"", 0, status.Length, status.LastModifiedUtc);
    }
}
