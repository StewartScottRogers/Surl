using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers Put Block ("Put Block"; ADR-0109, decisions 1, 4 and 7): stages the body as an
/// uncommitted block of the blob in its <see cref="AzureStagedBlocks"/>, invisible until Put Block
/// List commits it, and answers <c>201</c> with the body's <c>Content-MD5</c>.
/// </summary>
/// <remarks>
/// Uploads need <c>--allow-uploads</c> (off: <c>403 AuthorizationPermissionMismatch</c>). A
/// missing <c>blockid</c> is <c>400 MissingRequiredQueryParameter</c>; one that is not base64 of 1
/// to 64 bytes, or not as long as the blob's other block IDs, <c>400 InvalidBlockId</c>; a block
/// past 100000 uncommitted ones <c>409 BlockCountExceedsLimit</c>. The blocks' bytes together are
/// held to <c>--max-filesize</c>: past it the blob's uncommitted blocks are discarded and the
/// answer is <c>413 RequestBodyTooLarge</c>. The blob's folders are created when its first block
/// is staged, because the staging file sits beside the blob.
/// </remarks>
/// <param name="store">The content store the blob is written to.</param>
/// <param name="putBlob">Put Blob, whose body checks and folder creation Put Block shares.</param>
/// <param name="staging">The server's staged uploads.</param>
internal sealed class AzurePutBlock(ContentStore store, AzurePutBlob putBlob, AzureStagingArea staging)
{
    private static readonly KeyValuePair<string, string>[] NoBody = [new("Content-Length", "0")];

    /// <summary>
    /// Answers a Put Block whose container exists and whose name is not too long.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">The request's container, blob name and <c>blockid</c>.</param>
    /// <param name="caller">Who writes, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels reading the body.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (RefusalBeforeTheBody(request, target, out var blockId) is { } refusal)
        {
            return reply.Error(refusal);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        if (!mapping.IsMapped)
        {
            return reply.Error(AzureRefusal.Of(AzureError.InvalidResourceName));
        }

        await staging.EnterAsync(cancellationToken);
        try
        {
            return await StageAsync(reply, request, target, mapping, caller, blockId, cancellationToken);
        }
        finally
        {
            staging.Exit();
        }
    }

    // Uploads, the block ID, then the body's head checks and its announced length.
    private AzureRefusal? RefusalBeforeTheBody(HttpRequest request, AzureBlobTarget target, out string blockId)
    {
        var sent = AzureDataLakeOperationRecognition.Parameter(target, "blockid");
        blockId = sent ?? string.Empty;
        if (!store.ExposureOptions.AllowUploads)
        {
            return AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch);
        }

        if (sent is null)
        {
            return AzureRefusal.MissingRequiredQueryParameter("blockid");
        }

        if (!AzureBlockId.IsWellFormed(blockId))
        {
            return AzureRefusal.Of(AzureError.InvalidBlockId);
        }

        return BodyRefusal(request) is { } bodyRefusal ? AzureRefusal.Of(bodyRefusal) : null;
    }

    private AzureError? BodyRefusal(HttpRequest request)
    {
        if (AzureRequestFields.Value(request.Fields, "Content-MD5") is { } contentMd5 && AzurePutBlob.ExpectedMd5(contentMd5) is null)
        {
            return AzureError.InvalidMd5;
        }

        return AzurePutBlob.BodyFramingRefusal(request) ?? (putBlob.IsAnnouncedPastTheLimit(request.Body) ? AzureError.RequestBodyTooLarge : null);
    }

    // The blob's blocks, found or begun, take the block when its ID fits and there is room.
    private async Task<HttpAnswer> StageAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentPathMapping mapping, ContentCaller caller, string blockId, CancellationToken cancellationToken)
    {
        var key = AzureStagedBlocks.KeyOf(target);
        var blocks = staging.Find<AzureStagedBlocks>(key);
        if (blocks is not null && BlockRefusal(blocks, mapping, blockId) is { } blockRefusal)
        {
            return reply.Error(AzureRefusal.Of(blockRefusal));
        }

        blocks ??= await AzureStagedBlocks.BeginAsync(staging, key);
        if (blocks is null)
        {
            return reply.Error(AzureRefusal.Of(AzureError.ServerBusy));
        }

        return await SessionRefusalAsync(blocks, target, mapping, caller, cancellationToken) is { } sessionRefusal
            ? reply.Error(AzureRefusal.Of(sessionRefusal))
            : await WriteAsync(reply, request, blocks, blockId, cancellationToken);
    }

    // An ID of another length than the blob's other blocks, or one block too many.
    private AzureError? BlockRefusal(AzureStagedBlocks blocks, ContentPathMapping mapping, string blockId)
    {
        if (!AzureBlockId.MatchesLengthOf(blockId, [.. blocks.Uncommitted, .. blocks.CommittedFor(store.GetFileStatus(mapping))]))
        {
            return AzureError.InvalidBlockId;
        }

        return blocks.Uncommitted.Count >= AzureStagedBlocks.MaxUncommittedBlocks && !blocks.HoldsUncommitted(blockId)
            ? AzureError.BlockCountExceedsLimit
            : null;
    }

    // The session the blocks' bytes go into, opened beside the blob with its folders made.
    private async Task<AzureError?> SessionRefusalAsync(AzureStagedBlocks blocks, AzureBlobTarget target, ContentPathMapping mapping, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (blocks.Session is not null)
        {
            return null;
        }

        var (refusal, session) = await putBlob.OpenBlobUploadAsync(target, mapping, caller, cancellationToken);
        if (session is not null)
        {
            blocks.BeginSession(session);
        }

        return refusal;
    }

    // The body goes after the bytes already staged; past the upload limit every block is lost.
    private async Task<HttpAnswer> WriteAsync(AzureReply reply, HttpRequest request, AzureStagedBlocks blocks, string blockId, CancellationToken cancellationToken)
    {
        var session = blocks.Session!;
        var offset = session.Length;
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var sink = new AzureUploadStream(session, md5, offset);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(sink, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        if (AzurePutBlob.BodyRefusal(outcome, sink.IsTooLarge) is { } bodyRefusal)
        {
            if (sink.IsTooLarge)
            {
                await blocks.DiscardUncommittedAsync();
            }

            return reply.Error(AzureRefusal.Of(bodyRefusal));
        }

        var digest = md5.GetHashAndReset();
        if (AzureRequestFields.Value(request.Fields, "Content-MD5") is { } contentMd5 && !digest.AsSpan().SequenceEqual(AzurePutBlob.ExpectedMd5(contentMd5)))
        {
            return reply.Error(AzureRefusal.Of(AzureError.Md5Mismatch));
        }

        blocks.Stage(new AzureBlock(blockId, offset, sink.BytesWritten), staging.TimeProvider.GetUtcNow());

        return reply.Answer(
            AzureBlobRequestAnswerer.Created,
            [new("Content-MD5", Convert.ToBase64String(digest)), new("x-ms-request-server-encrypted", "false"), .. NoBody],
            HttpAnswerBody.None);
    }
}
