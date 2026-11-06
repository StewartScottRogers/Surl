using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Decides the answer to each write (ADR-0115, decisions 4.1 and 5.4): every upload request,
/// manifest pushes and deletes. <see cref="OciRequestAnswerer"/> has already refused each one
/// while <c>--allow-uploads</c> is off, and each one whose token does not grant it.
/// </summary>
/// <remarks>
/// Every write is the token's caller on <c>oci</c>, so that caller's share access decides what it
/// may write, and a mount reads its source only where the token grants <c>pull</c>.
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
    /// <param name="token">The token granting the write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The answer.</returns>
    public Task<HttpAnswer> AnswerAsync(OciReply reply, HttpRequest request, OciRequestTarget target, OciToken token, CancellationToken cancellationToken)
    {
        var caller = token.Caller;

        return (target.Endpoint, request.Method) switch
        {
            (OciEndpoint.Uploads, _) => new OciBlobUploads(store, state, token, context).OpenAsync(reply, request, target, cancellationToken),
            (OciEndpoint.UploadSession, _) => new OciBlobUploads(store, state, token, context).AnswerSessionAsync(reply, request, target, cancellationToken),
            (OciEndpoint.Manifest, "PUT") => new OciManifestPush(store, state, caller, context).AnswerAsync(reply, request, target, cancellationToken),
            (OciEndpoint.Manifest, _) => new OciDeletes(store, state, caller, context).DeleteManifestAsync(reply, target, cancellationToken),
            _ => new OciDeletes(store, state, caller, context).DeleteBlobAsync(reply, target, cancellationToken),
        };
    }
}
