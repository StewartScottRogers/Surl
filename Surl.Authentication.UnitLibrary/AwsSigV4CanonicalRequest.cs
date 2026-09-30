using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// The canonical request of AWS Signature Version 4, rebuilt from a received request the way
/// upstream curl 8.21.0's <c>--aws-sigv4</c> built it before signing (ADR-0043): the method, the
/// path (percent-encoded again for every service but <c>s3</c>), the query (each parameter
/// re-encoded, then sorted), each signed header as <c>name:value</c>, the signed header list and
/// the payload hash, one per line.
/// </summary>
internal static class AwsSigV4CanonicalRequest
{
    private const string UnreservedPunctuation = "-._~";

    /// <summary>
    /// Builds the canonical request, or <see langword="null"/> when a signed header is missing
    /// from the request or its target has no path.
    /// </summary>
    /// <param name="request">The request as received.</param>
    /// <param name="authorization">The credentials, for the signed headers and the service.</param>
    /// <param name="payloadHash">The payload hash, lower-case hex or <c>UNSIGNED-PAYLOAD</c>.</param>
    /// <returns>The canonical request, or <see langword="null"/>.</returns>
    public static string? Build(
        HttpAuthenticationRequest request, AwsSigV4Authorization authorization, string payloadHash)
    {
        var headers = BuildHeaders(request.Fields, authorization.SignedHeaders);
        var pathAndQuery = ReadPathAndQuery(request.Target);
        if (headers is null || pathAndQuery is null)
        {
            return null;
        }

        var (path, query) = pathAndQuery.Value;
        var canonicalPath = string.Equals(authorization.Service, "s3", StringComparison.OrdinalIgnoreCase)
            ? path
            : Encode(path, keepSlash: true);

        return string.Join(
            '\n',
            request.Method,
            canonicalPath,
            CanonicaliseQuery(query),
            headers,
            string.Join(';', authorization.SignedHeaders),
            payloadHash);
    }

    // The target in origin form (/path?query) or absolute form (scheme://authority/path?query).
    private static (string Path, string Query)? ReadPathAndQuery(string target)
    {
        var pathStart = FindPathStart(target);
        if (pathStart < 0)
        {
            return null;
        }

        var pathAndQuery = target[pathStart..].Split('?', 2);

        return (pathAndQuery[0], pathAndQuery.Length == 2 ? pathAndQuery[1] : string.Empty);
    }

    // 0 in origin form, the first / after the authority in absolute form, otherwise -1.
    private static int FindPathStart(string target)
    {
        if (target.StartsWith('/'))
        {
            return 0;
        }

        var authority = target.IndexOf("://", StringComparison.Ordinal);

        return authority < 0 ? -1 : target.IndexOf('/', authority + 3);
    }

    // Each signed header's values, trimmed with inner runs of spaces made one, joined with a
    // comma; every line ends with a line feed, so the block ends with a blank line.
    private static string? BuildHeaders(
        IReadOnlyList<KeyValuePair<string, string>> fields, IReadOnlyList<string> signedHeaders)
    {
        var lines = new StringBuilder();
        foreach (var name in signedHeaders)
        {
            var values = fields
                .Where(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase))
                .Select(field => CollapseSpaces(field.Value))
                .ToList();
            if (values.Count == 0)
            {
                return null;
            }

            lines.Append(name).Append(':').AppendJoin(',', values).Append('\n');
        }

        return lines.ToString();
    }

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));

    // Each parameter split at its first = (none reads as an empty value), its name and value
    // re-encoded, then sorted by name and then by value, as measured (Fixtures/README.md).
    private static string CanonicaliseQuery(string query)
    {
        var parameters = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(parameter => parameter.Split('=', 2))
            .Select(pair => (Name: EncodeQueryPart(pair[0]), Value: pair.Length == 2 ? EncodeQueryPart(pair[1]) : string.Empty))
            .OrderBy(pair => pair.Name, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .Select(pair => $"{pair.Name}={pair.Value}");

        return string.Join('&', parameters);
    }

    // A + is a space and %XX the byte it names; each byte is then written as Encode writes it,
    // so an escaped unreserved character is unescaped and a % before no hex digits is escaped.
    private static string EncodeQueryPart(string part)
    {
        var decoded = new StringBuilder();
        for (var index = 0; index < part.Length; index++)
        {
            var escaped = IsPercentEscape(part, index);
            _ = escaped
                ? decoded.Append((char)Convert.ToByte(part.Substring(index + 1, 2), 16))
                : decoded.Append(part[index] == '+' ? ' ' : part[index]);
            index += escaped ? 2 : 0;
        }

        return Encode(decoded.ToString(), keepSlash: false);
    }

    private static bool IsPercentEscape(string text, int index) =>
        text[index] == '%'
        && index + 2 < text.Length
        && char.IsAsciiHexDigit(text[index + 1])
        && char.IsAsciiHexDigit(text[index + 2]);

    private static string Encode(string text, bool keepSlash)
    {
        var encoded = new StringBuilder();
        foreach (var character in text)
        {
            AppendEncoded(encoded, character, keepSlash);
        }

        return encoded.ToString();
    }

    // The HTTP server reads each byte as one character (Latin-1), so each character is one byte.
    private static void AppendEncoded(StringBuilder encoded, char character, bool keepSlash)
    {
        var kept = char.IsAsciiLetterOrDigit(character)
            || UnreservedPunctuation.Contains(character)
            || keepSlash && character == '/';
        _ = kept ? encoded.Append(character) : encoded.Append('%').Append(((byte)character).ToString("X2"));
    }
}
