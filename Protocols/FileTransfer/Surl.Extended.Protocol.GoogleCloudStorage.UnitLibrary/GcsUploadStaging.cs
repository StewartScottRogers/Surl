using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// What multipart and resumable uploads share (ADR-0110, decision 9): opening an upload - its
/// checks, its folders and its staging session - appending a request body to the staged bytes, and
/// committing a written object with the metadata the opening request named.
/// </summary>
/// <param name="store">The content store objects are written to.</param>
/// <param name="putObject">PUT Object, whose checks, folders and metadata the uploads share.</param>
/// <param name="login">Finishes a <c>GOOG4-HMAC-SHA256</c> check that binds a body.</param>
/// <param name="uploads">The server's open uploads.</param>
/// <param name="isOverTls">Whether the connection is TLS, for an absolute URI's scheme.</param>
/// <param name="listenHost">The host the server listens on, for a URI when the request named none.</param>
internal sealed class GcsUploadStaging(ContentStore store, GcsPutObject putObject, GcsLoginJudge login, GcsStagedUploads uploads, bool isOverTls, string listenHost)
{
    private const int CopyBufferBytes = 81920;

    /// <summary>
    /// The server's open uploads.
    /// </summary>
    public GcsStagedUploads Uploads => uploads;

    /// <summary>
    /// Whether the store takes uploads (<c>--allow-uploads</c>).
    /// </summary>
    public bool AllowsUploads => store.ExposureOptions.AllowUploads;

    /// <summary>
    /// The object's absolute URI as the request reached it, with <paramref name="query"/> appended.
    /// </summary>
    /// <param name="request">The request, whose <c>Host</c> is used.</param>
    /// <param name="target">The request's path.</param>
    /// <param name="query">The query to append, with its <c>?</c>, or empty.</param>
    /// <returns>The URI.</returns>
    public string UriOf(HttpRequest request, GcsRequestTarget target, string query) =>
        $"{(isOverTls ? "https" : "http")}://{request.Authority ?? listenHost}{target.Path}{query}";

    /// <summary>
    /// Whether a length passes <c>--max-filesize</c>.
    /// </summary>
    /// <param name="length">The length, or <see langword="null"/>.</param>
    /// <returns>Whether it is past the limit.</returns>
    public bool IsPastTheUploadLimit(long? length) => putObject.IsPastTheUploadLimit(length);

    /// <summary>
    /// Opens an upload of the object: uploads on (<c>403 AccessDenied</c>), custom metadata at most
    /// 8 KiB (<c>400 MetadataTooLarge</c>), a name the store can hold and not ending in <c>/</c>
    /// (<c>400 InvalidObjectName</c>), room for one more (<c>503 SlowDown</c>), the name's folders,
    /// then the staging session.
    /// </summary>
    /// <param name="request">The opening request, whose fields become the object's metadata.</param>
    /// <param name="target">The bucket and object.</param>
    /// <param name="kind">Multipart or resumable.</param>
    /// <param name="caller">Who uploads.</param>
    /// <param name="cancellationToken">Cancels waiting for the uploads.</param>
    /// <returns>The refusal, or the open upload.</returns>
    public async Task<(GcsError? Refusal, GcsStagedUpload? Upload)> OpenAsync(HttpRequest request, GcsRequestTarget target, GcsUploadKind kind, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!AllowsUploads)
        {
            return (GcsError.AccessDenied, null);
        }

        if (GcsPutObject.HasTooMuchUserMetadata(request.Fields))
        {
            return (GcsError.MetadataTooLarge, null);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.ObjectPath, caller);
        if (!mapping.IsMapped || target.RawName.EndsWith('/'))
        {
            return (GcsError.InvalidObjectName, null);
        }

        return await uploads.RunAsync(() => OpenStagedAsync(request, target, kind, caller, mapping, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Appends a request body to the upload's staged bytes, then checks it against a signature that
    /// binds it and the digests the head sent. Refused, the staged bytes are cut back to where they
    /// were; past <c>--max-filesize</c>, the whole upload is discarded.
    /// </summary>
    /// <param name="request">The request whose body is read.</param>
    /// <param name="upload">The upload.</param>
    /// <param name="bodyCheck">The rest of a signature that binds the body, or <see langword="null"/>.</param>
    /// <param name="sent">The digests the head sent for this body, or <see langword="null"/> for none checked.</param>
    /// <param name="cancellationToken">Cancels reading the body.</param>
    /// <returns>The refusal, or <see langword="null"/> and the body's MD5.</returns>
    public async Task<(GcsError? Refusal, GcsObjectDigests? Digests)> AppendBodyAsync(HttpRequest request, GcsStagedUpload upload, IHttpRequestBodyCheck? bodyCheck, GcsSentDigests? sent, CancellationToken cancellationToken)
    {
        var session = upload.Session;
        var start = session.Length;
        using var hashes = new GcsObjectHashes();
        using var bodySha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var sink = new GcsHashingStream(hashes, session, bodySha256);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(sink, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        var refusal = GcsPutObject.BodyRefusal(outcome, sink.IsTooLarge)
            ?? await login.JudgeBodyAsync(bodyCheck, bodySha256.GetHashAndReset(), cancellationToken);
        var digests = hashes.Finish();
        refusal ??= sent is null || sent.Match(digests) ? null : GcsError.BadDigest;
        if (refusal is null)
        {
            return (null, digests);
        }

        if (sink.IsTooLarge)
        {
            await uploads.RemoveAsync(upload);
        }
        else
        {
            // Shorter than before, so never past the limit.
            _ = await session.SetLengthAsync(start);
        }

        return (refusal, null);
    }

    /// <summary>
    /// Copies <paramref name="size"/> staged bytes from <paramref name="offset"/> into <paramref name="destination"/>.
    /// </summary>
    /// <param name="staged">The staging session.</param>
    /// <param name="offset">The first byte.</param>
    /// <param name="size">How many bytes.</param>
    /// <param name="destination">Where they go.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>A task that completes when the bytes are copied.</returns>
    public static async Task CopyAsync(ContentUploadSession staged, long offset, long size, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferBytes];
        var copied = 0L;
        while (copied < size)
        {
            var read = await staged.ReadAtAsync(offset + copied, buffer.AsMemory(0, (int)Math.Min(buffer.Length, size - copied)), cancellationToken);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
        }
    }

    /// <summary>
    /// Commits <paramref name="session"/> over the object and keeps the metadata the upload was
    /// opened with; a commit the store refuses is <c>403 AccessDenied</c>, as for PUT Object.
    /// </summary>
    /// <param name="session">The session holding the object's bytes.</param>
    /// <param name="upload">The upload, whose opening fields are the metadata.</param>
    /// <param name="target">The bucket and object.</param>
    /// <param name="caller">Who uploads.</param>
    /// <param name="eTag">The ETag the object is kept with.</param>
    /// <param name="cancellationToken">Cancels the commit.</param>
    /// <returns>The refusal, or <see langword="null"/> and the object's generation.</returns>
    public async Task<(GcsError? Refusal, long Generation)> CommitAsync(ContentUploadSession session, GcsStagedUpload upload, GcsRequestTarget target, ContentCaller caller, string eTag, CancellationToken cancellationToken)
    {
        if (await session.CommitAsync(cancellationToken) != ContentUploadResult.Written)
        {
            return (GcsError.AccessDenied, 0);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.ObjectPath, caller);

        return upload.Metadata is { } metadata
            ? await putObject.StoreMetadataAsync(target.RawBucket, target.RawName, mapping, metadata, eTag, uploads.TimeProvider.GetUtcNow(), cancellationToken)
            : await putObject.StoreMetadataAsync(upload.MetadataFields, target, mapping, eTag, uploads.TimeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>
    /// Opens a fresh session at the object, for a completion to write into.
    /// </summary>
    /// <param name="target">The bucket and object.</param>
    /// <param name="caller">Who uploads.</param>
    /// <param name="cancellationToken">Cancels the opening.</param>
    /// <returns>The session, or the refusal.</returns>
    public async Task<(GcsError? Refusal, ContentUploadSession? Session)> OpenObjectSessionAsync(GcsRequestTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        var mapping = store.MapBucketRequestPathForCaller(target.ObjectPath, caller);
        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false), cancellationToken);

        return opening.Session is { } session ? (null, session) : (OpeningRefusal(opening.Result), null);
    }

    // Room for one more upload, the name's folders, then the staging session.
    private async Task<(GcsError? Refusal, GcsStagedUpload? Upload)> OpenStagedAsync(HttpRequest request, GcsRequestTarget target, GcsUploadKind kind, ContentCaller caller, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        if (!uploads.HasRoom)
        {
            return (GcsError.SlowDown, null);
        }

        if (putObject.CreateFolders(target, caller) is { } folderRefusal)
        {
            return (folderRefusal, null);
        }

        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false), cancellationToken);
        if (opening.Session is not { } session)
        {
            return (OpeningRefusal(opening.Result), null);
        }

        var upload = new GcsStagedUpload(RandomNumberGenerator.GetHexString(32, lowercase: true), kind, target, request.Fields, session, uploads.TimeProvider.GetUtcNow());
        uploads.Add(upload);

        return (null, upload);
    }

    private static GcsError OpeningRefusal(ContentUploadOpeningResult result) =>
        result == ContentUploadOpeningResult.IsADirectory ? GcsError.ConflictingObjectName : GcsError.AccessDenied;
}
