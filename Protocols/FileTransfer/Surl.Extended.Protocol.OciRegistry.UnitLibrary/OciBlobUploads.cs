using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Answers blob uploads (Dist "Push"; ADR-0115, decisions 3 and 5.4): opening a session (end-4a),
/// a single <c>POST</c> with <c>?digest=</c> (end-4b), a cross-repository mount (end-11), and on a
/// session a chunk (<c>PATCH</c>, end-5), the closing <c>PUT</c> (end-6), its status (<c>GET</c>,
/// end-13) and its cancel (<c>DELETE</c>).
/// </summary>
/// <remarks>
/// A blob is stored only once every byte received hashes to the digest the client named; a
/// mismatch is <c>400 DIGEST_INVALID</c> and the staged bytes are discarded. A chunk that does not
/// start where the session's bytes end is <c>416 BLOB_UPLOAD_INVALID</c>, and the session stays as
/// it was. A blob past <c>--max-filesize</c> is <c>413 SIZE_INVALID</c> and ends the session.
/// </remarks>
/// <param name="store">The shared content store.</param>
/// <param name="state">The server's upload sessions and session UUID source.</param>
/// <param name="token">The token granting the upload: whom it serves, and the repositories a mount may read.</param>
/// <param name="context">The exchange: its log.</param>
internal sealed class OciBlobUploads(ContentStore store, OciServerState state, OciToken token, ExchangeContext context)
{
    private static readonly HttpStatus Created = new(201, "Created");
    private static readonly HttpStatus Accepted = new(202, "Accepted");
    private static readonly HttpStatus NoContent = new(204, "No Content");

    private readonly ContentCaller caller = token.Caller;

    /// <summary>
    /// Answers <c>POST /v2/&lt;name&gt;/blobs/uploads/</c>, creating the repository when it is none yet.
    /// </summary>
    /// <param name="reply">The reply to the request.</param>
    /// <param name="request">The request.</param>
    /// <param name="target">The request's target.</param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> OpenAsync(OciReply reply, HttpRequest request, OciRequestTarget target, CancellationToken cancellationToken)
    {
        var (repository, refusal) = await OciRepositoryCreation.OpenOrCreateAsync(store, caller, target.Name, context.Log, state.MetadataIndex, cancellationToken);
        if (repository is null)
        {
            return reply.Error(refusal!);
        }

        if (OciQueryParameters.First(target.Query, "mount") is { } mount)
        {
            return await MountAsync(reply, repository, mount, OciQueryParameters.First(target.Query, "from"), cancellationToken);
        }

        return OciQueryParameters.First(target.Query, "digest") is { } digest
            ? await SinglePostAsync(reply, request, repository, digest, cancellationToken)
            : await OpenSessionAsync(reply, repository, cancellationToken);
    }

    /// <summary>
    /// Answers a request on <c>/v2/&lt;name&gt;/blobs/uploads/&lt;reference&gt;</c>.
    /// </summary>
    /// <param name="reply">The reply to the request.</param>
    /// <param name="request">The request: <c>GET</c>, <c>PATCH</c>, <c>PUT</c> or <c>DELETE</c>.</param>
    /// <param name="target">The request's target.</param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerSessionAsync(OciReply reply, HttpRequest request, OciRequestTarget target, CancellationToken cancellationToken)
    {
        if (OciRepository.Open(store, caller, target.Name, context.Log, state.MetadataIndex) is not { } repository)
        {
            return reply.Error(OciError.NameUnknown);
        }

        if (await state.Sessions.TakeAsync(target.Reference, target.Name, token.AccountName) is not { } session)
        {
            return reply.Error(OciError.BlobUploadUnknown);
        }

        return request.Method switch
        {
            "GET" => Status(reply, session),
            "PATCH" => await PatchAsync(reply, request, session, cancellationToken),
            "PUT" => await PutAsync(reply, request, repository, session, target.Query, cancellationToken),
            _ => await CancelAsync(reply, session),
        };
    }

    // The bytes of a request body appended to a session; the error that ends it, if any.
    private static async Task<OciError?> AppendBodyAsync(HttpRequest request, OciUploadSession session, CancellationToken cancellationToken)
    {
        if (!request.Body.IsAnnounced)
        {
            return null;
        }

        var sink = new OciUploadStream(session);
        var outcome = await request.Body.CopyToAsync(sink, cancellationToken);
        if (outcome == HttpRequestBodyDiscardOutcome.TooLarge || sink.IsTooLarge)
        {
            return OciError.SizeTooLarge;
        }

        return outcome == HttpRequestBodyDiscardOutcome.Incomplete ? OciError.BlobUploadInvalid : null;
    }

    // A chunk's Content-Range against the session: it must start where the bytes held end, and
    // span exactly the body's Content-Length. No Content-Range is a streamed chunk, appended whole.
    private static OciError? ChunkRefusal(HttpRequest request, OciUploadSession session)
    {
        if (OciRequestFields.Value(request, "Content-Range") is not { } contentRange)
        {
            return null;
        }

        var dash = contentRange.IndexOf('-');
        if (dash < 0
            || !long.TryParse(contentRange.AsSpan(0, dash), NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !long.TryParse(contentRange.AsSpan(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            || end < start)
        {
            return OciError.BlobUploadInvalid;
        }

        if (start != session.Length)
        {
            return OciError.BlobUploadOutOfOrder;
        }

        return request.Body.AnnouncedLength == end - start + 1 ? null : OciError.SizeInvalid;
    }

    private HttpAnswer Status(OciReply reply, OciUploadSession session)
    {
        state.Sessions.Return(session);

        return reply.Empty(NoContent, new("Location", session.Location), new("Range", session.Range));
    }

    private async Task<HttpAnswer> CancelAsync(OciReply reply, OciUploadSession session)
    {
        await session.DisposeAsync();

        return reply.Empty(NoContent);
    }

    private async Task<HttpAnswer> PatchAsync(OciReply reply, HttpRequest request, OciUploadSession session, CancellationToken cancellationToken)
    {
        if (ChunkRefusal(request, session) is { } chunkRefusal)
        {
            state.Sessions.Return(session);

            return reply.Error(chunkRefusal);
        }

        if (await AppendBodyAsync(request, session, cancellationToken) is { } bodyRefusal)
        {
            await session.DisposeAsync();

            return reply.Error(bodyRefusal);
        }

        state.Sessions.Return(session);

        return reply.AfterBodyRead().Empty(Accepted, new("Location", session.Location), new("Range", session.Range));
    }

    // The closing PUT ends the session whatever its answer.
    private async Task<HttpAnswer> PutAsync(OciReply reply, HttpRequest request, OciRepository repository, OciUploadSession session, string query, CancellationToken cancellationToken)
    {
        await using (session)
        {
            if (OciDigest.Parse(OciQueryParameters.First(query, "digest") ?? string.Empty) is not { } digest)
            {
                return reply.Error(OciError.DigestInvalid);
            }

            if (ChunkRefusal(request, session) is { } chunkRefusal)
            {
                return reply.Error(chunkRefusal);
            }

            return await AppendBodyAsync(request, session, cancellationToken) is { } bodyRefusal
                ? reply.Error(bodyRefusal)
                : await FinishAsync(reply.AfterBodyRead(), repository, session, digest, cancellationToken);
        }
    }

    private async Task<HttpAnswer> SinglePostAsync(OciReply reply, HttpRequest request, OciRepository repository, string digestText, CancellationToken cancellationToken)
    {
        if (OciDigest.Parse(digestText) is not { } digest)
        {
            return reply.Error(OciError.DigestInvalid);
        }

        if (await repository.OpenUploadSessionAsync(NewSessionId(), cancellationToken) is not { } session)
        {
            return reply.Error(OciError.Denied);
        }

        await using (session)
        {
            return await AppendBodyAsync(request, session, cancellationToken) is { } bodyRefusal
                ? reply.Error(bodyRefusal)
                : await FinishAsync(reply.AfterBodyRead(), repository, session, digest, cancellationToken);
        }
    }

    // Mounted when from names a repository the token grants pull in and the caller may read, holding the blob; else a new session.
    private async Task<HttpAnswer> MountAsync(OciReply reply, OciRepository repository, string mountText, string? from, CancellationToken cancellationToken)
    {
        if (OciDigest.Parse(mountText) is not { } digest)
        {
            return reply.Error(OciError.DigestInvalid);
        }

        var source = from is not null && token.Allows(from, OciActions.Pull) ? OciRepository.Open(store, caller, from, context.Log, state.MetadataIndex) : null;
        var blob = source is null ? null : await source.FindBlobAsync(digest, cancellationToken);
        if (blob is null)
        {
            return await OpenSessionAsync(reply, repository, cancellationToken);
        }

        if (await repository.OpenUploadSessionAsync(NewSessionId(), cancellationToken) is not { } session)
        {
            return reply.Error(OciError.Denied);
        }

        await using (session)
        {
            var sink = new OciUploadStream(session);
            await source!.CopyBlobAsync(blob, sink, cancellationToken);

            return sink.IsTooLarge ? reply.Error(OciError.SizeTooLarge) : await FinishAsync(reply, repository, session, digest, cancellationToken);
        }
    }

    private async Task<HttpAnswer> OpenSessionAsync(OciReply reply, OciRepository repository, CancellationToken cancellationToken)
    {
        if (await repository.OpenUploadSessionAsync(NewSessionId(), cancellationToken) is not { } session)
        {
            return reply.Error(OciError.Denied);
        }

        session.OwnerAccountName = token.AccountName;
        if (!await state.Sessions.TryAddAsync(session))
        {
            await session.DisposeAsync();

            return reply.Error(OciError.TooManyRequests);
        }

        return reply.Empty(Accepted, new("Location", session.Location), new("Range", session.Range), new("Docker-Upload-UUID", session.Id));
    }

    private static async Task<HttpAnswer> FinishAsync(OciReply reply, OciRepository repository, OciUploadSession session, OciDigest digest, CancellationToken cancellationToken)
    {
        if (!session.IsHashedTo(digest))
        {
            return reply.Error(OciError.DigestInvalid);
        }

        if (!await repository.StoreBlobAsync(session, digest, cancellationToken))
        {
            return reply.Error(OciError.Denied);
        }

        return reply.Empty(Created, new("Location", $"/v2/{repository.Name}/blobs/{digest}"), new("Docker-Content-Digest", digest.ToString()));
    }

    private string NewSessionId() => state.NewSessionId().ToString("D", CultureInfo.InvariantCulture);
}
