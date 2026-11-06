using System.Globalization;
using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers Put Block List ("Put Block List"; ADR-0109, decisions 1, 4 and 7): writes the blob as
/// the blocks the body names, in its order - each <c>Uncommitted</c> from the staged blocks, each
/// <c>Committed</c> from the blob's committed block list, each <c>Latest</c> from the staged blocks
/// and else the committed list - keeps its <c>x-ms-blob-*</c> fields and <c>x-ms-meta-*</c> as Put
/// Blob does, discards the uncommitted blocks, and answers <c>201</c>.
/// </summary>
/// <remarks>
/// Uploads need <c>--allow-uploads</c> (off: <c>403 AuthorizationPermissionMismatch</c>). Put Blob's
/// head checks apply. A body past 4 MiB, or not a <c>BlockList</c> document, is <c>400
/// InvalidXmlDocument</c>; one past 50000 entries <c>400 BlockListTooLong</c>; an entry naming a
/// block not in its list <c>400 InvalidBlockList</c>, and nothing changes. The blob is written
/// through an upload session, so it appears whole or not at all, and is held to
/// <c>--max-filesize</c> (<c>413 RequestBodyTooLarge</c>).
/// </remarks>
/// <param name="store">The content store the blob is written to.</param>
/// <param name="putBlob">Put Blob, whose checks, folder creation and metadata Put Block List shares.</param>
/// <param name="staging">The server's staged uploads.</param>
internal sealed class AzurePutBlockList(ContentStore store, AzurePutBlob putBlob, AzureStagingArea staging)
{
    private const int CopyBufferBytes = 81920;

    /// <summary>
    /// Answers a Put Block List whose container exists and whose name is not too long.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">The request's container and blob name.</param>
    /// <param name="caller">Who writes, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels reading the body and writing the blob.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (RefusalBeforeTheBody(request) is { } refusal)
        {
            return reply.Error(AzureRefusal.Of(refusal));
        }

        var (bodyRefusal, entries) = await ReadEntriesAsync(request, cancellationToken);
        if (bodyRefusal is not null)
        {
            return reply.Error(AzureRefusal.Of(bodyRefusal));
        }

        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        if (!mapping.IsMapped)
        {
            return reply.Error(AzureRefusal.Of(AzureError.InvalidResourceName));
        }

        await staging.EnterAsync(cancellationToken);
        try
        {
            return await CommitAsync(reply, request, target, mapping, caller, entries, cancellationToken);
        }
        finally
        {
            staging.Exit();
        }
    }

    private AzureError? RefusalBeforeTheBody(HttpRequest request)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return AzureError.AuthorizationPermissionMismatch;
        }

        return AzurePutBlob.RefusalBeforeTheBody(request)
            ?? (request.Body.AnnouncedLength > AzureBlockListDocument.MaxBodyBytes ? AzureError.InvalidXmlDocument : null);
    }

    // The body, at most 4 MiB, matching its Content-MD5, read as a BlockList.
    private static async Task<(AzureError? Refusal, List<AzureBlockListEntry> Entries)> ReadEntriesAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var body = new AzureBoundedBodyStream(AzureBlockListDocument.MaxBodyBytes);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(body, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        if (body.IsTooLarge)
        {
            return (AzureError.InvalidXmlDocument, []);
        }

        if (AzurePutBlob.BodyRefusal(outcome, isTooLarge: false) is { } bodyRefusal)
        {
            return (bodyRefusal, []);
        }

        var bytes = body.ToArray();
        if (AzureRequestFields.Value(request.Fields, "Content-MD5") is { } contentMd5 && !MD5.HashData(bytes).AsSpan().SequenceEqual(AzurePutBlob.ExpectedMd5(contentMd5)))
        {
            return (AzureError.Md5Mismatch, []);
        }

        return (AzureBlockListDocument.Read(bytes, out var entries), entries);
    }

    // Each entry found in its list, then the blob written from them.
    private async Task<HttpAnswer> CommitAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentPathMapping mapping, ContentCaller caller, List<AzureBlockListEntry> entries, CancellationToken cancellationToken)
    {
        var key = AzureStagedBlocks.KeyOf(target);
        var blocks = staging.Find<AzureStagedBlocks>(key);
        var file = store.GetFileStatus(mapping);
        var sources = Resolve(entries, blocks, file);
        if (sources is null)
        {
            return reply.Error(AzureRefusal.Of(AzureError.InvalidBlockList));
        }

        var (openingRefusal, opened) = await putBlob.OpenBlobUploadAsync(target, mapping, caller, cancellationToken);
        if (opened is not { } session)
        {
            return reply.Error(AzureRefusal.Of(openingRefusal!));
        }

        await using (session)
        {
            var written = await WriteBlobAsync(session, sources, blocks?.Session, mapping, file, cancellationToken);
            return await StoreAsync(reply, request, target, mapping, key, blocks, session, written, cancellationToken);
        }
    }

    // The block each entry names, in order, and whether it is staged; null when one is not found.
    private static List<(AzureBlock Block, bool IsStaged)>? Resolve(List<AzureBlockListEntry> entries, AzureStagedBlocks? blocks, ContentFileStatus? file)
    {
        var (stagedById, committedById) = ListsById(blocks, file);
        var sources = new List<(AzureBlock Block, bool IsStaged)>(entries.Count);
        foreach (var entry in entries)
        {
            var staged = entry.Kind == AzureBlockListKind.Committed ? null : stagedById.GetValueOrDefault(entry.Id);
            if (FoundBlock(entry, staged, committedById) is not { } found)
            {
                return null;
            }

            sources.Add((found, staged is not null));
        }

        return sources;
    }

    // The staged block when the entry found one, else the committed block a Committed or Latest
    // entry names.
    private static AzureBlock? FoundBlock(AzureBlockListEntry entry, AzureBlock? staged, Dictionary<string, AzureBlock> committedById) =>
        staged ?? (entry.Kind == AzureBlockListKind.Uncommitted ? null : committedById.GetValueOrDefault(entry.Id));

    // The uncommitted blocks and the committed list, each by ID; none for a blob with no blocks.
    private static (Dictionary<string, AzureBlock> Staged, Dictionary<string, AzureBlock> Committed) ListsById(AzureStagedBlocks? blocks, ContentFileStatus? file) =>
        blocks is null ? ([], []) : (ById(blocks.Uncommitted), ById(blocks.CommittedFor(file)));

    // Each ID's first block: a committed list may name one block more than once.
    private static Dictionary<string, AzureBlock> ById(IReadOnlyList<AzureBlock> blocks)
    {
        var byId = new Dictionary<string, AzureBlock>(StringComparer.Ordinal);
        foreach (var block in blocks)
        {
            byId.TryAdd(block.Id, block);
        }

        return byId;
    }

    // The blocks' bytes, one after another, into the blob's upload session, hashed for its ETag.
    private async Task<WrittenBlob> WriteBlobAsync(ContentUploadSession session, List<(AzureBlock Block, bool IsStaged)> sources, ContentUploadSession? stagedBytes, ContentPathMapping mapping, ContentFileStatus? file, CancellationToken cancellationToken)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var sink = new AzureUploadStream(session, md5, 0);
        var committed = new List<AzureBlock>(sources.Count);
        var offset = 0L;
        foreach (var (block, isStaged) in sources)
        {
            if (isStaged)
            {
                await CopyStagedAsync(stagedBytes!, block, sink, cancellationToken);
            }
            else if (block.Size > 0)
            {
                await store.CopyFileBytesAsync(mapping, ContentByteRange.Select(file!.Length, block.Offset, block.Offset + block.Size - 1), sink, cancellationToken);
            }

            committed.Add(block with { Offset = offset });
            offset += block.Size;
        }

        return new WrittenBlob(md5.GetHashAndReset(), committed, sink.IsTooLarge);
    }

    private static async Task CopyStagedAsync(ContentUploadSession stagedBytes, AzureBlock block, AzureUploadStream sink, CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferBytes];
        var copied = 0L;
        int read;
        do
        {
            read = await stagedBytes.ReadAtAsync(block.Offset + copied, buffer.AsMemory(0, (int)Math.Min(buffer.Length, block.Size - copied)), cancellationToken);
            await sink.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
        }
        while (read > 0 && copied < block.Size);
    }

    // The blob committed, its metadata kept, its block list remembered and the staged blocks dropped.
    private async Task<HttpAnswer> StoreAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentPathMapping mapping, string key, AzureStagedBlocks? blocks, ContentUploadSession session, WrittenBlob written, CancellationToken cancellationToken)
    {
        var refusal = written.IsTooLarge
            ? AzureError.RequestBodyTooLarge
            : AzurePutBlob.CommitRefusal(await session.CommitAsync(cancellationToken));
        refusal ??= await putBlob.StoreMetadataAsync(request.Fields, target, mapping, written.Digest, cancellationToken);
        if (refusal is not null)
        {
            return reply.Error(AzureRefusal.Of(refusal));
        }

        var file = store.GetFileStatus(mapping)!;
        blocks ??= await AzureStagedBlocks.BeginAsync(staging, key);
        if (blocks is not null)
        {
            await blocks.CommitAsync(written.Blocks, file, staging.TimeProvider.GetUtcNow());
        }

        return reply.Answer(
            AzureBlobRequestAnswerer.Created,
            [
                new("ETag", AzurePutBlob.ETagOf(written.Digest)),
                new("Last-Modified", file.LastModifiedUtc.ToString("r", CultureInfo.InvariantCulture)),
                new("x-ms-request-server-encrypted", "false"),
                new("Content-Length", "0"),
            ],
            HttpAnswerBody.None);
    }

    private sealed record WrittenBlob(byte[] Digest, List<AzureBlock> Blocks, bool IsTooLarge);
}
