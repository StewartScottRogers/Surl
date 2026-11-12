using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Decides the answer to each Data Lake endpoint request on one <c>abfs</c> or <c>abfss</c>
/// connection (ADR-0109): Path - Read, Path - Create, Path - Update's <c>append</c> and
/// <c>flush</c>, Path - Delete and Path - List, made on the content store as the caller the login
/// names, and the pages' own refusal, in the Data Lake JSON error body, for every other request.
/// </summary>
/// <remarks>
/// The order is decision 3.1's: the required headers, then the login, then the operation
/// (<see cref="AzureDataLakeOperationRecognition"/>), the filesystem (<c>404 FilesystemNotFound</c>),
/// the path's length (<c>400 InvalidResourceName</c>), and then the operation itself.
/// </remarks>
internal sealed class AzureDataLakeRequestAnswerer : IHttpRequestHandler, IHttpHeadNotReadRefuser
{
    private static readonly KeyValuePair<string, string> FileResourceField = new("x-ms-resource-type", "file");

    private readonly ContentStore store;
    private readonly ExchangeContext context;
    private readonly AzureLoginJudge login;
    private readonly AzurePathStyle pathStyle;
    private readonly AzureBlobPropertiesReader properties;
    private readonly AzurePathCreate create;
    private readonly AzurePathUpdate update;
    private readonly AzurePathList list;
    private readonly AzureStagingArea staging;
    private readonly AzurePathRename rename;
    private readonly AzurePathSetProperties setProperties;
    private readonly AzureFilesystemList listFilesystems;

    /// <summary>
    /// Creates the answerer for one exchange.
    /// </summary>
    /// <param name="store">The content store whose top-level folders are the filesystems.</param>
    /// <param name="index">The object metadata index of the store's served root.</param>
    /// <param name="authentication">The connection's authentication session.</param>
    /// <param name="context">The exchange: its scheme, clock and log.</param>
    /// <param name="staging">The server's staged appends.</param>
    /// <param name="pathStyle">Removes a path-style request's account segment.</param>
    public AzureDataLakeRequestAnswerer(ContentStore store, ObjectMetadataIndex index, IHttpAuthenticationSession authentication, ExchangeContext context, AzureStagingArea staging, AzurePathStyle pathStyle)
    {
        this.store = store;
        this.pathStyle = pathStyle;
        this.context = context;
        this.staging = staging;
        login = new AzureLoginJudge(authentication, context.Scheme, AzureStorageProtocolServer.PeerAddressOf(context));
        properties = new AzureBlobPropertiesReader(store, index, context.Log);
        create = new AzurePathCreate(store, index, properties, staging, context.Log);
        update = new AzurePathUpdate(store, index, properties, staging, context.Log);
        list = new AzurePathList(store, properties);
        rename = new AzurePathRename(store, index, properties, staging, context.Log);
        setProperties = new AzurePathSetProperties(store, index, properties, context.Log);
        listFilesystems = new AzureFilesystemList(store);
    }

    /// <inheritdoc/>
    /// <remarks>A head past <c>--max-request-head</c> is <c>400 InvalidInput</c> in the JSON error body (ADR-0109, decision 7).</remarks>
    public HttpAnswer RefuseHeadNotRead(HttpRequestHeadReadOutcome outcome, HttpAnswer standardRefusal) =>
        AzureReply.HeadNotReadRefusal(outcome, standardRefusal, context.TimeProvider.GetUtcNow(), context.Identifiers, writesJsonErrors: true);

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var (target, pathStyleAccount) = pathStyle.Read(request);
        var reply = new AzureReply(request, AzureReply.NewRequestId(context.Identifiers), context.TimeProvider.GetUtcNow(), writesJsonErrors: true);
        if (AzureRequestChecks.CheckRequiredHeaders(request.Fields) is { } headerRefusal)
        {
            return reply.Error(headerRefusal);
        }

        var loggedIn = await login.LogInAsync(request, target, pathStyleAccount, reply, cancellationToken);
        if (loggedIn.Refusal is not null)
        {
            return loggedIn.Refusal;
        }

        // A chosen-error fault rule answers in place of the operation, before the content store
        // is touched (ADR-0124, decisions 4 and 9).
        if (await AzureRequestFaults.FindChosenErrorAsync(context, request.Method, target.Path, loggedIn.Caller!.AccountName) is { } chosenError)
        {
            return reply.Error(AzureRefusal.Of(chosenError));
        }

        if (AzureDataLakeOperationRecognition.Recognize(request, target, out var operation) is { } operationRefusal)
        {
            context.Log.Note($"{request.Method} {target.Path} is no operation the Azure Data Lake server serves; answered {operationRefusal.Error.Code}.");

            return reply.Error(operationRefusal);
        }

        return await AnswerOperationAsync(reply, request, target, operation, loggedIn.Caller!, cancellationToken);
    }

    // Filesystem - List, which names no filesystem; else the filesystem, then the path's length,
    // then the operation.
    private async Task<HttpAnswer> AnswerOperationAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, AzureDataLakeOperation operation, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (operation == AzureDataLakeOperation.ListFilesystems)
        {
            return listFilesystems.Answer(reply, target, caller, cancellationToken);
        }

        var filesystem = store.MapBucketRequestPathForCaller($"/{target.RawContainer}/", caller);
        if (!filesystem.IsMapped || store.GetEntryKind(filesystem) != ContentEntryKind.Directory)
        {
            return reply.Error(AzureRefusal.Of(AzureError.FilesystemNotFound));
        }

        if (target.BlobName.Length > AzureBlobRequestAnswerer.MaxBlobNameLength)
        {
            return reply.Error(AzureRefusal.Of(AzureError.InvalidResourceName));
        }

        return operation switch
        {
            AzureDataLakeOperation.ListPaths => await list.AnswerAsync(reply, target, caller, cancellationToken),
            AzureDataLakeOperation.CreatePath => await create.AnswerAsync(reply, request, target, caller, cancellationToken),
            AzureDataLakeOperation.UpdatePath => await update.AnswerAsync(reply, request, target, caller, cancellationToken),
            AzureDataLakeOperation.DeletePath => await DeletePathAsync(reply, target, caller, cancellationToken),
            AzureDataLakeOperation.RenamePath => await rename.AnswerAsync(reply, request, target, caller, cancellationToken),
            AzureDataLakeOperation.SetPathProperties => await setProperties.AnswerAsync(reply, request, target, caller, cancellationToken),
            AzureDataLakeOperation.GetFilesystemProperties => AzureFolderProperties.Filesystem(reply, store.GetEntryStatus(filesystem)!.LastModifiedUtc),
            _ => await ReadPathAsync(reply, request, target, caller, cancellationToken),
        };
    }

    // Path - Read and Path - Get Properties: a file's bytes and head, with its x-ms-properties; a
    // directory's head on a HEAD, and 409 ResourceTypeMismatch on a GET.
    private async Task<HttpAnswer> ReadPathAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        if (mapping.IsMapped && store.GetEntryStatus(mapping) is { Kind: ContentEntryKind.Directory } directory)
        {
            return reply.IsHead ? AzureFolderProperties.Directory(reply, directory.LastModifiedUtc) : reply.Error(AzureRefusal.Of(AzureError.ResourceTypeMismatch));
        }

        var found = mapping.IsMapped
            ? await properties.ReadAsync(mapping, AzureIndexKey.OfRaw(store, target.RawContainer, target.RawBlob), cancellationToken)
            : null;

        return found is null
            ? reply.Error(AzureRefusal.Of(AzureError.PathNotFound))
            : await AzureBlobAnswer.AnswerAsync(reply, request.Fields, found, new HttpAnswerBody.ContentFile(store, mapping, ContentByteRange.WholeFile(found.Length)), FileFieldsOf(found), cancellationToken);
    }

    // x-ms-resource-type: file, then x-ms-properties when the file has any ("Path - Get Properties").
    private static List<KeyValuePair<string, string>> FileFieldsOf(ObjectProperties found) =>
        AzurePropertiesField.Format(found.UserMetadata) is { } properties
            ? [FileResourceField, new(AzurePropertiesField.Name, properties)]
            : [FileResourceField];

    // Path - Delete ("Path - Delete"; decision 5.5): a file, an empty directory, or with
    // recursive=true a directory and all it holds; 200. Uploads off is 403.
    private async Task<HttpAnswer> DeletePathAsync(AzureReply reply, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch));
        }

        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        var kind = mapping.IsMapped ? store.GetEntryKind(mapping) : ContentEntryKind.None;
        var isRecursive = string.Equals(AzureDataLakeOperationRecognition.Parameter(target, "recursive"), "true", StringComparison.OrdinalIgnoreCase);
        var deleted = kind switch
        {
            ContentEntryKind.File => store.DeleteFile(mapping),
            ContentEntryKind.Directory => DeleteDirectory(mapping, $"/{target.RawContainer}/{target.RawBlob.TrimEnd('/')}/", isRecursive, caller, cancellationToken),
            _ => ContentChangeResult.Absent,
        };
        if (deleted == ContentChangeResult.Done)
        {
            await staging.DiscardInTurnAsync(AzurePathUpdate.KeyOf(target), cancellationToken);
        }

        return deleted switch
        {
            ContentChangeResult.Done => reply.Answer(HttpStatus.Ok, [new("Content-Length", "0")], HttpAnswerBody.None),
            ContentChangeResult.NotEmpty => reply.Error(AzureRefusal.Of(AzureError.DirectoryNotEmpty)),
            ContentChangeResult.NotPermitted => reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch)),
            _ => reply.Error(AzureRefusal.Of(AzureError.PathNotFound)),
        };
    }

    // A directory's entries first when recursive, deepest first; then the directory, which is
    // NotEmpty when anything is left in it.
    private ContentChangeResult DeleteDirectory(ContentPathMapping mapping, string folderPath, bool isRecursive, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (isRecursive)
        {
            foreach (var path in new AzureFilesystemWalk(store, caller).Paths(folderPath, string.Empty, isRecursive: true, after: null, cancellationToken).Reverse())
            {
                _ = path.IsDirectory ? store.RemoveEmptyDirectory(path.Mapping) : store.DeleteFile(path.Mapping);
            }
        }

        return store.RemoveEmptyDirectory(mapping);
    }
}
