using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// Reads the flags of <c>STORE</c> and <c>APPEND</c> (RFC 3501, section 9; ADR-0055, decision 5):
/// the five system flags in any case are kept, a keyword or another <c>\</c> flag is read and
/// ignored, since the store keeps no keywords and <c>PERMANENTFLAGS</c> never listed one, and
/// <c>\Recent</c> or <c>\*</c> makes the flags invalid (the grammar's <c>flag</c> excludes both).
/// </summary>
internal static class ImapFlagList
{
    private static readonly Dictionary<string, MailFlags> SystemFlags = new(StringComparer.Ordinal)
    {
        ["ANSWERED"] = MailFlags.Answered,
        ["FLAGGED"] = MailFlags.Flagged,
        ["DELETED"] = MailFlags.Deleted,
        ["SEEN"] = MailFlags.Seen,
        ["DRAFT"] = MailFlags.Draft,
    };

    /// <summary>
    /// Reads a <c>flag-list</c>: <c>(</c>, flags separated by single spaces, <c>)</c>; it may be empty.
    /// </summary>
    /// <param name="arguments">The command, just before the <c>(</c>.</param>
    /// <returns>The system flags named, or <see langword="null"/> when no valid list is next.</returns>
    public static MailFlags? ReadList(ImapArguments arguments)
    {
        if (!arguments.TryReadByte((byte)'('))
        {
            return null;
        }

        return arguments.TryReadByte((byte)')') ? MailFlags.None : ReadFlags(arguments) is { } flags && arguments.TryReadByte((byte)')') ? flags : null;
    }

    /// <summary>
    /// Reads <c>STORE</c>'s flags: a <c>flag-list</c>, or one or more flags separated by single
    /// spaces with no parentheses.
    /// </summary>
    /// <param name="arguments">The command, just before the flags.</param>
    /// <returns>The system flags named, or <see langword="null"/> when no valid flags are next.</returns>
    public static MailFlags? ReadStoreFlags(ImapArguments arguments) => arguments.IsAt((byte)'(') ? ReadList(arguments) : ReadFlags(arguments);

    // flag *(SP flag)
    private static MailFlags? ReadFlags(ImapArguments arguments)
    {
        var flags = MailFlags.None;
        do
        {
            if (ReadFlag(arguments) is not { } flag)
            {
                return null;
            }

            flags |= flag;
        }
        while (arguments.TryReadSpace());

        return flags;
    }

    // "\" atom, or a keyword atom; None for a flag not kept.
    private static MailFlags? ReadFlag(ImapArguments arguments)
    {
        var isSystem = arguments.TryReadByte((byte)'\\');
        return arguments.ReadAtom() is not { } name ? null
            : !isSystem ? MailFlags.None
            : name == "RECENT" ? null
            : SystemFlags.GetValueOrDefault(name);
    }
}
