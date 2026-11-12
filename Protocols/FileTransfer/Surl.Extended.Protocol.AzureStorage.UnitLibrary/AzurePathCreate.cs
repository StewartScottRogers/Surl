using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers Path - Create ("Path - Create"; ADR-0109, decisions 2 and 5.5): <c>resource=directory</c>
/// creates the directory and every missing parent; <c>resource=file</c> creates every missing parent
/// and the file empty, or empties an existing one, discards its staged appends, and keeps its
/// <c>x-ms-content-*</c>, <c>x-ms-cache-control</c> and <c>x-ms-properties</c> fields in the object
/// metadata index. Both answer <c>201</c> with <c>ETag</c> and <c>Last-Modified</c>.
/// </summary>
/// <remarks>
/// Needs <c>--allow-uploads</c> (off: <c>403 AuthorizationPermissionMismatch</c>). Before anything
/// changes, an <c>x-ms-properties</c> that does not read is <c>400 InvalidHeaderValue</c>, and the
/// conditional fields are checked against the entry at the name (ADR-0109, decision 1):
/// <c>If-None-Match: *</c> on an existing file or directory is <c>409 PathAlreadyExists</c>, any other
/// failing condition <c>412 ConditionNotMet</c>. A file in the way
/// of a parent, a file created over a directory or a directory over a file, is <c>409
/// PathConflict</c>; a name the store cannot hold is <c>400 InvalidResourceName</c>.
/// </remarks>
/// <param name="store">The content store the path is created in.</param>
/// <param name="index">The object metadata index of the store's served root.</param>
/// <param name="properties">Reads the existing file's ETag the conditional fields are checked against.</param>
/// <param name="staging">The server's staged appends, a re-created file's discarded.</param>
/// <param name="log">Where a failure to rewrite the index file is noted.</param>
internal sealed class AzurePathCreate(ContentStore store, ObjectMetadataIndex index, AzureBlobPropertiesReader properties, AzureStagingArea staging, IExchangeLog log)
{
    // MD5 of no bytes, quoted: the ETag of an empty file the index does not key.
    private const string EmptyFileETag = "\"d41d8cd98f00b204e9800998ecf8427e\"";

    /// <summary>
    /// Answers a Path - Create on a path in an existing filesystem.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose header fields a file keeps.</param>
    /// <param name="target">The request's filesystem, path and <c>resource</c>.</param>
    /// <param name="caller">Who writes, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels writing the file and the index.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch));
        }

        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        if (!mapping.IsMapped)
        {
            return reply.Error(AzureRefusal.Of(AzureError.InvalidResourceName));
        }

        var sentProperties = AzureRequestFields.Value(request.Fields, AzurePropertiesField.Name) ?? string.Empty;
        if (AzurePropertiesField.Parse(sentProperties) is not { } userMetadata)
        {
            return reply.Error(AzureRefusal.InvalidHeaderValue(AzurePropertiesField.Name, sentProperties));
        }

        if (await ConditionRefusalAsync(request, target, mapping, cancellationToken) is { } conditionRefusal)
        {
            return reply.Error(AzureRefusal.Of(conditionRefusal));
        }

        var isDirectory = AzureDataLakeOperationRecognition.Parameter(target, "resource") == "directory";
        var segments = target.RawBlob.TrimEnd('/').Split('/');
        var folderCount = isDirectory ? segments.Length : segments.Length - 1;
        if (CreateFolders(target.RawContainer, segments[..folderCount], caller) is { } folderRefusal)
        {
            return reply.Error(AzureRefusal.Of(folderRefusal));
        }

        return isDirectory
            ? Created(reply, DirectoryETag(store.GetEntryStatus(mapping)!.LastModifiedUtc), store.GetEntryStatus(mapping)!.LastModifiedUtc)
            : await CreateFileAsync(reply, request, target, mapping, userMetadata, cancellationToken);
    }

    /// <summary>
    /// What the conditional fields make of the entry at a Data Lake path (ADR-0109, decision 1):
    /// <c>If-None-Match: *</c> on an existing file or directory is <c>409 PathAlreadyExists</c>, any
    /// other failing condition <c>412 ConditionNotMet</c>. A directory's ETag is <see cref="DirectoryETag"/>.
    /// </summary>
    /// <param name="store">The content store the path is in.</param>
    /// <param name="properties">Reads a file's ETag.</param>
    /// <param name="request">The request, whose fields hold the conditions.</param>
    /// <param name="target">The request's filesystem and path.</param>
    /// <param name="mapping">The path's mapped location.</param>
    /// <param name="cancellationToken">Cancels hashing the file.</param>
    /// <returns><see langword="null"/> when the write goes ahead, else its error.</returns>
    public static async Task<AzureError?> ConditionRefusalAsync(ContentStore store, AzureBlobPropertiesReader properties, HttpRequest request, AzureBlobTarget target, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        (string ETag, DateTimeOffset LastModifiedUtc)? existing = store.GetEntryStatus(mapping) switch
        {
            null => null,
            { Kind: ContentEntryKind.Directory } directory => (DirectoryETag(directory.LastModifiedUtc), directory.LastModifiedUtc),
            _ => await FileEntityAsync(store, properties, target, mapping, cancellationToken),
        };

        return AzureConditions.EntryWriteRefusal(request.Fields, existing, AzureError.PathAlreadyExists);
    }

    private Task<AzureError?> ConditionRefusalAsync(HttpRequest request, AzureBlobTarget target, ContentPathMapping mapping, CancellationToken cancellationToken) =>
        ConditionRefusalAsync(store, properties, request, target, mapping, cancellationToken);

    // A file's ETag and last write time; none when it went between the status and the read.
    private static async Task<(string ETag, DateTimeOffset LastModifiedUtc)?> FileEntityAsync(ContentStore store, AzureBlobPropertiesReader properties, AzureBlobTarget target, ContentPathMapping mapping, CancellationToken cancellationToken) =>
        await properties.ReadAsync(mapping, AzureIndexKey.OfRaw(store, target.RawContainer, target.RawBlob), cancellationToken) is { } file
            ? (file.ETag, file.LastWriteTimeUtc)
            : null;

    /// <summary>
    /// The ETag a directory answers with: its last write time's ticks in hexadecimal, as the
    /// service's <c>0x…</c> ETags are written.
    /// </summary>
    /// <param name="lastModified">The directory's last write time.</param>
    /// <returns>The quoted ETag.</returns>
    public static string DirectoryETag(DateTimeOffset lastModified) =>
        string.Create(CultureInfo.InvariantCulture, $"\"0x{lastModified.UtcTicks:X}\"");

    // Each folder in turn, created when missing; a file in the way is a conflict.
    private AzureError? CreateFolders(string rawContainer, string[] segments, ContentCaller caller)
    {
        for (var count = 1; count <= segments.Length; count++)
        {
            var folder = store.MapBucketRequestPathForCaller($"/{rawContainer}/{string.Join('/', segments[..count])}", caller);
            if (CreateFolder(folder) is { } refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    private AzureError? CreateFolder(ContentPathMapping folder) => folder.EntryKind switch
    {
        ContentEntryKind.Directory => null,
        ContentEntryKind.File => AzureError.PathConflict,
        _ => store.CreateDirectory(folder) == ContentChangeResult.Done ? null : AzureError.AuthorizationPermissionMismatch,
    };

    // The file, created or emptied as an upload of no bytes, then its header fields in the index.
    // A directory at the name is a conflict; any other refusal is the caller's permission.
    private async Task<HttpAnswer> CreateFileAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentPathMapping mapping, Dictionary<string, string> userMetadata, CancellationToken cancellationToken)
    {
        if (await store.WriteUploadAsync(mapping, Stream.Null, cancellationToken) != ContentUploadResult.Written)
        {
            return reply.Error(AzureRefusal.Of(store.GetEntryKind(mapping) == ContentEntryKind.Directory ? AzureError.PathConflict : AzureError.AuthorizationPermissionMismatch));
        }

        await staging.DiscardInTurnAsync(AzurePathUpdate.KeyOf(target), cancellationToken);
        var lastModified = store.GetFileStatus(mapping)!.LastModifiedUtc;
        if (AzureIndexKey.OfRaw(store, target.RawContainer, target.RawBlob) is not { } indexKey)
        {
            return Created(reply, EmptyFileETag, lastModified);
        }

        var stored = await index.StoreAsync(indexKey, MetadataOf(request.Fields, userMetadata), cancellationToken);
        if (stored.StorageFailure is { } failure)
        {
            log.Note($"The object metadata index could not be written: {failure}");
        }

        if (AzurePutBlob.MetadataRefusal(stored.Outcome) is { } refusal)
        {
            store.DeleteFile(mapping);
            return reply.Error(AzureRefusal.Of(refusal));
        }

        return Created(reply, stored.Properties!.ETag, lastModified);
    }

    // "Path - Create": x-ms-content-type, -encoding, -language, -disposition, x-ms-cache-control
    // and x-ms-properties.
    private static ObjectMetadata MetadataOf(IReadOnlyList<KeyValuePair<string, string>> fields, Dictionary<string, string> userMetadata) => new()
    {
        ContentType = AzureRequestFields.Value(fields, "x-ms-content-type"),
        ContentEncoding = AzureRequestFields.Value(fields, "x-ms-content-encoding"),
        ContentDisposition = AzureRequestFields.Value(fields, "x-ms-content-disposition"),
        CacheControl = AzureRequestFields.Value(fields, "x-ms-cache-control"),
        ContentLanguage = AzureRequestFields.Value(fields, "x-ms-content-language"),
        UserMetadata = userMetadata,
    };

    // 201 with the tag and the date, and no body ("Path - Create", "Response headers").
    private static HttpAnswer Created(AzureReply reply, string eTag, DateTimeOffset lastModified) =>
        reply.Answer(
            AzureBlobRequestAnswerer.Created,
            [
                new("ETag", eTag),
                new("Last-Modified", lastModified.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)),
                new("Content-Length", "0"),
            ],
            HttpAnswerBody.None);
}
