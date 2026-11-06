using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Decides the answer to each registry request on one connection (ADR-0115, decisions 1 and 5):
/// <c>/v2/</c>, manifest and blob pulls, the tag list and the referrers API, each failure with
/// the distribution specification's JSON error body; uploads, pushes and deletes go to
/// <see cref="OciWriteAnswerer"/>.
/// </summary>
/// <remarks>
/// The order is decision 4.1's: <c>/token</c> goes to <see cref="OciTokenEndpoint"/>; then the
/// path's endpoint (<c>404 UNSUPPORTED</c>), the method (<c>405 UNSUPPORTED</c> with
/// <c>Allow</c>), the name (<c>400 NAME_INVALID</c>), a write while uploads are off (<c>403
/// DENIED</c>, since no token could grant it), the token (<see cref="OciAccessCheck"/>), then a
/// write's own checks, or a read's repository (<c>404 NAME_UNKNOWN</c>) and the endpoint's own
/// checks. The name is checked before the token so a challenge's scope always names a valid
/// repository. Each request is served as the token's account, or as the anonymous caller on
/// <c>oci</c> for an anonymous token or an anonymous read.
/// </remarks>
/// <param name="store">The shared content store whose folders are the repositories.</param>
/// <param name="state">The server's upload sessions, index lock and tokens.</param>
/// <param name="policy">Checks the token endpoint's credentials and says whether anonymous reads are allowed.</param>
/// <param name="tlsSession">The connection's TLS session, or <see langword="null"/> on plain text.</param>
/// <param name="context">The exchange: its clock and log.</param>
internal sealed class OciRequestAnswerer(ContentStore store, OciServerState state, IAuthenticationPolicy policy, TlsSession? tlsSession, ExchangeContext context) : IHttpRequestHandler
{
    private static readonly byte[] EmptyObject = "{}"u8.ToArray();

    private readonly OciAccessCheck access = new(policy, state.Tokens, tlsSession, context);

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var reply = new OciReply(request.Method == "HEAD", request.Body.IsAnnounced, context.TimeProvider.GetUtcNow());
        if (OciTokenEndpoint.IsTokenTarget(request.Target))
        {
            return await new OciTokenEndpoint(policy, state.Tokens, store.ExposureOptions.AllowUploads, tlsSession, context).AnswerAsync(reply, request, cancellationToken);
        }

        var target = OciRequestTarget.Parse(request.Target);
        if (target.Endpoint == OciEndpoint.None)
        {
            return reply.Error(OciError.UnsupportedPath);
        }

        var allowed = AllowedMethods(target.Endpoint);
        if (!allowed.Split(", ").Contains(request.Method, StringComparer.Ordinal))
        {
            return reply.Error(OciError.UnsupportedMethod, new KeyValuePair<string, string>("Allow", allowed));
        }

        return target.Endpoint == OciEndpoint.Base
            ? await AnswerBaseAsync(reply, request, cancellationToken)
            : await AnswerInRepositoryAsync(reply, request, target, cancellationToken);
    }

    private async Task<HttpAnswer> AnswerBaseAsync(OciReply reply, HttpRequest request, CancellationToken cancellationToken) =>
        (await access.CheckAsync(reply, request, string.Empty, OciActions.None, cancellationToken)).Refusal
            ?? reply.Bytes(HttpStatus.Ok, "application/json", [], EmptyObject);

    // A delete needs delete; every other write, the upload session's GET and DELETE included, push.
    private static OciActions RequiredActions(OciRequestTarget target, string method, bool isWrite) => isWrite
        ? target.Endpoint is OciEndpoint.Manifest or OciEndpoint.Blob && method == "DELETE" ? OciActions.Delete : OciActions.Push
        : OciActions.Pull;

    private static string AllowedMethods(OciEndpoint endpoint) => endpoint switch
    {
        OciEndpoint.Blob => "GET, HEAD, DELETE",
        OciEndpoint.Manifest => "GET, HEAD, PUT, DELETE",
        OciEndpoint.Uploads => "POST",
        OciEndpoint.UploadSession => "GET, PATCH, PUT, DELETE",
        _ => "GET",
    };

    private async Task<HttpAnswer> AnswerInRepositoryAsync(OciReply reply, HttpRequest request, OciRequestTarget target, CancellationToken cancellationToken)
    {
        if (!OciNames.IsRepositoryName(target.Name))
        {
            return reply.Error(OciError.NameInvalid);
        }

        var isWrite = OciWriteAnswerer.IsWrite(target.Endpoint, request.Method);
        if (isWrite && !store.ExposureOptions.AllowUploads)
        {
            return reply.Error(OciError.Denied);
        }

        var checkedAccess = await access.CheckAsync(reply, request, target.Name, RequiredActions(target, request.Method, isWrite), cancellationToken);
        if (checkedAccess.Refusal is { } refusal)
        {
            return refusal;
        }

        if (isWrite)
        {
            return await new OciWriteAnswerer(store, state, context).AnswerAsync(reply, request, target, checkedAccess.Token!, cancellationToken);
        }

        if (OciRepository.Open(store, checkedAccess.Caller, target.Name, context.Log) is not { } repository)
        {
            return reply.Error(OciError.NameUnknown);
        }

        return await AnswerEndpointAsync(reply, request, repository, target, cancellationToken);
    }

    private Task<HttpAnswer> AnswerEndpointAsync(OciReply reply, HttpRequest request, OciRepository repository, OciRequestTarget target, CancellationToken cancellationToken)
    {
        if (target.Endpoint == OciEndpoint.Manifest)
        {
            return ManifestAsync(reply, repository, target.Reference, cancellationToken);
        }

        if (target.Endpoint == OciEndpoint.Blob)
        {
            return BlobAsync(reply, repository, target.Reference, OciRequestFields.Value(request, "Range"), cancellationToken);
        }

        return target.Endpoint == OciEndpoint.TagsList
            ? OciTagsList.AnswerAsync(reply, repository, target.Query, cancellationToken)
            : ReferrersAsync(reply, repository, target, cancellationToken);
    }

    private static async Task<HttpAnswer> ManifestAsync(OciReply reply, OciRepository repository, string reference, CancellationToken cancellationToken) =>
        await repository.FindManifestAsync(reference, cancellationToken) is { } manifest
            ? OciPullAnswers.Manifest(reply, manifest)
            : reply.Error(OciError.ManifestUnknown);

    private async Task<HttpAnswer> BlobAsync(OciReply reply, OciRepository repository, string reference, string? rangeField, CancellationToken cancellationToken)
    {
        var digest = OciDigest.Parse(reference);
        var blob = digest is null ? null : await repository.FindBlobAsync(digest, cancellationToken);

        return blob is null ? reply.Error(OciError.BlobUnknown) : OciPullAnswers.Blob(reply, store, digest!, blob, rangeField);
    }

    private static async Task<HttpAnswer> ReferrersAsync(OciReply reply, OciRepository repository, OciRequestTarget target, CancellationToken cancellationToken) =>
        OciDigest.Parse(target.Reference) is { } subject
            ? await OciReferrers.AnswerAsync(reply, repository, subject, target.Query, cancellationToken)
            : reply.Error(OciError.DigestInvalid);
}
