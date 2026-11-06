using Surl.Content;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// What <see cref="OciAccessCheck"/> decided about one request.
/// </summary>
/// <param name="Token">The token the request carried, or <see langword="null"/> for an anonymous read or a refusal.</param>
/// <param name="Refusal">The answer refusing the request, or <see langword="null"/> when it may proceed.</param>
internal sealed record OciAccess(OciToken? Token, HttpAnswer? Refusal)
{
    /// <summary>
    /// The caller the content store serves the request as.
    /// </summary>
    public ContentCaller Caller => Token?.Caller ?? ContentCaller.Anonymous(OciRegistryProtocolServer.Scheme);
}
