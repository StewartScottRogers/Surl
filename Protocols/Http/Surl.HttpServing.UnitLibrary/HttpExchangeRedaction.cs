using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.HttpServing;

/// <summary>
/// Creates the exchange-log redactor of an extended server that answers HTTP/1.1 through
/// <see cref="Http1ConnectionServer"/>, so the log shows <c>Authorization: Basic ****</c> and never
/// the credentials (ADR-0182, decision 7; BL-1465). A server implements
/// <see cref="IExchangeBytesRedactingServer"/> by returning <see cref="CreateRedactor"/>.
/// </summary>
public static class HttpExchangeRedaction
{
    /// <summary>
    /// The API-key and session-token fields besides <c>Authorization</c> and <c>Proxy-Authorization</c>
    /// whose whole value is masked.
    /// </summary>
    public static IReadOnlyList<string> ApiKeyFieldNames { get; } = Array.AsReadOnly(
        ["X-Api-Key", "Api-Key", "X-Goog-Api-Key", "X-Amz-Security-Token", "Ocp-Apim-Subscription-Key"]);

    /// <summary>
    /// Creates a redactor for one connection's HTTP/1.x request heads.
    /// </summary>
    /// <returns>A redactor used by that connection alone.</returns>
    public static IReceivedBytesRedactor CreateRedactor() => new HttpRequestHeadBytesRedactor(ApiKeyFieldNames);
}
