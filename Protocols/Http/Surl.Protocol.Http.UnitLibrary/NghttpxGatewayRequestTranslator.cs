using System.Globalization;
using System.Text;

namespace Surl.Protocol.Http;

/// <summary>
/// Writes the HTTP/1.1 request upstream's nghttpx gateway forwards to the case's <c>http</c> half
/// for an HTTP/2 or HTTP/3 request (ADR-0154 decision 2). Pure: no socket and no stream state.
/// </summary>
public static class NghttpxGatewayRequestTranslator
{
    private const string Http2ViaValue = "2 nghttpx";
    private const string Http3ViaValue = "3 nghttpx";

    private static readonly HashSet<string> RemovedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade", "http2-settings", "host",
    };

    /// <summary>Writes the forwarded HTTP/1.1 request: request line, fields, blank line and body.</summary>
    /// <param name="version">The frontend the request arrived on.</param>
    /// <param name="method">The <c>:method</c> pseudo-field.</param>
    /// <param name="scheme">The <c>:scheme</c> pseudo-field, or null for <c>http</c>.</param>
    /// <param name="authority">The <c>:authority</c> pseudo-field, or null when the client sent <c>Host</c> instead.</param>
    /// <param name="path">The <c>:path</c> pseudo-field.</param>
    /// <param name="fields">The client's ordinary fields, in arrival order, any capitalisation.</param>
    /// <param name="body">The request body, empty for none.</param>
    /// <returns>The request bytes.</returns>
    public static byte[] Translate(
        NghttpxGatewayVersion version,
        string method,
        string? scheme,
        string? authority,
        string path,
        IReadOnlyList<KeyValuePair<string, string>> fields,
        ReadOnlySpan<byte> body)
    {
        string forwardedScheme = scheme ?? "http";
        string target = version == NghttpxGatewayVersion.Http3 ? $"{forwardedScheme}://{authority}{path}" : path;
        string host = authority ?? FirstValue(fields, "host") ?? string.Empty;
        bool chunked = body.Length > 0 && FirstValue(fields, "content-length") is null;

        StringBuilder head = new();
        head.Append(method).Append(' ').Append(target).Append(" HTTP/1.1\r\n");
        head.Append("Host: ").Append(host).Append("\r\n");
        AppendClientFields(head, fields);
        if (chunked)
        {
            head.Append("Transfer-Encoding: chunked\r\n");
        }

        if (version == NghttpxGatewayVersion.Http2)
        {
            head.Append("X-Forwarded-Proto: ").Append(forwardedScheme).Append("\r\n");
        }

        head.Append("Via: ").Append(JoinVia(version, fields)).Append("\r\n\r\n");
        byte[] headBytes = Encoding.Latin1.GetBytes(head.ToString());
        return chunked ? Concatenate(headBytes, Chunk(body)) : Concatenate(headBytes, body);
    }

    /// <summary>Capitalises each hyphen-separated part of a lower-cased field name (<c>user-agent</c> to <c>User-Agent</c>).</summary>
    /// <param name="name">The field name in any capitalisation.</param>
    /// <returns>The canonical capitalisation.</returns>
    public static string CanonicalName(string name)
    {
        char[] letters = name.ToLowerInvariant().ToCharArray();
        for (int index = 0; index < letters.Length; index++)
        {
            if (index == 0 || letters[index - 1] == '-')
            {
                letters[index] = char.ToUpperInvariant(letters[index]);
            }
        }

        return new string(letters);
    }

    private static void AppendClientFields(StringBuilder head, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        List<string> cookies = [];
        foreach (KeyValuePair<string, string> field in fields)
        {
            if (field.Key.Equals("cookie", StringComparison.OrdinalIgnoreCase))
            {
                cookies.Add(field.Value);
            }
            else if (IsForwarded(field))
            {
                head.Append(CanonicalName(field.Key)).Append(": ").Append(field.Value).Append("\r\n");
            }
        }

        if (cookies.Count > 0)
        {
            head.Append("Cookie: ").Append(string.Join("; ", cookies)).Append("\r\n");
        }
    }

    private static bool IsForwarded(KeyValuePair<string, string> field) =>
        !field.Key.StartsWith(':')
        && !RemovedFields.Contains(field.Key)
        && !field.Key.Equals("via", StringComparison.OrdinalIgnoreCase)
        && !field.Key.Equals("x-forwarded-proto", StringComparison.OrdinalIgnoreCase)
        && (!field.Key.Equals("te", StringComparison.OrdinalIgnoreCase)
            || field.Value.Equals("trailers", StringComparison.OrdinalIgnoreCase));

    private static string JoinVia(NghttpxGatewayVersion version, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        List<string> values = [];
        foreach (KeyValuePair<string, string> field in fields)
        {
            if (field.Key.Equals("via", StringComparison.OrdinalIgnoreCase))
            {
                values.Add(field.Value);
            }
        }

        values.Add(version == NghttpxGatewayVersion.Http3 ? Http3ViaValue : Http2ViaValue);
        return string.Join(", ", values);
    }

    private static string? FirstValue(IReadOnlyList<KeyValuePair<string, string>> fields, string name)
    {
        foreach (KeyValuePair<string, string> field in fields)
        {
            if (field.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return field.Value;
            }
        }

        return null;
    }

    private static byte[] Chunk(ReadOnlySpan<byte> body)
    {
        byte[] size = Encoding.ASCII.GetBytes(body.Length.ToString("x", CultureInfo.InvariantCulture) + "\r\n");
        return Concatenate(Concatenate(size, body), "\r\n0\r\n\r\n"u8);
    }

    private static byte[] Concatenate(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        byte[] joined = new byte[first.Length + second.Length];
        first.CopyTo(joined);
        second.CopyTo(joined.AsSpan(first.Length));
        return joined;
    }
}
