using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers Path - Create with <c>x-ms-rename-source</c> ("Path - Create"; ADR-0109, decision 1): the
/// file or directory the field names moves to the request's path through the content store, so
/// every protocol sees it under its new name at once. <c>201</c> with <c>ETag</c> and
/// <c>Last-Modified</c>.
/// </summary>
/// <remarks>
/// <para>
/// Needs <c>--allow-uploads</c> (off: <c>403 AuthorizationPermissionMismatch</c>). The field is
/// <c>/&lt;filesystem&gt;/&lt;path&gt;</c>, percent-encoded, with any query (a shared access
/// signature) ignored: one that names no path is <c>400 InvalidSourceUri</c>; no file or directory
/// there, or one the caller may not read, <c>404 SourcePathNotFound</c>. A directory renamed into
/// itself is <c>400 InvalidRenameSourcePath</c>; a destination whose parent directory does not
/// exist <c>404 RenameDestinationParentPathNotFound</c>; a directory at the destination, a file
/// there when the source is a directory, or any entry there with <c>If-None-Match: *</c>, <c>409
/// PathAlreadyExists</c>; a rename the store does not permit (a read-only share, a rename across
/// shares) <c>403 AuthorizationPermissionMismatch</c>. A file at the destination is otherwise
/// replaced, as the page's default says.
/// </para>
/// <para>
/// A renamed file keeps its stored header fields and properties: they are stored again under the
/// new name. Staged appends of either name are discarded. <c>mode</c> is accepted and changes
/// nothing: both its values rename the same way on one content store.
/// </para>
/// </remarks>
/// <param name="store">The content store the path is renamed in.</param>
/// <param name="index">The object metadata index of the store's served root.</param>
/// <param name="properties">Reads a renamed file's properties.</param>
/// <param name="staging">The server's staged appends.</param>
/// <param name="log">Where a failure to rewrite the index file is noted.</param>
internal sealed class AzurePathRename(ContentStore store, ObjectMetadataIndex index, AzureBlobPropertiesReader properties, AzureStagingArea staging, IExchangeLog log)
{
    /// <summary>
    /// The field naming what is renamed.
    /// </summary>
    public const string SourceField = "x-ms-rename-source";

    /// <summary>
    /// Answers a rename onto a path in an existing filesystem.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose fields name the source.</param>
    /// <param name="target">The destination: the request's filesystem and path.</param>
    /// <param name="caller">Who renames, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels reading the file and writing the index.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch));
        }

        var sourceField = AzureRequestFields.Value(request.Fields, SourceField)!;
        var source = AzureBlobTarget.Parse(sourceField.Split('?')[0]);
        if (!source.NamesABlob)
        {
            return reply.Error(AzureRefusal.Of(AzureError.InvalidSourceUri));
        }

        var sourceMapping = store.MapBucketRequestPathForCaller(source.Path, caller);
        var destination = store.MapBucketRequestPathForCaller(target.Path, caller);
        if (RefusalBeforeTheMove(source, sourceMapping, target, destination) is { } refusal)
        {
            return reply.Error(AzureRefusal.Of(refusal));
        }

        var sourceIndexKey = AzureIndexKey.OfRaw(store, source.RawContainer, source.RawBlob);
        var kept = await properties.ReadAsync(sourceMapping, sourceIndexKey, cancellationToken);
        var moved = IsExclusive(request.Fields) ? store.RenameEntryWithoutReplacing(sourceMapping, destination) : store.RenameEntry(sourceMapping, destination);
        if (MoveRefusal(moved) is { } moveRefusal)
        {
            return reply.Error(AzureRefusal.Of(moveRefusal));
        }

        await staging.DiscardInTurnAsync(AzurePathUpdate.KeyOf(source), cancellationToken);
        await staging.DiscardInTurnAsync(AzurePathUpdate.KeyOf(target), cancellationToken);

        return kept is null
            ? Created(reply, store.GetEntryStatus(destination)!.LastModifiedUtc, eTag: null)
            : await FileCreatedAsync(reply, target, destination, kept, cancellationToken);
    }

    // The source must hold something and a directory may not move inside itself; the destination
    // must be a name the store can hold.
    private AzureError? RefusalBeforeTheMove(AzureBlobTarget source, ContentPathMapping sourceMapping, AzureBlobTarget target, ContentPathMapping destination)
    {
        if (!sourceMapping.IsMapped || store.GetEntryKind(sourceMapping) == ContentEntryKind.None)
        {
            return AzureError.SourcePathNotFound;
        }

        if (!destination.IsMapped)
        {
            return AzureError.InvalidResourceName;
        }

        var sourceKey = AzurePathUpdate.KeyOf(source).TrimEnd('/');

        return AzurePathUpdate.KeyOf(target).StartsWith(sourceKey + "/", StringComparison.Ordinal) ? AzureError.InvalidRenameSourcePath : null;
    }

    private static AzureError? MoveRefusal(ContentChangeResult moved) => moved switch
    {
        ContentChangeResult.Done => null,
        ContentChangeResult.Exists => AzureError.PathAlreadyExists,
        ContentChangeResult.NoSuchDirectory => AzureError.RenameDestinationParentPathNotFound,
        _ => AzureError.AuthorizationPermissionMismatch,
    };

    private static bool IsExclusive(IReadOnlyList<KeyValuePair<string, string>> fields) =>
        AzureRequestFields.Value(fields, "If-None-Match") == "*";

    // The renamed file's header fields and properties, stored again under its new name.
    private async Task<HttpAnswer> FileCreatedAsync(AzureReply reply, AzureBlobTarget target, ContentPathMapping destination, ObjectProperties kept, CancellationToken cancellationToken)
    {
        if (AzureIndexKey.OfRaw(store, target.RawContainer, target.RawBlob) is { } indexKey)
        {
            var stored = await index.StoreAsync(
                indexKey,
                new ObjectMetadata
                {
                    ContentType = kept.ContentType,
                    ContentEncoding = kept.ContentEncoding,
                    ContentDisposition = kept.ContentDisposition,
                    CacheControl = kept.CacheControl,
                    ContentLanguage = kept.ContentLanguage,
                    ContentMd5 = kept.ContentMd5,
                    UserMetadata = kept.UserMetadata.ToDictionary(StringComparer.Ordinal),
                },
                cancellationToken);
            if (stored.StorageFailure is { } failure)
            {
                log.Note($"The object metadata index could not be written: {failure}");
            }
        }

        return Created(reply, store.GetFileStatus(destination)!.LastModifiedUtc, kept.ETag);
    }

    // 201 with the tag - a directory's from its last write time - and the date, and no body.
    private static HttpAnswer Created(AzureReply reply, DateTimeOffset lastModified, string? eTag) =>
        reply.Answer(
            AzureBlobRequestAnswerer.Created,
            [
                new("ETag", eTag ?? AzurePathCreate.DirectoryETag(lastModified)),
                new("Last-Modified", lastModified.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)),
                new("Content-Length", "0"),
            ],
            HttpAnswerBody.None);
}
