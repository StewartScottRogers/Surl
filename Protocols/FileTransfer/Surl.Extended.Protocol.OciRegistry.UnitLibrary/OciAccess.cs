using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// What <see cref="OciAccessCheck"/> decided about one request.
/// </summary>
/// <param name="Token">The token the request carried, or <see langword="null"/> for an anonymous read or a refusal.</param>
/// <param name="Refusal">The answer refusing the request, or <see langword="null"/> when it may proceed.</param>
internal sealed record OciAccess(OciToken? Token, HttpAnswer? Refusal)
{
    /// <summary>
    /// The caller the content store serves the request as, on <paramref name="exchange"/>.
    /// </summary>
    /// <param name="exchange">The exchange that carries the request.</param>
    /// <returns>The caller.</returns>
    public ContentCaller CallerOn(ExchangeContext exchange) => ContentCaller.ForExchange(exchange, Token?.AccountName);
}
