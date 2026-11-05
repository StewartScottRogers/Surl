using System.Globalization;
using System.Text;
using Surl.HttpMessage;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// A response head an origin sent the proxy: its status line and fields (RFC 9112 sections 4
/// and 5).
/// </summary>
/// <param name="MinorVersion">The minor version of the origin's <c>HTTP/1.x</c>.</param>
/// <param name="StatusCode">The three-digit status code.</param>
/// <param name="ReasonPhrase">The reason phrase, possibly empty.</param>
/// <param name="Fields">The header fields, in order.</param>
internal sealed record OriginResponseHead(int MinorVersion, int StatusCode, string ReasonPhrase, IReadOnlyList<HttpRequestField> Fields)
{
    /// <summary>
    /// Whether the response can carry no body whatever its fields say: a <c>1xx</c>, <c>204</c>
    /// or <c>304</c> (RFC 9112 section 6.3).
    /// </summary>
    public bool HasNoBody => StatusCode is < 200 or 204 or 304;

    /// <summary>Whether the origin keeps its connection after this response.</summary>
    public bool KeepsConnection =>
        MinorVersion >= 1 && !ProxyFieldRules.ListsToken(Fields, "Connection", "close");

    /// <summary>
    /// Reads a status line: <c>HTTP/1.</c>, one digit, a space, three digits, then nothing or a
    /// space and the reason phrase.
    /// </summary>
    /// <param name="line">The line, without its line feed; a trailing carriage return is ignored.</param>
    /// <returns>The version, code and reason, or <see langword="null"/> when the line is not a status line.</returns>
    public static (int MinorVersion, int StatusCode, string ReasonPhrase)? ParseStatusLine(byte[] line)
    {
        var text = Encoding.Latin1.GetString(line).TrimEnd('\r');
        var hasShape = text.Length >= 12 && text.StartsWith("HTTP/1.", StringComparison.Ordinal) && char.IsAsciiDigit(text[7]) && text[8] == ' '
            && (text.Length == 12 || text[12] == ' ');
        if (!hasShape || !int.TryParse(text.AsSpan(9, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var code) || code < 100)
        {
            return null;
        }

        return (text[7] - '0', code, text.Length > 12 ? text[13..] : string.Empty);
    }

    /// <summary>
    /// Reads a field line (RFC 9112 section 5): a token name, a colon, and a value of visible
    /// characters, spaces and tabs, trimmed.
    /// </summary>
    /// <param name="line">The line, without its line feed; a trailing carriage return is ignored.</param>
    /// <returns>The field, or <see langword="null"/> when the line is not a field line.</returns>
    public static HttpRequestField? ParseFieldLine(byte[] line)
    {
        var text = Encoding.Latin1.GetString(line).TrimEnd('\r');
        var colon = text.IndexOf(':');
        var name = colon < 0 ? string.Empty : text[..colon];
        var value = text[(colon + 1)..].Trim(' ', '\t');

        return name.Length > 0 && name.All(IsTokenCharacter) && !value.Any(IsForbiddenInValue) ? new HttpRequestField(name, value) : null;
    }

    /// <summary>
    /// The head relayed to curl: <c>HTTP/1.1</c>, the origin's status and reason, and
    /// <paramref name="relayedFields"/>.
    /// </summary>
    /// <param name="relayedFields">The fields to write, from <see cref="ProxyFieldRules.RelayedResponseFields"/>.</param>
    /// <returns>The head's bytes, ending with the empty line.</returns>
    public byte[] ToRelayedBytes(IReadOnlyList<HttpRequestField> relayedFields)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"HTTP/1.1 {StatusCode} {ReasonPhrase}\r\n");
        foreach (var field in relayedFields)
        {
            text.Append(field.Name).Append(": ").Append(field.Value).Append("\r\n");
        }

        return Encoding.Latin1.GetBytes(text.Append("\r\n").ToString());
    }

    private static bool IsForbiddenInValue(char character) => character is (< ' ' and not '\t') or '\x7F';

    private static bool IsTokenCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character);
}
