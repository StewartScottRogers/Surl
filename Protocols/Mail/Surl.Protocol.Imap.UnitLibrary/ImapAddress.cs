namespace Surl.Protocol.Imap;

/// <summary>
/// One address of an <c>ENVELOPE</c> (RFC 3501, section 7.4.2): a mailbox, or the start or end
/// of a group.
/// </summary>
/// <param name="Name">The display name, or <see langword="null"/>.</param>
/// <param name="Route">The obsolete source route, or <see langword="null"/>.</param>
/// <param name="Mailbox">The local part; for a group's start, the group's name; for its end,
/// <see langword="null"/>.</param>
/// <param name="Host">The domain, empty when the address has none; <see langword="null"/> for a
/// group's start and end.</param>
internal sealed record ImapAddress(string? Name, string? Route, string? Mailbox, string? Host)
{
    /// <summary>
    /// The address that ends a group.
    /// </summary>
    public static readonly ImapAddress GroupEnd = new(null, null, null, null);

    /// <summary>
    /// Reads an address list field value (RFC 5322, section 3.4): mailboxes separated by commas,
    /// each <c>name &lt;local@domain&gt;</c> or <c>local@domain</c>, and groups
    /// <c>name: mailbox, ...;</c>. Comments are left out.
    /// </summary>
    /// <param name="text">The unfolded field value.</param>
    /// <returns>The addresses, in order.</returns>
    public static IReadOnlyList<ImapAddress> ReadList(string text)
    {
        var roles = ImapHeaderText.Roles(text);
        List<ImapAddress> addresses = [];
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (IsSeparator(roles[index], text[index]))
            {
                AddAt(addresses, text[start..index], text[index]);
                start = index + 1;
            }
        }

        AddMailbox(addresses, text[start..]);
        return addresses;
    }

    // A "," between mailboxes, or a group's ":" or ";", outside quotes, comments and angle brackets.
    private static bool IsSeparator(ImapHeaderCharRole role, char next) => role == ImapHeaderCharRole.Plain && next is ',' or ':' or ';';

    private static void AddAt(List<ImapAddress> addresses, string piece, char separator)
    {
        if (separator == ':')
        {
            addresses.Add(new ImapAddress(null, null, Phrase(piece) ?? string.Empty, null));
            return;
        }

        AddMailbox(addresses, piece);
        if (separator == ';')
        {
            addresses.Add(GroupEnd);
        }
    }

    private static void AddMailbox(List<ImapAddress> addresses, string piece)
    {
        var roles = ImapHeaderText.Roles(piece);
        var open = Array.IndexOf(roles, ImapHeaderCharRole.Angle);
        var name = open < 0 ? null : Phrase(piece[..open]);
        var close = piece.IndexOf('>', Math.Max(open, 0));
        var spec = open < 0 ? ImapHeaderText.RemoveComments(piece) : piece[(open + 1)..(close < 0 ? piece.Length : close)];
        if (open >= 0 || spec.Trim().Length > 0)
        {
            addresses.Add(FromSpec(name, spec.Trim()));
        }
    }

    // "@route,@route:local@domain", the route optional.
    private static ImapAddress FromSpec(string? name, string spec)
    {
        var colon = spec.IndexOf(':', StringComparison.Ordinal);
        var address = spec[(colon + 1)..];
        var at = address.LastIndexOf('@');
        return new ImapAddress(
            name,
            colon < 0 ? null : spec[..colon],
            ImapHeaderText.Unquote(at < 0 ? address : address[..at]).Trim(),
            at < 0 ? string.Empty : address[(at + 1)..].Trim());
    }

    // A display name: comments removed, unquoted, trimmed; null when nothing is left.
    private static string? Phrase(string text)
    {
        var phrase = ImapHeaderText.Unquote(ImapHeaderText.RemoveComments(text)).Trim();
        return phrase.Length == 0 ? null : phrase;
    }
}
