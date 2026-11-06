using System.Globalization;
using Surl.HttpMessage;

namespace Surl.WebSocket;

/// <summary>
/// The <c>101 Switching Protocols</c> a server answers an upgrade request it accepts with, and
/// the subprotocol it chose (RFC 6455 section 4.2.2; ADR-0084 decision 2).
/// </summary>
/// <remarks>
/// The subprotocol chosen is the first of the server's own, in its preference order, that the
/// client's <c>Sec-WebSocket-Protocol</c> fields list - exactly one of the client's values, or
/// none. Subprotocol names are compared ordinally, as the tokens they are. Accepting never
/// refuses: whether an upgrade that offers none of a server's subprotocols is refused is that
/// server's own decision. No extension is ever negotiated.
/// </remarks>
public sealed class WebSocketUpgradeAcceptance
{
    private WebSocketUpgradeAcceptance(HttpResponseHead responseHead, string? chosenSubprotocol)
    {
        ResponseHead = responseHead;
        ChosenSubprotocol = chosenSubprotocol;
    }

    /// <summary>
    /// The <c>101</c>'s head: <c>Date</c>, <c>Server</c>, <c>Upgrade</c>, <c>Connection</c>,
    /// <c>Sec-WebSocket-Accept</c>, and <c>Sec-WebSocket-Protocol</c> when a subprotocol was
    /// chosen. A server may append fields of its own before writing it.
    /// </summary>
    public HttpResponseHead ResponseHead { get; }

    /// <summary>
    /// The subprotocol named in <c>Sec-WebSocket-Protocol</c>; <see langword="null"/> when none was chosen.
    /// </summary>
    public string? ChosenSubprotocol { get; }

    /// <summary>
    /// Builds the acceptance of an upgrade request that has passed
    /// <see cref="WebSocketUpgradeChecks.FindRefusal"/>.
    /// </summary>
    /// <param name="request">The upgrade request's head, which carries exactly one valid <c>Sec-WebSocket-Key</c>.</param>
    /// <param name="serverSubprotocols">The subprotocols the server speaks, most preferred first; empty for none.</param>
    /// <param name="now">The time written in the <c>Date</c> field.</param>
    /// <returns>The acceptance.</returns>
    public static WebSocketUpgradeAcceptance Accept(HttpRequestHead request, IReadOnlyList<string> serverSubprotocols, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(serverSubprotocols);

        var chosen = ChooseSubprotocol(request, serverSubprotocols);
        var key = request.GetFieldValues("Sec-WebSocket-Key")[0];
        var head = new HttpResponseHead(WebSocketHttpStatuses.SwitchingProtocols)
            .AddField("Date", FormatHttpDate(now))
            .AddField("Server", HttpResponseHead.ServerName)
            .AddField("Upgrade", "websocket")
            .AddField("Connection", "Upgrade")
            .AddField("Sec-WebSocket-Accept", WebSocketAcceptKey.Compute(key))
            .AddField("Sec-WebSocket-Protocol", chosen);

        return new WebSocketUpgradeAcceptance(head, chosen);
    }

    private static string? ChooseSubprotocol(HttpRequestHead request, IReadOnlyList<string> serverSubprotocols)
    {
        var offered = request.GetFieldValues("Sec-WebSocket-Protocol")
            .SelectMany(value => value.Split(','))
            .Select(listed => listed.Trim(' ', '\t'))
            .ToHashSet(StringComparer.Ordinal);

        return serverSubprotocols.FirstOrDefault(offered.Contains);
    }

    // IMF-fixdate (RFC 9110, section 5.6.7): "r" is exactly that format, in UTC.
    private static string FormatHttpDate(DateTimeOffset time) => time.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
}
