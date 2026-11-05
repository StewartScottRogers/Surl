using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Answers deletes (end-9, end-10; Dist "Content Management"; ADR-0115, decision 5.4): a manifest
/// by digest loses its file and every <c>index.json</c> descriptor of it; a tag loses its
/// descriptor only, its manifest staying listed untagged when nothing else lists it; a blob loses
/// its file. Each is <c>202 Accepted</c>. Nothing checks whether a deleted blob is still referenced.
/// </summary>
/// <param name="store">The shared content store.</param>
/// <param name="state">The server's index lock.</param>
/// <param name="caller">Who deletes.</param>
/// <param name="context">The exchange: its log.</param>
internal sealed class OciDeletes(ContentStore store, OciServerState state, ContentCaller caller, ExchangeContext context)
{
    private static readonly HttpStatus Accepted = new(202, "Accepted");

    /// <summary>
    /// Answers <c>DELETE /v2/&lt;name&gt;/manifests/&lt;reference&gt;</c>.
    /// </summary>
    /// <param name="reply">The reply to the request.</param>
    /// <param name="target">The request's target.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> DeleteManifestAsync(OciReply reply, OciRequestTarget target, CancellationToken cancellationToken)
    {
        if (OciRepository.Open(store, caller, target.Name, context.Log) is not { } repository)
        {
            return reply.Error(OciError.NameUnknown);
        }

        var digest = OciDigest.Parse(target.Reference);
        if (ReferenceRefusal(target.Reference, digest) is { } refusal)
        {
            return reply.Error(refusal);
        }

        await state.IndexLock.WaitAsync(cancellationToken);
        try
        {
            var error = await UnlistAsync(repository, target.Reference, digest, cancellationToken);

            return error is null ? reply.Empty(Accepted) : reply.Error(error);
        }
        finally
        {
            state.IndexLock.Release();
        }
    }

    /// <summary>
    /// Answers <c>DELETE /v2/&lt;name&gt;/blobs/&lt;digest&gt;</c>.
    /// </summary>
    /// <param name="reply">The reply to the request.</param>
    /// <param name="target">The request's target.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> DeleteBlobAsync(OciReply reply, OciRequestTarget target, CancellationToken cancellationToken)
    {
        if (OciRepository.Open(store, caller, target.Name, context.Log) is not { } repository)
        {
            return reply.Error(OciError.NameUnknown);
        }

        if (OciDigest.Parse(target.Reference) is not { } digest || await repository.FindBlobAsync(digest, cancellationToken) is null)
        {
            return reply.Error(OciError.BlobUnknown);
        }

        return repository.DeleteBlobFile(digest) == ContentChangeResult.Done ? reply.Empty(Accepted) : reply.Error(OciError.Denied);
    }

    private static OciError? ReferenceRefusal(string reference, OciDigest? digest)
    {
        if (reference.Contains(':'))
        {
            return digest is null ? OciError.DigestInvalid : null;
        }

        return OciNames.IsTag(reference) ? null : OciError.ManifestInvalid;
    }

    private static async Task<OciError?> UnlistAsync(OciRepository repository, string reference, OciDigest? digest, CancellationToken cancellationToken)
    {
        var index = await repository.RereadIndexAsync(cancellationToken);
        var isListed = index.Any(descriptor => digest is null ? descriptor.Tag == reference : descriptor.Digest == reference);
        if (!isListed)
        {
            return OciError.ManifestUnknown;
        }

        var unlisted = digest is null ? OciIndexFile.WithoutTag(index, reference) : OciIndexFile.WithoutDigest(index, reference);
        if (!await repository.WriteIndexAsync(unlisted, cancellationToken))
        {
            return OciError.Denied;
        }

        if (digest is not null)
        {
            repository.DeleteBlobFile(digest);
        }

        return null;
    }
}
