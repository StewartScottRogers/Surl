using System.Text;

namespace Surl.MailStore;

/// <summary>
/// Reads the local part out of an SMTP <c>RCPT TO</c> forward path (RFC 5321 section 4.1.2,
/// ADR-0050 decision 5): the angle brackets are optional, a source route is ignored, a quoted
/// local part is unquoted with its backslash escapes removed, and the domain or address
/// literal is required but otherwise ignored. <c>&lt;Postmaster&gt;</c> with no domain is valid
/// in any case (RFC 5321 section 4.1.1.3). Characters above U+007F are allowed in a local part
/// and a domain, as SMTPUTF8 allows them (RFC 6531).
/// </summary>
internal static class MailRecipientPath
{
    private const string AtomSpecials = "!#$%&'*+-/=?^_`{|}~";

    /// <summary>
    /// Reads the local part of <paramref name="path"/>.
    /// </summary>
    /// <returns><see langword="false"/> when <paramref name="path"/> is not a valid forward path.</returns>
    public static bool TryReadLocalPart(string path, out string localPart)
    {
        localPart = string.Empty;
        var mailbox = WithoutSourceRoute(WithoutAngleBrackets(path));
        if (mailbox is null || !TryReadLocalPartAndDomain(mailbox, out var local, out var domain))
        {
            return false;
        }

        localPart = local;
        return domain is null ? local.Equals("postmaster", StringComparison.OrdinalIgnoreCase) : IsDomain(domain);
    }

    private static string? WithoutAngleBrackets(string path) =>
        path.StartsWith('<') ? (path.Length > 1 && path.EndsWith('>') ? path[1..^1] : null) : path;

    private static string? WithoutSourceRoute(string? path)
    {
        if (path is null || !path.StartsWith('@'))
        {
            return path;
        }

        var colon = path.IndexOf(':');
        return colon < 0 ? null : path[(colon + 1)..];
    }

    private static bool TryReadLocalPartAndDomain(string mailbox, out string localPart, out string? domain)
    {
        var end = mailbox.StartsWith('"') ? TryUnquote(mailbox, out localPart) : ReadDotString(mailbox, out localPart);
        domain = end >= 0 && end < mailbox.Length && mailbox[end] == '@' ? mailbox[(end + 1)..] : null;
        return end == mailbox.Length || domain is not null;
    }

    private static int ReadDotString(string mailbox, out string localPart)
    {
        var at = mailbox.IndexOf('@');
        var end = at < 0 ? mailbox.Length : at;
        localPart = mailbox[..end];
        return IsDotString(localPart) ? end : -1;
    }

    private static bool IsDotString(string text) =>
        text.Split('.').All(atom => atom.Length > 0 && atom.All(IsAtomCharacter));

    private static bool IsAtomCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || AtomSpecials.Contains(character) || character > '\u007F';

    /// <summary>
    /// Unquotes the quoted string <paramref name="mailbox"/> starts with.
    /// </summary>
    /// <returns>The index just past the closing quote, or -1 when the quoted string is invalid.</returns>
    private static int TryUnquote(string mailbox, out string localPart)
    {
        var unquoted = new StringBuilder();
        localPart = string.Empty;
        for (var index = 1; index < mailbox.Length; index++)
        {
            var character = mailbox[index];
            if (character == '"')
            {
                localPart = unquoted.ToString();
                return index + 1;
            }

            if (character == '\\' && ++index < mailbox.Length)
            {
                character = mailbox[index];
            }

            if (!IsQuotedCharacter(character))
            {
                return -1;
            }

            unquoted.Append(character);
        }

        return -1;
    }

    private static bool IsQuotedCharacter(char character) => character is >= ' ' and not '\u007F';

    private static bool IsDomain(string domain) =>
        domain.Length > 0 && domain.All(character => character is > ' ' and not '\u007F' and not '<' and not '>' and not '@');
}
