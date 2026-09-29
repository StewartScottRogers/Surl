using System.Text;

namespace Surl.Authentication;

/// <summary>
/// Reads the <c>auth-param</c> list after <c>Digest</c> in an <c>Authorization</c> field
/// (RFC 9110 section 11.2): <c>name=token</c> or <c>name="quoted-string"</c>, separated by
/// commas and optional whitespace, a backslash in a quoted string escaping the next character.
/// </summary>
internal static class DigestParameterParser
{
    /// <summary>
    /// Splits <paramref name="credentials"/> into its parameters, names matched
    /// case-insensitively and quoted values unescaped.
    /// </summary>
    /// <param name="credentials">What followed <c>Digest</c> and its spaces.</param>
    /// <returns>
    /// The parameters, or <see langword="null"/> when the list is malformed or names a
    /// parameter twice.
    /// </returns>
    public static Dictionary<string, string>? Parse(string credentials)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var position = SkipSeparators(credentials, 0);
        while (position < credentials.Length)
        {
            if (!TryReadParameter(credentials, ref position, out var name, out var value)
                || !parameters.TryAdd(name, value))
            {
                return null;
            }

            position = SkipSeparators(credentials, position);
        }

        return parameters;
    }

    private static bool TryReadParameter(string text, ref int position, out string name, out string value)
    {
        name = ReadToken(text, ref position);
        value = string.Empty;
        position = SkipWhitespace(text, position);
        if (name.Length == 0 || !IsAt(text, position, '='))
        {
            return false;
        }

        position = SkipWhitespace(text, position + 1);

        return TryReadValue(text, ref position, out value)
            && (position == text.Length || IsSeparator(text[position]));
    }

    private static bool TryReadValue(string text, ref int position, out string value)
    {
        if (IsAt(text, position, '"'))
        {
            return TryReadQuoted(text, ref position, out value);
        }

        value = ReadToken(text, ref position);

        return value.Length > 0;
    }

    private static bool IsAt(string text, int position, char character) =>
        position < text.Length && text[position] == character;

    private static string ReadToken(string text, ref int position)
    {
        var start = position;
        while (position < text.Length && !IsSeparator(text[position]) && text[position] is not ('=' or '"'))
        {
            position++;
        }

        return text[start..position];
    }

    private static bool TryReadQuoted(string text, ref int position, out string value)
    {
        var unescaped = new StringBuilder();
        for (position++; position < text.Length; position++)
        {
            var character = text[position];
            if (character == '"')
            {
                position++;
                value = unescaped.ToString();

                return true;
            }

            if (character == '\\' && position + 1 < text.Length)
            {
                character = text[++position];
            }

            unescaped.Append(character);
        }

        value = string.Empty;

        return false;
    }

    private static int SkipSeparators(string text, int position)
    {
        while (position < text.Length && IsSeparator(text[position]))
        {
            position++;
        }

        return position;
    }

    private static int SkipWhitespace(string text, int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t')
        {
            position++;
        }

        return position;
    }

    private static bool IsSeparator(char character) => character is ' ' or '\t' or ',';
}
