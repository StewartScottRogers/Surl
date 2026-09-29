using System.Text;

namespace Surl.Protocol.Http;

/// <summary>
/// Parses a header field line, <c>field-name ":" OWS field-value OWS</c> (RFC 9112,
/// section 5).
/// </summary>
internal static class HttpFieldLineParser
{
    /// <summary>
    /// Parses <paramref name="line"/>, its line ending already removed and not empty.
    /// </summary>
    /// <param name="line">The field line.</param>
    /// <param name="field">The field, when the line is well formed.</param>
    /// <returns>
    /// <see langword="null"/> when the line is well formed;
    /// <see cref="HttpRequestHeadReadOutcome.WhitespaceBeforeColon"/> when whitespace ends
    /// the name (RFC 9112, section 5.1); otherwise
    /// <see cref="HttpRequestHeadReadOutcome.MalformedHeaderField"/>, which includes an
    /// obsolete line folding (section 5.2).
    /// </returns>
    public static HttpRequestHeadReadOutcome? Parse(ReadOnlySpan<byte> line, out HttpRequestField? field)
    {
        field = null;

        var colon = line.IndexOf((byte)':');
        var name = colon < 0 ? line : line[..colon];
        if (colon > 0 && EndsWithWhitespaceAfterAName(name))
        {
            return HttpRequestHeadReadOutcome.WhitespaceBeforeColon;
        }

        var value = HttpSyntax.TrimOptionalWhitespace(line[(colon + 1)..]);
        if (colon < 0 || !IsNameAndValue(name, value))
        {
            return HttpRequestHeadReadOutcome.MalformedHeaderField;
        }

        field = new HttpRequestField(Encoding.ASCII.GetString(name), Encoding.Latin1.GetString(value));

        return null;
    }

    // Whitespace at the start is an obsolete line folding, reported as malformed instead.
    private static bool EndsWithWhitespaceAfterAName(ReadOnlySpan<byte> name) =>
        !HttpSyntax.IsOptionalWhitespace(name[0]) && HttpSyntax.IsOptionalWhitespace(name[^1]);

    private static bool IsNameAndValue(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value) =>
        HttpSyntax.IsToken(name) && HttpSyntax.IsFieldValue(value);
}
