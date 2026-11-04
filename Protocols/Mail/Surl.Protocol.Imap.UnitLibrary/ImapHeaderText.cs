using System.Text;

namespace Surl.Protocol.Imap;

/// <summary>
/// Reads the structured text of an RFC 5322 or RFC 2045 header field value: which characters
/// are in a quoted string, a comment or an angle-bracketed address, so a separator is only
/// found outside them.
/// </summary>
internal static class ImapHeaderText
{
    private enum Scan
    {
        Plain,
        Quoted,
        Comment,
        Angle,
    }

    /// <summary>
    /// What each character of <paramref name="text"/> is part of.
    /// </summary>
    /// <param name="text">A field value.</param>
    /// <returns>One role per character.</returns>
    public static ImapHeaderCharRole[] Roles(string text)
    {
        var roles = new ImapHeaderCharRole[text.Length];
        var scan = Scan.Plain;
        var depth = 0;
        for (var index = 0; index < text.Length; index++)
        {
            (roles[index], scan, depth) = Step(scan, depth, text[index]);
            if (IsEscape(text, index, scan))
            {
                // The character a backslash quotes belongs where the backslash does, as itself.
                roles[index + 1] = roles[index];
                index++;
            }
        }

        return roles;
    }

    // A backslash inside a quoted string or a comment, with a character after it.
    private static bool IsEscape(string text, int index, Scan scan) =>
        text[index] == '\\' && scan is Scan.Quoted or Scan.Comment && index + 1 < text.Length;

    /// <summary>
    /// <paramref name="text"/> without its comments, <c>(...)</c> outside quoted strings, nested
    /// or not.
    /// </summary>
    /// <param name="text">A field value.</param>
    /// <returns>The value without them.</returns>
    public static string RemoveComments(string text)
    {
        var roles = Roles(text);
        return string.Concat(text.Where((_, index) => roles[index] != ImapHeaderCharRole.Comment));
    }

    /// <summary>
    /// Splits <paramref name="text"/> at each <paramref name="separator"/> outside quoted
    /// strings, comments and angle brackets.
    /// </summary>
    /// <param name="text">A field value.</param>
    /// <param name="separator">The separator.</param>
    /// <returns>The pieces, one more than the separators found.</returns>
    public static List<string> Split(string text, char separator)
    {
        var roles = Roles(text);
        List<string> pieces = [];
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (roles[index] == ImapHeaderCharRole.Plain && text[index] == separator)
            {
                pieces.Add(text[start..index]);
                start = index + 1;
            }
        }

        pieces.Add(text[start..]);
        return pieces;
    }

    /// <summary>
    /// <paramref name="text"/> with its double quotes removed and each character a backslash
    /// quotes taken as itself.
    /// </summary>
    /// <param name="text">A word or phrase, quoted or not.</param>
    /// <returns>Its value.</returns>
    public static string Unquote(string text)
    {
        var value = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var next = text[index];
            if (next == '\\' && index + 1 < text.Length)
            {
                value.Append(text[++index]);
            }
            else if (next != '"')
            {
                value.Append(next);
            }
        }

        return value.ToString();
    }

    private static (ImapHeaderCharRole Role, Scan Scan, int Depth) Step(Scan scan, int depth, char next) => scan switch
    {
        Scan.Quoted => (ImapHeaderCharRole.Quoted, next == '"' ? Scan.Plain : Scan.Quoted, 0),
        Scan.Comment => InComment(depth, next),
        Scan.Angle => (ImapHeaderCharRole.Angle, next == '>' ? Scan.Plain : Scan.Angle, 0),
        _ => InPlain(next),
    };

    // Comments nest (RFC 5322, section 3.2.2).
    private static (ImapHeaderCharRole Role, Scan Scan, int Depth) InComment(int depth, char next) => next switch
    {
        '(' => (ImapHeaderCharRole.Comment, Scan.Comment, depth + 1),
        ')' => (ImapHeaderCharRole.Comment, depth == 1 ? Scan.Plain : Scan.Comment, depth - 1),
        _ => (ImapHeaderCharRole.Comment, Scan.Comment, depth),
    };

    private static (ImapHeaderCharRole Role, Scan Scan, int Depth) InPlain(char next) => next switch
    {
        '"' => (ImapHeaderCharRole.Quoted, Scan.Quoted, 0),
        '(' => (ImapHeaderCharRole.Comment, Scan.Comment, 1),
        '<' => (ImapHeaderCharRole.Angle, Scan.Angle, 0),
        _ => (ImapHeaderCharRole.Plain, Scan.Plain, 0),
    };
}
