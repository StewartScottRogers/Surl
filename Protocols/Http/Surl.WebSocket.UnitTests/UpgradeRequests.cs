using Surl.HttpMessage;

namespace Surl.WebSocket;

/// <summary>
/// Upgrade request heads built from RFC 6455 section 1.3's example handshake, one field
/// changed or added by the test.
/// </summary>
internal static class UpgradeRequests
{
    /// <summary>The <c>Sec-WebSocket-Key</c> of section 1.3's example.</summary>
    public const string SampleKey = "dGhlIHNhbXBsZSBub25jZQ==";

    /// <summary>The <c>Sec-WebSocket-Accept</c> section 1.3 computes for <see cref="SampleKey"/>.</summary>
    public const string SampleAccept = "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=";

    /// <summary>
    /// A head that passes every check: <c>GET /chat HTTP/1.1</c> with <c>Host</c>,
    /// <c>Upgrade</c>, <c>Connection</c>, the sample key and version 13, each field of
    /// <paramref name="replacements"/> replacing the field of its name (removed when its value
    /// is <see langword="null"/>) or added when there is none.
    /// </summary>
    public static HttpRequestHead Head(string method = "GET", int minorVersion = 1, params (string Name, string? Value)[] replacements)
    {
        var fields = new List<(string Name, string? Value)>
        {
            ("Host", "server.example.com"),
            ("Upgrade", "websocket"),
            ("Connection", "Upgrade"),
            ("Sec-WebSocket-Key", SampleKey),
            ("Sec-WebSocket-Version", "13"),
        };
        foreach (var replacement in replacements)
        {
            var index = fields.FindIndex(field => field.Name == replacement.Name);
            if (index < 0)
            {
                fields.Add(replacement);
            }
            else
            {
                fields[index] = replacement;
            }
        }

        return new HttpRequestHead(
            method,
            "/chat",
            new Version(1, minorVersion),
            fields.Where(field => field.Value is not null).Select(field => new HttpRequestField(field.Name, field.Value!)));
    }
}
