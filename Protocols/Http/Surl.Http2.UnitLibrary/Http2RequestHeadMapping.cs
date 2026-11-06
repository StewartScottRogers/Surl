using System.Globalization;
using Surl.Hpack;

namespace Surl.Http2;

/// <summary>
/// Maps a decoded HTTP/2 request header section to an <see cref="Http2RequestHead"/>, or names
/// what makes the request malformed (RFC 9113 sections 8.1.1, 8.2 and 8.3.1), which the stream
/// answers with <c>RST_STREAM</c> <c>PROTOCOL_ERROR</c> (ADR-0081, decision 5).
/// </summary>
/// <remarks>
/// Malformed: a pseudo-header field after a regular one, one that is unknown (<c>:protocol</c>
/// included, since the server never enables extended <c>CONNECT</c>; ADR-0081, decision 9) or
/// repeated, or a missing <c>:method</c>; a request other than <c>CONNECT</c> without
/// <c>:scheme</c> or with an empty or missing <c>:path</c>, and a <c>CONNECT</c> without
/// <c>:authority</c> or with <c>:scheme</c> or <c>:path</c>; a field name that is empty or holds
/// an uppercase letter, a control character, a space or a byte past 0x7E; a value holding NUL,
/// CR or LF or starting or ending with a space or tab; a connection-specific field
/// (<c>connection</c>, <c>keep-alive</c>, <c>proxy-connection</c>, <c>transfer-encoding</c>,
/// <c>upgrade</c>) or a <c>te</c> other than <c>trailers</c>; and <c>content-length</c> values
/// that are not one number of decimal digits. HTTP/3 applies the same rules (RFC 9114 sections 4.2
/// and 4.3) with its own copy, <c>Surl.Http3</c>'s <c>Http3RequestHeadMapping</c> (ADR-0086, decision 5).
/// </remarks>
public static class Http2RequestHeadMapping
{
    private static readonly string[] PseudoHeaderFields = [":method", ":scheme", ":authority", ":path"];

    private static readonly string[] ConnectionSpecificFields = ["connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade"];

    /// <summary>
    /// Maps <paramref name="fields"/>.
    /// </summary>
    /// <param name="fields">The decoded header section, in the order received.</param>
    /// <returns>The head, or <see langword="null"/> and why the request is malformed.</returns>
    public static (Http2RequestHead? Head, string? Malformation) Map(IReadOnlyList<HpackHeaderField> fields)
    {
        var pseudoHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
        var regularFields = new List<KeyValuePair<string, string>>();
        foreach (var field in fields)
        {
            var malformation = field.Name.StartsWith(':')
                ? AddPseudoHeader(pseudoHeaders, field, regularFields.Count > 0)
                : RegularFieldMalformation(field);
            if (malformation is not null)
            {
                return (null, malformation);
            }

            if (!field.Name.StartsWith(':'))
            {
                regularFields.Add(new(field.Name, field.Value));
            }
        }

        return MapHead(pseudoHeaders, regularFields);
    }

    private static string? AddPseudoHeader(Dictionary<string, string> pseudoHeaders, HpackHeaderField field, bool followsRegularField)
    {
        if (followsRegularField)
        {
            return $"the pseudo-header field {field.Name} follows a regular field";
        }

        if (!PseudoHeaderFields.Contains(field.Name, StringComparer.Ordinal))
        {
            return $"the pseudo-header field {field.Name} is not one a request may carry";
        }

        return pseudoHeaders.TryAdd(field.Name, field.Value) ? null : $"the pseudo-header field {field.Name} is repeated";
    }

    private static string? RegularFieldMalformation(HpackHeaderField field)
    {
        if (!IsAllowedName(field.Name))
        {
            return "a field name is empty or holds an uppercase letter or a character a field name may not";
        }

        if (!IsAllowedValue(field.Value))
        {
            return $"the {field.Name} field's value holds NUL, CR or LF, or starts or ends with whitespace";
        }

        return IsConnectionSpecific(field) ? $"the connection-specific field {field.Name} is not allowed in HTTP/2 or HTTP/3" : null;
    }

    // RFC 9113 section 8.2.1: no byte 0x00-0x20, 0x41-0x5A (uppercase) or 0x7F and above.
    private static bool IsAllowedName(string name) =>
        name.Length > 0 && !name.AsSpan().ContainsAnyExceptInRange('!', '~') && !name.AsSpan().ContainsAnyInRange('A', 'Z');

    // RFC 9113 section 8.2.1: no NUL, CR or LF, and no space or tab at either end.
    private static bool IsAllowedValue(string value) =>
        !value.AsSpan().ContainsAny("\0\r\n") && value.Trim(' ', '\t').Length == value.Length;

    private static bool IsConnectionSpecific(HpackHeaderField field) =>
        ConnectionSpecificFields.Contains(field.Name, StringComparer.Ordinal) || (field.Name == "te" && field.Value != "trailers");

    private static (Http2RequestHead? Head, string? Malformation) MapHead(Dictionary<string, string> pseudoHeaders, List<KeyValuePair<string, string>> fields)
    {
        if (!pseudoHeaders.TryGetValue(":method", out var method))
        {
            return (null, "the request has no :method");
        }

        var malformation = method == "CONNECT" ? ConnectMalformation(pseudoHeaders) : RequestTargetMalformation(pseudoHeaders);
        if (malformation is not null)
        {
            return (null, malformation);
        }

        var (contentLength, isContentLengthValid) = ContentLength(fields);
        if (!isContentLengthValid)
        {
            return (null, "the content-length is not one number of decimal digits");
        }

        var target = method == "CONNECT" ? pseudoHeaders[":authority"] : pseudoHeaders[":path"];

        return (new Http2RequestHead(method, target, Authority(pseudoHeaders, fields), fields, contentLength, ExpectsContinue(fields)) { Scheme = pseudoHeaders.GetValueOrDefault(":scheme") }, null);
    }

    private static string? ConnectMalformation(Dictionary<string, string> pseudoHeaders) =>
        !pseudoHeaders.ContainsKey(":authority") || pseudoHeaders.ContainsKey(":scheme") || pseudoHeaders.ContainsKey(":path")
            ? "a CONNECT request needs :authority and may carry neither :scheme nor :path"
            : null;

    private static string? RequestTargetMalformation(Dictionary<string, string> pseudoHeaders) =>
        !pseudoHeaders.ContainsKey(":scheme") || pseudoHeaders.GetValueOrDefault(":path", string.Empty).Length == 0
            ? "the request needs :scheme and a :path that is not empty"
            : null;

    private static string? Authority(Dictionary<string, string> pseudoHeaders, List<KeyValuePair<string, string>> fields)
    {
        if (pseudoHeaders.TryGetValue(":authority", out var authority))
        {
            return authority;
        }

        var hosts = fields.Where(field => field.Key == "host").ToArray();

        return hosts.Length == 1 ? hosts[0].Value : null;
    }

    // Every content-length value must be the same run of decimal digits (RFC 9110 section 8.6).
    private static (long? Length, bool IsValid) ContentLength(List<KeyValuePair<string, string>> fields)
    {
        var values = fields.Where(field => field.Key == "content-length").Select(field => field.Value).Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length == 0)
        {
            return (null, true);
        }

        return values.Length == 1 && long.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var length)
            ? (length, true)
            : (null, false);
    }

    private static bool ExpectsContinue(List<KeyValuePair<string, string>> fields) =>
        fields.Where(field => field.Key == "expect")
            .SelectMany(field => field.Value.Split(','))
            .Any(expectation => string.Equals(expectation.Trim(' ', '\t'), "100-continue", StringComparison.OrdinalIgnoreCase));
}
