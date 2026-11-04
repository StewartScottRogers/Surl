using Surl.HttpMessage;

namespace Surl.Protocol.Ws;

/// <summary>
/// The statuses only the WebSocket server answers an upgrade request with, beside
/// <see cref="HttpStatus"/>'s shared ones (ADR-0071, decisions 1 and 2).
/// </summary>
internal static class WebSocketHttpStatuses
{
    /// <summary>
    /// 101 Switching Protocols: the upgrade is accepted, and frames follow (RFC 6455 section 4.2.2).
    /// </summary>
    public static HttpStatus SwitchingProtocols { get; } = new(101, "Switching Protocols");

    /// <summary>
    /// 426 Upgrade Required: the request did not ask to upgrade to <c>websocket</c>, or named a
    /// <c>Sec-WebSocket-Version</c> other than 13 (RFC 9110 section 15.5.22; RFC 6455 section 4.4).
    /// </summary>
    public static HttpStatus UpgradeRequired { get; } = new(426, "Upgrade Required");
}
