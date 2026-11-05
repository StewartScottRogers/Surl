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
/// The order is decision 4.1's, without the token check BL-438 adds: the path's endpoint
/// (<c>404 UNSUPPORTED</c>), the method (<c>405 UNSUPPORTED</c> with <c>Allow</c>), the name
/// (<c>400 NAME_INVALID</c>), then a write's own checks, or a read's repository (<c>404
/// NAME_UNKNOWN</c>) and the endpoint's own checks. Every request is served as the anonymous
/// caller on <c>oci</c>, so a share closed to anonymous callers holds no repository.
/// </remarks>
/// <param name="store">The shared content store whose folders are the repositories.</param>
/// <param name="state">The server's upload sessions and index lock.</param>
/// <param name="context">The exchange: its clock and log.</param>
internal sealed class OciRequestAnswerer(ContentStore store, OciServerState state, ExchangeContext context) : IHttpRequestHandler
{
    private static readonly byte[] EmptyObject = "{}"u8.ToArray();

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var target = OciRequestTarget.Parse(request.Target);
        var reply = new OciReply(request.Method == "HEAD", request.Body.IsAnnounced, context.TimeProvider.GetUtcNow());
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
            ? reply.Bytes(HttpStatus.Ok, "application/json", [], EmptyObject)
            : await AnswerInRepositoryAsync(reply, request, target, cancellationToken);
    }

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

        if (OciWriteAnswerer.IsWrite(target.Endpoint, request.Method))
        {
            return await new OciWriteAnswerer(store, state, context).AnswerAsync(reply, request, target, cancellationToken);
        }

        if (OciRepository.Open(store, ContentCaller.Anonymous(OciRegistryProtocolServer.Scheme), target.Name, context.Log) is not { } repository)
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
