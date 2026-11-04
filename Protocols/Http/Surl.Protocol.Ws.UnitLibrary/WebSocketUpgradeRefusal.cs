using Surl.HttpMessage;

namespace Surl.Protocol.Ws;

/// <summary>
/// Why an upgrade request is refused, and how: the status, the check it failed in the words of
/// the refusal note (ADR-0071, decision 9), and the fields the refusal carries besides
/// <c>Date</c>, <c>Server</c>, <c>Content-Length</c> and <c>Connection</c> (decision 1).
/// </summary>
/// <param name="Status">The status the refusal answers with.</param>
/// <param name="FailedCheck">The check the request failed, as the refusal note names it.</param>
/// <param name="Fields">The refusal's own fields, in the order written.</param>
internal sealed record WebSocketUpgradeRefusal(HttpStatus Status, string FailedCheck, IReadOnlyList<KeyValuePair<string, string>> Fields)
{
    /// <summary>
    /// A refusal that carries no field of its own.
    /// </summary>
    /// <param name="status">The status the refusal answers with.</param>
    /// <param name="failedCheck">The check the request failed.</param>
    /// <returns>The refusal.</returns>
    public static WebSocketUpgradeRefusal WithoutFields(HttpStatus status, string failedCheck) => new(status, failedCheck, []);
}
