using Surl.HttpMessage;

namespace Surl.Protocol.Ws;

/// <summary>
/// ADR-0071 decision 1's checks 4 to 9 of an upgrade request, the ones that look only at its
/// request line and fields (RFC 6455 section 4.2.1), run in the ADR's order.
/// </summary>
internal static class WebSocketUpgradeChecks
{
    /// <summary>
    /// The only <c>Sec-WebSocket-Version</c> surl speaks (RFC 6455 section 4.1).
    /// </summary>
    public const string SupportedVersion = "13";

    private const int KeyBytes = 16;

    private static readonly KeyValuePair<string, string> SupportedVersionField = new("Sec-WebSocket-Version", SupportedVersion);

    private static readonly WebSocketUpgradeRefusal NotGet = new(
        HttpStatus.MethodNotAllowed, "the method is not GET", [new("Allow", "GET")]);

    private static readonly WebSocketUpgradeRefusal NotHttp11 = WebSocketUpgradeRefusal.WithoutFields(
        HttpStatus.BadRequest, "the version is not HTTP/1.1");

    private static readonly WebSocketUpgradeRefusal NoWebSocketUpgrade = new(
        WebSocketHttpStatuses.UpgradeRequired, "Upgrade does not list websocket", [new("Upgrade", "websocket"), SupportedVersionField]);

    private static readonly WebSocketUpgradeRefusal NoConnectionUpgrade = WebSocketUpgradeRefusal.WithoutFields(
        HttpStatus.BadRequest, "Connection does not list Upgrade");

    private static readonly WebSocketUpgradeRefusal AnnouncesBody = WebSocketUpgradeRefusal.WithoutFields(
        HttpStatus.BadRequest, "the request announces a body");

    private static readonly WebSocketUpgradeRefusal NoValidKey = WebSocketUpgradeRefusal.WithoutFields(
        HttpStatus.BadRequest, "Sec-WebSocket-Key is not exactly one base64 value of 16 bytes");

    private static readonly WebSocketUpgradeRefusal NotVersion13 = new(
        WebSocketHttpStatuses.UpgradeRequired, "Sec-WebSocket-Version is not 13", [SupportedVersionField]);

    /// <summary>
    /// Runs checks 4 to 9 in order, a body announced by <c>Content-Length</c> or
    /// <c>Transfer-Encoding</c> refused at check 7's position.
    /// </summary>
    /// <param name="head">The upgrade request's head, which has passed checks 1 to 3.</param>
    /// <returns>The first check's refusal; <see langword="null"/> when every check passes.</returns>
    public static WebSocketUpgradeRefusal? FindRefusal(HttpRequestHead head)
    {
        if (head.Method != "GET")
        {
            return NotGet;
        }

        if (head.Version.Minor != 1)
        {
            return NotHttp11;
        }

        if (!ListsToken(head, "Upgrade", "websocket"))
        {
            return NoWebSocketUpgrade;
        }

        if (!ListsToken(head, "Connection", "Upgrade"))
        {
            return NoConnectionUpgrade;
        }

        if (HttpRequestBodyFraming.Of(head).Kind != HttpRequestBodyFramingKind.None)
        {
            return AnnouncesBody;
        }

        return FindKeyOrVersionRefusal(head);
    }

    /// <summary>
    /// Whether any field named <paramref name="fieldName"/> lists <paramref name="token"/>, a
    /// comma-separated list compared without regard to ASCII case (RFC 9110 section 5.6.1).
    /// </summary>
    /// <param name="head">The request head.</param>
    /// <param name="fieldName">The list field's name.</param>
    /// <param name="token">The token looked for.</param>
    /// <returns><see langword="true"/> when the token is listed.</returns>
    public static bool ListsToken(HttpRequestHead head, string fieldName, string token) =>
        head.GetFieldValues(fieldName)
            .SelectMany(value => value.Split(','))
            .Any(listed => string.Equals(listed.Trim(' ', '\t'), token, StringComparison.OrdinalIgnoreCase));

    private static WebSocketUpgradeRefusal? FindKeyOrVersionRefusal(HttpRequestHead head)
    {
        var keys = head.GetFieldValues("Sec-WebSocket-Key");
        if (keys.Count != 1 || !IsBase64Of16Bytes(keys[0]))
        {
            return NoValidKey;
        }

        var versions = head.GetFieldValues("Sec-WebSocket-Version");

        return versions.Count == 1 && versions[0] == SupportedVersion ? null : NotVersion13;
    }

    // Decoded base64 is never longer than its text, so a buffer of the text's length holds it.
    private static bool IsBase64Of16Bytes(string key)
    {
        var decoded = new byte[key.Length];

        return Convert.TryFromBase64String(key, decoded, out var written) && written == KeyBytes;
    }
}
