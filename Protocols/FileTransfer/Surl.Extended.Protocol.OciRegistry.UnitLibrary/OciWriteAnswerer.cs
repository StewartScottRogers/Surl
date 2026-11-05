using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Decides the answer to each write (ADR-0115, decisions 4.1 and 5.4): every upload request,
/// manifest pushes and deletes. All of them need <c>--allow-uploads</c>; without it each is
/// <c>403 DENIED</c> before the repository is looked at.
/// </summary>
/// <remarks>
/// Every write is the anonymous caller on <c>oci</c> until BL-438 adds tokens, so the share
/// access of the anonymous caller decides what it may write.
/// </remarks>
/// <param name="store">The shared content store.</param>
/// <param name="state">The server's upload sessions and index lock.</param>
/// <param name="context">The exchange: its log.</param>
internal sealed class OciWriteAnswerer(ContentStore store, OciServerState state, ExchangeContext context)
{
    /// <summary>
    /// Whether a request is a write this class answers.
    /// </summary>
    /// <param name="endpoint">The endpoint the request names.</param>
    /// <param name="method">The request's method, one the endpoint allows.</param>
    /// <returns><see langword="true"/> for an upload request, a <c>PUT</c> or a <c>DELETE</c>.</returns>
    public static bool IsWrite(OciEndpoint endpoint, string method) =>
        endpoint is OciEndpoint.Uploads or OciEndpoint.UploadSession || method is "PUT" or "DELETE";

    /// <summary>
    /// Answers a write.
    /// </summary>
    /// <param name="reply">The reply to the request.</param>
    /// <param name="request">The request.</param>
    /// <param name="target">The request's target, its name already checked against the grammar.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The answer.</returns>
    public Task<HttpAnswer> AnswerAsync(OciReply reply, HttpRequest request, OciRequestTarget target, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return Task.FromResult(reply.Error(OciError.Denied));
        }

        var caller = ContentCaller.Anonymous(OciRegistryProtocolServer.Scheme);

        return (target.Endpoint, request.Method) switch
        {
            (OciEndpoint.Uploads, _) => new OciBlobUploads(store, state, caller, context).OpenAsync(reply, request, target, cancellationToken),
            (OciEndpoint.UploadSession, _) => new OciBlobUploads(store, state, caller, context).AnswerSessionAsync(reply, request, target, cancellationToken),
            (OciEndpoint.Manifest, "PUT") => new OciManifestPush(store, state, caller, context).AnswerAsync(reply, request, target, cancellationToken),
            (OciEndpoint.Manifest, _) => new OciDeletes(store, state, caller, context).DeleteManifestAsync(reply, target, cancellationToken),
            _ => new OciDeletes(store, state, caller, context).DeleteBlobAsync(reply, target, cancellationToken),
        };
    }
}
