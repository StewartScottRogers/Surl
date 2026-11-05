using System.Text;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// The header escapes of the specification's "Value Encoding" (ADR-0118 decision 0): in 1.2,
/// <c>\r</c>, <c>\n</c>, <c>\c</c> and <c>\\</c>; in 1.1, the same without <c>\r</c>; in 1.0,
/// none. CONNECT and CONNECTED are never escaped; the callers decide that.
/// </summary>
internal static class StompHeaderEscaping
{
    // The characters the escapes stand for; EscapeLetters gives each one's letter, in this order.
    private const string EscapedCharacters = "\n:\\\r";

    /// <summary>
    /// Decodes <paramref name="text"/>'s escapes for <paramref name="version"/>; false for an
    /// escape the version does not define, a fatal malformed frame.
    /// </summary>
    /// <param name="text">A header name or value as sent.</param>
    /// <param name="version">The connection's version.</param>
    /// <param name="decoded">The decoded text, or empty when false is returned.</param>
    public static bool TryDecode(string text, StompVersion version, out string decoded)
    {
        decoded = text;
        if (version == StompVersion.V1_0 || !text.Contains('\\', StringComparison.Ordinal))
        {
            return true;
        }

        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\\')
            {
                builder.Append(text[index]);
                continue;
            }

            index++;
            var escaped = index < text.Length ? Unescape(text[index], version) : null;
            if (escaped is null)
            {
                decoded = string.Empty;
                return false;
            }

            builder.Append(escaped.Value);
        }

        decoded = builder.ToString();
        return true;
    }

    /// <summary>Escapes what <paramref name="version"/> can escape in <paramref name="text"/>.</summary>
    /// <param name="text">A header name or value the server writes.</param>
    /// <param name="version">The connection's version.</param>
    public static string Encode(string text, StompVersion version)
    {
        if (version == StompVersion.V1_0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            builder.Append(Escape(character, version));
        }

        return builder.ToString();
    }

    private static char? Unescape(char escape, StompVersion version)
    {
        var index = EscapeLetters(version).IndexOf(escape, StringComparison.Ordinal);
        return index < 0 ? null : EscapedCharacters[index];
    }

    private static string Escape(char character, StompVersion version)
    {
        var index = EscapedCharacters.IndexOf(character, StringComparison.Ordinal);
        var letters = EscapeLetters(version);
        return index < 0 || index >= letters.Length ? character.ToString() : string.Concat("\\", letters[index].ToString());
    }

    // The letter after the backslash for each of EscapedCharacters, in the same order; 1.1 has no \r.
    private static string EscapeLetters(StompVersion version) => version == StompVersion.V1_2 ? "nc\\r" : "nc\\";
}
