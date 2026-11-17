using System.Globalization;
using System.Text;

namespace Surl.Protocol.Http;

/// <summary>
/// Reads the HTTP/1 reply bytes a backend sent into the response fields and body nghttpx's HTTP/2 or
/// HTTP/3 frontend writes (ADR-0154 decision 3). Pure: no socket and no stream state.
/// </summary>
public static class NghttpxGatewayResponseTranslator
{
    private static readonly HashSet<string> DroppedFields = new(StringComparer.Ordinal)
    {
        "connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade",
    };

    /// <summary>Translates the backend's reply, dropping interim <c>1xx</c> replies.</summary>
    /// <param name="version">The frontend the response goes back on.</param>
    /// <param name="reply">Every byte the backend sent, up to its close or the end of the body.</param>
    /// <returns>The status, fields and body, or null when the bytes hold no complete reply head.</returns>
    public static NghttpxGatewayResponse? Translate(NghttpxGatewayVersion version, ReadOnlySpan<byte> reply)
    {
        int offset = 0;
        while (true)
        {
            if (!NghttpxGatewayReplyHead.TryRead(reply[offset..], out string[] lines, out int headSize))
            {
                return null;
            }

            int bodyStart = offset + headSize;
            int status = ReadStatus(lines[0]);
            if (status is >= 100 and < 200)
            {
                offset = bodyStart;
                continue;
            }

            List<KeyValuePair<string, string>> backendFields = ReadFields(lines);
            byte[] body = ReadBody(backendFields, reply[bodyStart..]);
            return new NghttpxGatewayResponse(status, BuildFields(version, backendFields), body);
        }
    }

    private static int ReadStatus(string statusLine)
    {
        string[] parts = statusLine.Split(' ', 3);
        return parts.Length > 1 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int status)
            ? status
            : 502;
    }

    private static List<KeyValuePair<string, string>> ReadFields(string[] lines)
    {
        List<KeyValuePair<string, string>> fields = [];
        foreach (string line in lines.Skip(1))
        {
            int colon = line.IndexOf(':');
            if (colon > 0)
            {
                fields.Add(new(line[..colon].ToLowerInvariant(), line[(colon + 1)..].Trim(' ', '\t')));
            }
        }

        return fields;
    }

    private static List<KeyValuePair<string, string>> BuildFields(
        NghttpxGatewayVersion version,
        List<KeyValuePair<string, string>> backendFields)
    {
        bool dechunked = IsChunked(backendFields);
        bool addServer = version == NghttpxGatewayVersion.Http2;
        List<KeyValuePair<string, string>> fields = [];
        List<string> via = [];
        foreach (KeyValuePair<string, string> field in backendFields)
        {
            if (field.Key == "via")
            {
                via.Add(field.Value);
            }
            else if (IsKept(field.Key, addServer, dechunked))
            {
                fields.Add(field);
            }
        }

        if (addServer)
        {
            fields.Add(new("server", "nghttpx"));
        }

        via.Add("1.1 nghttpx");
        fields.Add(new("via", string.Join(", ", via)));
        return fields;
    }

    private static bool IsKept(string name, bool serverReplaced, bool dechunked) =>
        !DroppedFields.Contains(name)
        && !(serverReplaced && name == "server")
        && !(dechunked && name == "content-length");

    private static bool IsChunked(List<KeyValuePair<string, string>> fields) =>
        fields.Any(field => field.Key == "transfer-encoding"
            && field.Value.Contains("chunked", StringComparison.OrdinalIgnoreCase));

    private static byte[] ReadBody(List<KeyValuePair<string, string>> fields, ReadOnlySpan<byte> rest)
    {
        if (IsChunked(fields))
        {
            return Dechunk(rest);
        }

        string? length = fields.FirstOrDefault(field => field.Key == "content-length").Value;
        return length is not null && int.TryParse(length, NumberStyles.None, CultureInfo.InvariantCulture, out int count)
            ? rest[..Math.Min(count, rest.Length)].ToArray()
            : rest.ToArray();
    }

    private static byte[] Dechunk(ReadOnlySpan<byte> rest)
    {
        List<byte> body = [];
        while (true)
        {
            int lineEnd = rest.IndexOf("\r\n"u8);
            if (lineEnd < 0)
            {
                return [.. body];
            }

            string sizeText = Encoding.ASCII.GetString(rest[..lineEnd]).Split(';')[0].Trim();
            if (!int.TryParse(sizeText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int size) || size == 0)
            {
                return [.. body];
            }

            ReadOnlySpan<byte> data = rest[(lineEnd + 2)..];
            int taken = Math.Min(size, data.Length);
            body.AddRange(data[..taken].ToArray());
            rest = data[taken..];
            rest = rest.Length >= 2 ? rest[2..] : [];
        }
    }
}
