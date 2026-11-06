namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// Tells apart the parse failures the BCL's <see cref="System.Xml.XmlReader"/> reports alike
/// (ADR-0114 decision 6): it rejects a DTD, an undeclared entity reference and an encoding it cannot
/// switch to with the same exception as XML that is simply not well-formed, so the most recent
/// bytes read are searched for what RFC 6120 section 11 forbids.
/// </summary>
internal static class XmppParseFailures
{
    private static readonly string[] PredefinedEntities = ["amp", "lt", "gt", "quot", "apos"];

    /// <summary>
    /// The stream error for a parse failure: <c>restricted-xml</c> when the text holds a DTD or an
    /// entity reference other than the five predefined and character references,
    /// <c>unsupported-encoding</c> when it holds an XML declaration naming an encoding other than
    /// UTF-8, else <c>not-well-formed</c>.
    /// </summary>
    /// <param name="recentText">The most recent text read, holding the point the parse failed at.</param>
    /// <returns>One of <see cref="XmppStreamErrors"/>.</returns>
    public static string Condition(string recentText)
    {
        if (recentText.Contains("<!DOCTYPE", StringComparison.Ordinal) || HasUndeclaredEntityReference(recentText))
        {
            return XmppStreamErrors.RestrictedXml;
        }

        return DeclaresAnotherEncoding(recentText) ? XmppStreamErrors.UnsupportedEncoding : XmppStreamErrors.NotWellFormed;
    }

    private static bool HasUndeclaredEntityReference(string text)
    {
        for (var ampersand = text.IndexOf('&', StringComparison.Ordinal); ampersand >= 0; ampersand = text.IndexOf('&', ampersand + 1))
        {
            var semicolon = text.IndexOf(';', ampersand);
            if (semicolon > ampersand + 1 && IsEntityName(text[(ampersand + 1)..semicolon]))
            {
                return true;
            }
        }

        return false;
    }

    // A name other than the predefined five; a character reference starts with '#', which no name does.
    private static bool IsEntityName(string name) =>
        !PredefinedEntities.Contains(name) && IsNameStart(name[0]) && !name.Any(IsNameEnd);

    private static bool IsNameStart(char character) => char.IsLetter(character) || character is '_' or ':';

    private static bool IsNameEnd(char character) => char.IsWhiteSpace(character) || character is '<' or '&';

    private static bool DeclaresAnotherEncoding(string text)
    {
        var declaration = text.IndexOf("<?xml", StringComparison.Ordinal);
        var end = declaration < 0 ? -1 : text.IndexOf("?>", declaration, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        // Whitespace may surround '=' and either quote may delimit the value (XML 1.0 [23], [80]).
        var attributes = string.Concat(text[declaration..end].Where(character => !char.IsWhiteSpace(character))).Replace('"', '\'');
        var encoding = attributes.IndexOf("encoding='", StringComparison.Ordinal);
        return encoding >= 0 && !attributes[(encoding + "encoding='".Length)..].StartsWith("utf-8'", StringComparison.OrdinalIgnoreCase);
    }
}
