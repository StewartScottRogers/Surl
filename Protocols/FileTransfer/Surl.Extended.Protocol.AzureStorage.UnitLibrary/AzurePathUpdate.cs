using System.Globalization;
using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers Path - Update's <c>append</c> and <c>flush</c> ("Path - Update"; ADR-0109, decisions 4,
/// 5.5 and 7): an append stages its body at <c>position</c> in the file's
/// <see cref="AzureStagedPath"/>, invisible to every protocol, and answers <c>202</c>; a flush to
/// <c>position</c> commits the staged bytes up to it when they reach it without a gap or an overlap,
/// else <c>400 InvalidFlushPosition</c>, and answers <c>200</c>.
/// </summary>
/// <remarks>
/// Both need <c>--allow-uploads</c> (off: <c>403 AuthorizationPermissionMismatch</c>) and a file at
/// the path (<c>404 PathNotFound</c>; a directory <c>409 ResourceTypeMismatch</c>). An append before
/// the file's committed end is <c>400 InvalidQueryParameterValue</c>: its bytes would overwrite
/// committed ones. The conditional fields are checked against the committed file before anything
/// is staged or flushed (<c>If-None-Match: *</c> <c>409 PathAlreadyExists</c>, any other failing
/// condition <c>412 ConditionNotMet</c>). Staged bytes past a flush are dropped, unless the flush
/// sends <c>retainUncommittedData=true</c>: then they stay staged for a later flush (ADR-0109,
/// decision 5.5). <c>close</c> is accepted and changes nothing.
/// </remarks>
/// <param name="store">The content store the file is written to.</param>
/// <param name="index">The object metadata index, whose entry for the file is carried over a flush.</param>
/// <param name="properties">Reads the flushed file's properties.</param>
/// <param name="staging">The server's staged appends.</param>
/// <param name="log">Where a failure to rewrite the index file is noted.</param>
internal sealed class AzurePathUpdate(ContentStore store, ObjectMetadataIndex index, AzureBlobPropertiesReader properties, AzureStagingArea staging, IExchangeLog log)
{
    private static readonly KeyValuePair<string, string>[] NoBody = [new("Content-Length", "0")];

    /// <summary>
    /// Answers an append or a flush on a path in an existing filesystem.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body an append reads.</param>
    /// <param name="target">The request's filesystem, path and query.</param>
    /// <param name="caller">Who writes, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels reading the body and writing the file.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch));
        }

        var isAppend = AzureDataLakeOperationRecognition.Parameter(target, "action") == "append";
        if (RequestRefusal(request, target, isAppend, out var position) is { } requestRefusal)
        {
            return reply.Error(requestRefusal);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        if (PathRefusal(mapping) is { } pathRefusal)
        {
            return reply.Error(AzureRefusal.Of(pathRefusal));
        }

        if (await AzurePathCreate.ConditionRefusalAsync(store, properties, request, target, mapping, cancellationToken) is { } conditionRefusal)
        {
            return reply.Error(AzureRefusal.Of(conditionRefusal));
        }

        await staging.EnterAsync(cancellationToken);
        try
        {
            return isAppend
                ? await AppendAsync(reply, request, target, mapping, position, cancellationToken)
                : await FlushAsync(reply, target, mapping, position, cancellationToken);
        }
        finally
        {
            staging.Exit();
        }
    }

    /// <summary>
    /// The staging key of a target: its filesystem and path, percent-decoded.
    /// </summary>
    /// <param name="target">The target.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(AzureBlobTarget target) => Uri.UnescapeDataString($"{target.RawContainer}/{target.RawBlob}");

    // The query, then the body's framing.
    private AzureRefusal? RequestRefusal(HttpRequest request, AzureBlobTarget target, bool isAppend, out long position)
    {
        if (QueryRefusal(target, out position) is { } queryRefusal)
        {
            return queryRefusal;
        }

        var bodyRefusal = isAppend ? AppendBodyRefusal(request) : FlushBodyRefusal(request);

        return bodyRefusal is null ? null : AzureRefusal.Of(bodyRefusal);
    }

    // position is required and a whole number.
    private static AzureRefusal? QueryRefusal(AzureBlobTarget target, out long position)
    {
        position = 0;
        if (AzureDataLakeOperationRecognition.Parameter(target, "position") is not { } positionText)
        {
            return AzureRefusal.MissingRequiredQueryParameter("position");
        }

        return long.TryParse(positionText, NumberStyles.None, CultureInfo.InvariantCulture, out position)
            ? null
            : AzureRefusal.InvalidQueryParameterValue("position", positionText, "The position must be a whole number of bytes.");
    }

    // An append's body must be readable, announced, and within --max-filesize.
    private AzureError? AppendBodyRefusal(HttpRequest request)
    {
        if (!request.Body.IsReadable)
        {
            return AzureError.InvalidInput;
        }

        if (!request.Body.IsAnnounced && AzureRequestFields.Value(request.Fields, "Content-Length") is null)
        {
            return AzureError.MissingContentLengthHeader;
        }

        return store.ExposureOptions.MaxUploadBytes > 0 && request.Body.AnnouncedLength > store.ExposureOptions.MaxUploadBytes
            ? AzureError.RequestBodyTooLarge
            : null;
    }

    // A flush carries no body ("Path - Update": Content-Length must be 0 for flush).
    private static AzureError? FlushBodyRefusal(HttpRequest request) =>
        request.Body.IsAnnounced ? AzureError.ContentLengthMustBeZero : null;

    private AzureError? PathRefusal(ContentPathMapping mapping)
    {
        var kind = mapping.IsMapped ? store.GetEntryKind(mapping) : ContentEntryKind.None;

        return kind switch
        {
            ContentEntryKind.File => null,
            ContentEntryKind.Directory => AzureError.ResourceTypeMismatch,
            _ => AzureError.PathNotFound,
        };
    }

    // Stages the body at position, beginning the file's staging when it has none.
    private async Task<HttpAnswer> AppendAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentPathMapping mapping, long position, CancellationToken cancellationToken)
    {
        var key = KeyOf(target);
        var staged = staging.Find<AzureStagedPath>(key) ?? await BeginStagingAsync(key, mapping, cancellationToken);
        if (staged is null)
        {
            return reply.Error(AzureRefusal.Of(AzureError.ServerBusy));
        }

        if (position < staged.CommittedLength)
        {
            return reply.Error(AzureRefusal.InvalidQueryParameterValue("position", position.ToString(CultureInfo.InvariantCulture), "The position is before the end of the file's committed data."));
        }

        var (bodyRefusal, bytesWritten) = await StageBodyAsync(request, key, staged, position, cancellationToken);
        if (bodyRefusal is not null)
        {
            return reply.Error(AzureRefusal.Of(bodyRefusal));
        }

        return IsFlushRequested(target)
            ? await FlushAsync(reply, target, mapping, position + bytesWritten, cancellationToken)
            : reply.Answer(AzureBlobRequestAnswerer.Accepted, NoBody, HttpAnswerBody.None);
    }

    // The body written into the staging at position and recorded as a range; past the upload
    // limit the session has ended, so the staging is discarded.
    private async Task<(AzureError? Refusal, long BytesWritten)> StageBodyAsync(HttpRequest request, string key, AzureStagedPath staged, long position, CancellationToken cancellationToken)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var sink = new AzureUploadStream(staged.Session, md5, position);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(sink, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        if (AzurePutBlob.BodyRefusal(outcome, sink.IsTooLarge) is { } bodyRefusal)
        {
            if (sink.IsTooLarge)
            {
                await staging.DiscardAsync(key);
            }

            return (bodyRefusal, 0);
        }

        staged.Add(new AzureStagedRange(position, sink.BytesWritten), staging.TimeProvider.GetUtcNow());
        return (null, sink.BytesWritten);
    }

    private static bool IsFlushRequested(AzureBlobTarget target) =>
        string.Equals(AzureDataLakeOperationRecognition.Parameter(target, "flush"), "true", StringComparison.OrdinalIgnoreCase);

    private static bool IsRetainRequested(AzureBlobTarget target) =>
        string.Equals(AzureDataLakeOperationRecognition.Parameter(target, "retainUncommittedData"), "true", StringComparison.OrdinalIgnoreCase);

    private async Task<AzureStagedPath?> BeginStagingAsync(string key, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: true, CreatesMissingFile: false, RefusesExistingFile: false), cancellationToken);
        // The path was just found to be a file the caller may write, so the upload opens.
        var session = opening.Session!;
        var staged = new AzureStagedPath(session, session.Length, staging.TimeProvider.GetUtcNow());
        if (await staging.TryAddAsync(key, staged))
        {
            return staged;
        }

        await session.DisposeAsync();
        return null;
    }

    // Commits the staged bytes up to position, keeping the file's stored header fields.
    private async Task<HttpAnswer> FlushAsync(AzureReply reply, AzureBlobTarget target, ContentPathMapping mapping, long position, CancellationToken cancellationToken)
    {
        var key = KeyOf(target);
        var staged = staging.Find<AzureStagedPath>(key);
        if (staged is null ? position != store.GetFileStatus(mapping)!.Length : !staged.Reaches(position))
        {
            return reply.Error(AzureRefusal.Of(AzureError.InvalidFlushPosition));
        }

        var indexKey = AzureIndexKey.OfRaw(store, target.RawContainer, target.RawBlob);
        var before = await properties.ReadAsync(mapping, indexKey, cancellationToken);
        if (staged is not null && await CommitAsync(key, staged, mapping, position, IsRetainRequested(target), cancellationToken) is { } commitRefusal)
        {
            return reply.Error(AzureRefusal.Of(commitRefusal));
        }

        if (indexKey is not null)
        {
            await KeepHeaderFieldsAsync(indexKey, before!, cancellationToken);
        }

        var after = (await properties.ReadAsync(mapping, indexKey, cancellationToken))!;

        return reply.Answer(
            HttpStatus.Ok,
            [
                new("ETag", after.ETag),
                new("Last-Modified", after.LastWriteTimeUtc.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)),
                .. NoBody,
            ],
            HttpAnswerBody.None);
    }

    // The staged bytes up to position committed; with bytes staged past it and retain asked for,
    // a copy of the whole staging taken first carries the staging on, else the staging ends.
    private async Task<AzureError?> CommitAsync(string key, AzureStagedPath staged, ContentPathMapping mapping, long position, bool isRetainRequested, CancellationToken cancellationToken)
    {
        var next = isRetainRequested && staged.Session.Length > position ? await CopyAsync(staged.Session, mapping, cancellationToken) : null;
        await staged.Session.SetLengthAsync(position);
        var refusal = AzurePutBlob.CommitRefusal(await staged.Session.CommitAsync(cancellationToken));
        if (next is null || refusal is not null)
        {
            await staging.DiscardAsync(key);
            await (next?.DisposeAsync() ?? ValueTask.CompletedTask);

            return refusal;
        }

        staged.ContinueAfterFlush(next, position, staging.TimeProvider.GetUtcNow());
        return null;
    }

    // A new upload session for the file holding every byte of the staging's session.
    private async Task<ContentUploadSession> CopyAsync(ContentUploadSession staged, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: false, RefusesExistingFile: false), cancellationToken);
        // The file is there and the caller may write it: the staging was opened on it.
        var next = opening.Session!;
        var buffer = new byte[81920];
        for (long offset = 0; offset < staged.Length; offset += buffer.Length)
        {
            var read = await staged.ReadAtAsync(offset, buffer, cancellationToken);
            // The copy is no longer than the staging, which is within --max-filesize.
            _ = await next.WriteAtAsync(offset, buffer.AsMemory(0, read), cancellationToken);
        }

        return next;
    }

    // The index forgets an entry whose file changed, so the header fields are stored again and
    // the ETag computed afresh.
    private async Task KeepHeaderFieldsAsync(string indexKey, ObjectProperties before, CancellationToken cancellationToken)
    {
        var stored = await index.StoreAsync(
            indexKey,
            new ObjectMetadata
            {
                ContentType = before.ContentType,
                ContentEncoding = before.ContentEncoding,
                ContentDisposition = before.ContentDisposition,
                CacheControl = before.CacheControl,
                ContentLanguage = before.ContentLanguage,
                UserMetadata = before.UserMetadata.ToDictionary(StringComparer.Ordinal),
            },
            cancellationToken);
        if (stored.StorageFailure is { } failure)
        {
            log.Note($"The object metadata index could not be written: {failure}");
        }
    }
}
