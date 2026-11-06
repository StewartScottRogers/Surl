using System.Buffers.Text;
using Surl.HttpServing;

namespace Surl.Protocol.Http;

/// <summary>
/// Decides whether an HTTP/1.1 request on a cleartext connection is upgraded to HTTP/2 with
/// <c>Upgrade: h2c</c> (RFC 7540 section 3.2; ADR-0081, decision 3; ADR-0091), and reads the
/// client's settings from its <c>HTTP2-Settings</c> field.
/// </summary>
/// <remarks>
/// A request qualifies when it is HTTP/1.1, its <c>Upgrade</c> names <c>h2c</c>, it has exactly
/// one <c>HTTP2-Settings</c> field whose base64url value (RFC 7540 section 3.2.1) decodes to whole
/// <c>SETTINGS</c> entries the frame parser accepts, its <c>Connection</c> names both
/// <c>Upgrade</c> and <c>HTTP2-Settings</c>, and it announces no body (no <c>Transfer-Encoding</c>,
/// no <c>Content-Length</c> other than 0). Any other request ignores the upgrade.
/// </remarks>
internal static class Http2CleartextUpgrade
{
    /// <summary>The upgrade token for HTTP/2 over cleartext TCP (RFC 7540 section 3.2).</summary>
    public const string UpgradeToken = "h2c";

    private const string SettingsFieldName = "HTTP2-Settings";

    /// <summary>
    /// The client's settings when <paramref name="request"/> qualifies for the upgrade, or
    /// <see langword="null"/> when its upgrade is ignored.
    /// </summary>
    /// <param name="request">The HTTP/1.x request.</param>
    /// <returns>The settings its <c>HTTP2-Settings</c> carries, or <see langword="null"/>.</returns>
    public static IReadOnlyList<Http2Setting>? ClientSettingsOf(HttpRequest request)
    {
        var settingsFields = Values(request, SettingsFieldName);
        var qualifies = IsHttp11(request.Http1Version)
            && AsksForH2c(request)
            && settingsFields.Count == 1
            && !AnnouncesBody(request);

        return qualifies ? DecodeSettings(settingsFields[0]) : null;
    }

    // RFC 9110 section 7.8: an HTTP/1.0 request's Upgrade is ignored.
    private static bool IsHttp11(Version? version) => version is { Major: 1, Minor: >= 1 };

    private static bool AsksForH2c(HttpRequest request)
    {
        var connectionTokens = Tokens(request, "Connection");

        return Tokens(request, "Upgrade").Contains(UpgradeToken, StringComparer.Ordinal)
            && connectionTokens.Contains("Upgrade", StringComparer.OrdinalIgnoreCase)
            && connectionTokens.Contains(SettingsFieldName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool AnnouncesBody(HttpRequest request) =>
        Values(request, "Transfer-Encoding").Count > 0
        || Values(request, "Content-Length").Any(value => value.Trim() != "0");

    // RFC 7540 section 3.2.1: base64url with the trailing '=' left out, of a SETTINGS payload.
    private static IReadOnlyList<Http2Setting>? DecodeSettings(string value)
    {
        var text = value.Trim();
        if (!Base64Url.IsValid(text, out var length))
        {
            return null;
        }

        var payload = new byte[length];
        Base64Url.DecodeFromChars(text, payload);
        return Http2SettingsPayload.Decode(payload);
    }

    private static List<string> Values(HttpRequest request, string name) =>
        [.. request.Fields.Where(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase)).Select(field => field.Value)];

    private static List<string> Tokens(HttpRequest request, string name) =>
        [.. Values(request, name).SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))];
}
