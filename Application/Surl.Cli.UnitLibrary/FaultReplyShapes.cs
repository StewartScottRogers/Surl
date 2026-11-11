using System.Collections.Frozen;
using System.Globalization;

namespace Surl.Cli;

/// <summary>
/// The error shape each scheme's <c>--fault error</c> <c>reply</c> must fit (ADR-0124 decision 4's
/// reply tables), so a chosen error is one the protocol allows. A code and its text are separated
/// by the first space. Of the extended schemes, <c>coap</c>, <c>mcp</c> and <c>signalr</c> and
/// their secure twins have rows (ADR-0144), as do <c>s3</c>, <c>az</c>, <c>abfs</c>, <c>abfss</c>
/// and <c>gs</c> (BL-983); the others have none yet (BL-834, BL-836).
/// </summary>
internal static class FaultReplyShapes
{
    private static readonly FrozenDictionary<string, Func<string, bool>> FitsByScheme = new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal)
    {
        ["abfs"] = IsObjectStoreError,
        ["abfss"] = IsObjectStoreError,
        ["az"] = IsObjectStoreError,
        ["coap"] = IsCoapResponseCode,
        ["coaps"] = IsCoapResponseCode,
        ["dict"] = reply => IsCodeWithText(reply, 500, 599),
        ["ftp"] = reply => IsCodeWithText(reply, 400, 599),
        ["ftps"] = reply => IsCodeWithText(reply, 400, 599),
        ["gopher"] = IsText,
        ["gs"] = IsObjectStoreError,
        ["gophers"] = IsText,
        ["http"] = reply => IsCodeWithOptionalText(reply, 400, 599),
        ["https"] = reply => IsCodeWithOptionalText(reply, 400, 599),
        ["imap"] = IsImapResponse,
        ["imaps"] = IsImapResponse,
        ["ldap"] = IsLdapResult,
        ["ldaps"] = IsLdapResult,
        ["mcp"] = IsJsonRpcError,
        ["mcps"] = IsJsonRpcError,
        ["mqtt"] = IsMqttReasonCode,
        ["mqtts"] = IsMqttReasonCode,
        ["pop3"] = IsText,
        ["pop3s"] = IsText,
        ["rtsp"] = reply => IsCodeWithOptionalText(reply, 400, 599),
        ["signalr"] = IsText,
        ["signalrs"] = IsText,
        ["s3"] = IsObjectStoreError,
        ["scp"] = IsText,
        ["sftp"] = reply => IsCodeWithText(reply, 2, 8),
        ["smb"] = IsNtStatusError,
        ["smbs"] = IsNtStatusError,
        ["smtp"] = reply => IsCodeWithText(reply, 400, 599),
        ["smtps"] = reply => IsCodeWithText(reply, 400, 599),
        ["telnet"] = IsText,
        ["tftp"] = reply => IsCodeWithText(reply, 0, 7),
        ["ws"] = IsWebSocketClose,
        ["wss"] = IsWebSocketClose,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="scheme"/> has an error shape a reply can be checked against.</summary>
    /// <param name="scheme">A lower-case scheme.</param>
    /// <returns><see langword="true"/> for an original scheme.</returns>
    public static bool HasShape(string scheme) => FitsByScheme.ContainsKey(scheme);

    /// <summary>Whether <paramref name="reply"/> fits the error shape of <paramref name="scheme"/>.</summary>
    /// <param name="scheme">A lower-case scheme for which <see cref="HasShape"/> holds.</param>
    /// <param name="reply">The reply, decoded; never empty.</param>
    /// <returns><see langword="true"/> when the protocol allows the reply as an error.</returns>
    public static bool Fits(string scheme, string reply) => FitsByScheme[scheme](reply);

    private static bool IsText(string reply) => reply.Length > 0;

    /// <summary>A status 400 to 599 and an error code word, as the object-store services write them (<c>503 SlowDown</c>).</summary>
    private static bool IsObjectStoreError(string reply) => IsCodeWithText(reply, 400, 599);

    private static bool IsCodeWithText(string reply, int lowest, int highest) =>
        TrySplitCode(reply, out var code, out var text) && code >= lowest && code <= highest && !string.IsNullOrEmpty(text);

    private static bool IsCodeWithOptionalText(string reply, int lowest, int highest) =>
        TrySplitCode(reply, out var code, out var text) && code >= lowest && code <= highest && text != string.Empty;

    /// <summary>A close code 1000 to 4999 other than the three never sent (RFC 6455 section 7.4.1), and an optional reason.</summary>
    private static bool IsWebSocketClose(string reply) =>
        IsCodeWithOptionalText(reply, 1000, 4999) && TrySplitCode(reply, out var code, out _) && code is not (1005 or 1006 or 1015);

    /// <summary><c>NO</c> or <c>BAD</c>, in any case, and text.</summary>
    private static bool IsImapResponse(string reply)
    {
        var space = reply.IndexOf(' ', StringComparison.Ordinal);
        var word = space < 0 ? reply : reply[..space];
        return space > 0
            && space < reply.Length - 1
            && (word.Equals("NO", StringComparison.OrdinalIgnoreCase) || word.Equals("BAD", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A resultCode 1 to 80 other than referral (10) and saslBindInProgress (14), and diagnostic text.</summary>
    private static bool IsLdapResult(string reply) =>
        IsCodeWithText(reply, 1, 80) && TrySplitCode(reply, out var code, out _) && code is not (10 or 14);

    /// <summary>An MQTT 5 reason code 128 to 255, alone.</summary>
    private static bool IsMqttReasonCode(string reply) =>
        TrySplitCode(reply, out var code, out var text) && text is null && code is >= 128 and <= 255;

    /// <summary>
    /// A CoAP response code 4.00 to 5.05 written <c>c.dd</c> (RFC 7252 section 3; 4.00 to 4.31 and
    /// 5.00 to 5.05), and an optional diagnostic text.
    /// </summary>
    private static bool IsCoapResponseCode(string reply)
    {
        var space = reply.IndexOf(' ', StringComparison.Ordinal);
        var code = space < 0 ? reply : reply[..space];
        return code.Length == 4
            && code[1] == '.'
            && space != reply.Length - 1
            && int.TryParse(code.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var detail)
            && ((code[0] == '4' && detail <= 31) || (code[0] == '5' && detail <= 5));
    }

    /// <summary>A JSON-RPC error code, a whole number with an optional sign, and a message.</summary>
    private static bool IsJsonRpcError(string reply)
    {
        var space = reply.IndexOf(' ', StringComparison.Ordinal);
        return space > 0
            && space < reply.Length - 1
            && int.TryParse(reply.AsSpan(0, space), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
    }

    /// <summary>Eight hex digits whose severity bits say error (0xC0000000 and up).</summary>
    private static bool IsNtStatusError(string reply) =>
        reply.Length == 8
        && uint.TryParse(reply, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var status)
        && status >= 0xC0000000;

    /// <summary>Splits a reply at its first space into a decimal code and the text after it (null when no space).</summary>
    private static bool TrySplitCode(string reply, out int code, out string? text)
    {
        var space = reply.IndexOf(' ', StringComparison.Ordinal);
        text = space < 0 ? null : reply[(space + 1)..];
        return int.TryParse(space < 0 ? reply : reply[..space], NumberStyles.None, CultureInfo.InvariantCulture, out code);
    }
}
