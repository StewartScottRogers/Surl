using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// The arguments of <c>STORE</c> and <c>UID STORE</c> (RFC 3501, sections 6.4.6 and 9; ADR-0055,
/// decision 7): a sequence set, <c>FLAGS</c>, <c>+FLAGS</c> or <c>-FLAGS</c> with an optional
/// <c>.SILENT</c>, then the flags.
/// </summary>
/// <param name="Set">The messages to change.</param>
/// <param name="Change">Whether the flags replace, are added to or are removed from each message's.</param>
/// <param name="IsSilent">Whether <c>.SILENT</c> asked for no untagged <c>FETCH</c>.</param>
/// <param name="Flags">The system flags given.</param>
internal sealed record ImapStoreRequest(ImapSequenceSet Set, MailFlagChange Change, bool IsSilent, MailFlags Flags)
{
    private static readonly Dictionary<string, (MailFlagChange Change, bool IsSilent)> Items = new(StringComparer.Ordinal)
    {
        ["FLAGS"] = (MailFlagChange.Replace, false),
        ["FLAGS.SILENT"] = (MailFlagChange.Replace, true),
        ["+FLAGS"] = (MailFlagChange.Add, false),
        ["+FLAGS.SILENT"] = (MailFlagChange.Add, true),
        ["-FLAGS"] = (MailFlagChange.Remove, false),
        ["-FLAGS.SILENT"] = (MailFlagChange.Remove, true),
    };

    /// <summary>
    /// Reads the arguments, from the space after the command's name to the command's end.
    /// </summary>
    /// <param name="arguments">The command, just after <c>STORE</c>.</param>
    /// <returns>The request, or <see langword="null"/> when the arguments do not parse.</returns>
    public static ImapStoreRequest? Read(ImapArguments arguments)
    {
        var set = ImapSequenceSet.ReadSpaced(arguments);
        var item = set is null ? null : ReadItem(arguments);
        var flags = item is null ? null : ImapFlagList.ReadStoreFlags(arguments);
        return flags is not null && arguments.IsAtEnd ? new ImapStoreRequest(set!, item!.Value.Change, item.Value.IsSilent, flags.Value) : null;
    }

    // A space, the item's name, and the space after it.
    private static (MailFlagChange Change, bool IsSilent)? ReadItem(ImapArguments arguments) =>
        arguments.TryReadSpace() && arguments.ReadAtom() is { } name && Items.TryGetValue(name, out var item) && arguments.TryReadSpace() ? item : null;
}
