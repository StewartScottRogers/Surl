using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Answers <c>PUT /v2/&lt;name&gt;/manifests/&lt;reference&gt;</c> (end-7; Dist "Pushing
/// Manifests", "Pushing Manifests with Subject"; ADR-0115, decisions 3 and 3.1): the body is
/// checked, stored byte for byte as the blob file of its digest, and listed in <c>index.json</c>,
/// tagged when pushed by tag.
/// </summary>
/// <remarks>
/// The order: the reference (<c>DIGEST_INVALID</c>, or <c>MANIFEST_INVALID</c> for a tag off the
/// grammar), the <c>Content-Type</c> (<c>MANIFEST_INVALID</c>), the size (<c>413
/// MANIFEST_INVALID</c> past 4 MiB), the repository, the body (<c>MANIFEST_INVALID</c>), the digest
/// of a push by digest (<c>DIGEST_INVALID</c>), then the blobs and manifests it names
/// (<c>MANIFEST_BLOB_UNKNOWN</c>, once per missing digest). A manifest with a <c>subject</c> is
/// answered with <c>OCI-Subject</c>; the subject need not exist.
/// </remarks>
/// <param name="store">The shared content store.</param>
/// <param name="state">The server's index lock.</param>
/// <param name="caller">Who pushes.</param>
/// <param name="context">The exchange: its log.</param>
internal sealed class OciManifestPush(ContentStore store, OciServerState state, ContentCaller caller, ExchangeContext context)
{
    private static readonly HttpStatus Created = new(201, "Created");

    /// <summary>
    /// Answers the push.
    /// </summary>
    /// <param name="reply">The reply to the request.</param>
    /// <param name="request">The request.</param>
    /// <param name="target">The request's target.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(OciReply reply, HttpRequest request, OciRequestTarget target, CancellationToken cancellationToken)
    {
        var byDigest = OciDigest.Parse(target.Reference);
        if (HeadRefusal(request, target.Reference, byDigest) is { } headRefusal)
        {
            return reply.Error(headRefusal);
        }

        var (repository, refusal) = await OciRepositoryCreation.OpenOrCreateAsync(store, caller, target.Name, context.Log, state.MetadataIndex, cancellationToken);
        if (repository is null)
        {
            return reply.Error(refusal!);
        }

        using var body = new MemoryStream();
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(body, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        if (outcome != HttpRequestBodyDiscardOutcome.Discarded)
        {
            return reply.Error(outcome == HttpRequestBodyDiscardOutcome.TooLarge ? OciError.ManifestTooLarge : OciError.ManifestInvalid);
        }

        var push = new Push(repository, target.Reference, byDigest, MediaTypeOf(request)!, body.ToArray());

        return await StoreAsync(reply.AfterBodyRead(), push, cancellationToken);
    }

    // What the request line and head alone refuse, before the body is read.
    private static OciError? HeadRefusal(HttpRequest request, string reference, OciDigest? byDigest)
    {
        if (reference.Contains(':'))
        {
            if (byDigest is null)
            {
                return OciError.DigestInvalid;
            }
        }
        else if (!OciNames.IsTag(reference))
        {
            return OciError.ManifestInvalid;
        }

        if (MediaTypeOf(request) is not { } mediaType || !OciPushedManifest.PushableMediaTypes.Contains(mediaType))
        {
            return OciError.ManifestInvalid;
        }

        return request.Body.AnnouncedLength > OciRepository.MaxManifestBytes ? OciError.ManifestTooLarge : null;
    }

    // The Content-Type without its parameters (Dist: SHOULD ignore them).
    private static string? MediaTypeOf(HttpRequest request) =>
        OciRequestFields.Value(request, "Content-Type")?.Split(';')[0].Trim();

    private static async Task<int> CountMissingAsync(OciRepository repository, OciPushedManifest manifest, CancellationToken cancellationToken)
    {
        var missing = 0;
        foreach (var blob in manifest.RequiredBlobs)
        {
            missing += await repository.FindBlobAsync(blob, cancellationToken) is null ? 1 : 0;
        }

        foreach (var child in manifest.RequiredManifests)
        {
            missing += await repository.FindManifestAsync(child.ToString(), cancellationToken) is null ? 1 : 0;
        }

        return missing;
    }

    private static OciDigest Sha256Of(byte[] bytes) =>
        OciDigest.Parse("sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)))!;

    private static bool IsHashedTo(byte[] bytes, OciDigest digest)
    {
        using var hash = digest.CreateHash();
        hash.AppendData(bytes);

        return digest.IsHashOf(hash.GetHashAndReset());
    }

    private async Task<HttpAnswer> StoreAsync(OciReply reply, Push push, CancellationToken cancellationToken)
    {
        if (push.Bytes.Length > OciRepository.MaxManifestBytes)
        {
            return reply.Error(OciError.ManifestTooLarge);
        }

        if (OciPushedManifest.Read(push.Bytes, push.MediaType) is not { } manifest)
        {
            return reply.Error(OciError.ManifestInvalid);
        }

        if (push.ByDigest is { } named && !IsHashedTo(push.Bytes, named))
        {
            return reply.Error(OciError.DigestInvalid);
        }

        var missing = await CountMissingAsync(push.Repository, manifest, cancellationToken);
        if (missing > 0)
        {
            return reply.Errors(OciError.ManifestBlobUnknown, missing);
        }

        var digest = push.ByDigest ?? Sha256Of(push.Bytes);
        var descriptor = new OciDescriptor(push.MediaType, digest.ToString(), push.Bytes.Length, manifest.ArtifactType, []);
        if (await ListAsync(push, digest, descriptor, cancellationToken) is { } refusal)
        {
            return reply.Error(refusal);
        }

        KeyValuePair<string, string>[] fields = [new("Location", $"/v2/{push.Repository.Name}/manifests/{digest}"), new("Docker-Content-Digest", digest.ToString())];

        return reply.Empty(Created, manifest.SubjectDigest is { } subject ? [.. fields, new("OCI-Subject", subject)] : fields);
    }

    // The manifest's file written, then its descriptor listed in index.json under the index lock.
    private async Task<OciError?> ListAsync(Push push, OciDigest digest, OciDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (!await push.Repository.WriteManifestFileAsync(digest, push.Bytes, cancellationToken))
        {
            return OciError.Denied;
        }

        await state.IndexLock.WaitAsync(cancellationToken);
        try
        {
            var index = await push.Repository.RereadIndexAsync(cancellationToken);
            var listed = push.ByDigest is null ? OciIndexFile.WithTag(index, descriptor, push.Reference) : OciIndexFile.WithManifest(index, descriptor);
            if (listed.Count > OciIndexFile.MaxDescriptors)
            {
                return OciError.RepositoryFull;
            }

            return await push.Repository.WriteIndexAsync(listed, cancellationToken) ? null : OciError.Denied;
        }
        finally
        {
            state.IndexLock.Release();
        }
    }

    private sealed record Push(OciRepository Repository, string Reference, OciDigest? ByDigest, string MediaType, byte[] Bytes);
}
