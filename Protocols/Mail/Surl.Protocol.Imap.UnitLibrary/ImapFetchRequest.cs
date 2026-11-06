using System.Globalization;

namespace Surl.Protocol.Imap;

/// <summary>
/// The arguments of <c>FETCH</c> and <c>UID FETCH</c> (RFC 3501, sections 6.4.5 and 9; ADR-0055,
/// decision 4): a sequence set, then a macro, one data item, or a parenthesised list of them.
/// </summary>
/// <param name="Set">The messages asked for.</param>
/// <param name="Items">The data items asked for, macros expanded, in the order asked.</param>
internal sealed record ImapFetchRequest(ImapSequenceSet Set, IReadOnlyList<ImapFetchItem> Items)
{
    private static readonly Dictionary<string, ImapFetchItemKind> SimpleItems = new(StringComparer.Ordinal)
    {
        ["UID"] = ImapFetchItemKind.Uid,
        ["FLAGS"] = ImapFetchItemKind.Flags,
        ["INTERNALDATE"] = ImapFetchItemKind.InternalDate,
        ["RFC822.SIZE"] = ImapFetchItemKind.Rfc822Size,
        ["ENVELOPE"] = ImapFetchItemKind.Envelope,
        ["BODY"] = ImapFetchItemKind.Body,
        ["BODYSTRUCTURE"] = ImapFetchItemKind.BodyStructure,
        ["RFC822"] = ImapFetchItemKind.Rfc822,
        ["RFC822.HEADER"] = ImapFetchItemKind.Rfc822Header,
        ["RFC822.TEXT"] = ImapFetchItemKind.Rfc822Text,
    };

    private static readonly Dictionary<string, ImapFetchItemKind[]> Macros = new(StringComparer.Ordinal)
    {
        ["ALL"] = [ImapFetchItemKind.Flags, ImapFetchItemKind.InternalDate, ImapFetchItemKind.Rfc822Size, ImapFetchItemKind.Envelope],
        ["FAST"] = [ImapFetchItemKind.Flags, ImapFetchItemKind.InternalDate, ImapFetchItemKind.Rfc822Size],
        ["FULL"] = [ImapFetchItemKind.Flags, ImapFetchItemKind.InternalDate, ImapFetchItemKind.Rfc822Size, ImapFetchItemKind.Envelope, ImapFetchItemKind.Body],
    };

    /// <summary>
    /// Reads the arguments, from the space after the command's name to the command's end.
    /// </summary>
    /// <param name="arguments">The command, just after <c>FETCH</c>.</param>
    /// <returns>The request, or <see langword="null"/> when the arguments do not parse.</returns>
    public static ImapFetchRequest? Read(ImapArguments arguments)
    {
        var set = ImapSequenceSet.ReadSpaced(arguments);
        var items = set is not null && arguments.TryReadSpace() ? ReadItems(arguments) : null;
        return items is not null && arguments.IsAtEnd ? new ImapFetchRequest(set!, items) : null;
    }

    private static IReadOnlyList<ImapFetchItem>? ReadItems(ImapArguments arguments)
    {
        if (arguments.TryReadByte((byte)'('))
        {
            return ReadItemList(arguments);
        }

        var name = ReadName(arguments);
        return Macros.TryGetValue(name, out var macro)
            ? macro.Select(kind => new ImapFetchItem(kind)).ToList()
            : ReadItem(arguments, name) is { } item ? [item] : null;
    }

    private static List<ImapFetchItem>? ReadItemList(ImapArguments arguments)
    {
        List<ImapFetchItem> items = [];
        do
        {
            if (ReadItem(arguments, ReadName(arguments)) is not { } item)
            {
                return null;
            }

            items.Add(item);
        }
        while (arguments.TryReadSpace());

        return arguments.TryReadByte((byte)')') ? items : null;
    }

    // The item's name in capitals; empty when none is next.
    private static string ReadName(ImapArguments arguments) => arguments.ReadRun(IsNameByte)?.ToUpperInvariant() ?? string.Empty;

    private static bool IsNameByte(byte value) => char.IsAsciiLetterOrDigit((char)value) || value == '.';

    private static ImapFetchItem? ReadItem(ImapArguments arguments, string name)
    {
        if (IsSectionName(name) && arguments.TryReadByte((byte)'['))
        {
            return ReadBodySection(arguments, name == "BODY.PEEK");
        }

        return SimpleItems.TryGetValue(name, out var kind) ? new ImapFetchItem(kind) : null;
    }

    private static bool IsSectionName(string name) => name is "BODY" or "BODY.PEEK";

    // The section, then an optional "<" number "." nz-number ">".
    private static ImapFetchItem? ReadBodySection(ImapArguments arguments, bool isPeek)
    {
        if (ImapSection.Read(arguments) is not { } section)
        {
            return null;
        }

        if (!arguments.TryReadByte((byte)'<'))
        {
            return new ImapFetchItem(ImapFetchItemKind.BodySection, section, isPeek);
        }

        return ReadPartial(arguments) is { } partial
            ? new ImapFetchItem(ImapFetchItemKind.BodySection, section, isPeek, partial.Origin, partial.Length)
            : null;
    }

    private static (long Origin, long Length)? ReadPartial(ImapArguments arguments) =>
        ReadPartialOrigin(arguments) is { } origin && ReadPartialLength(arguments) is { } length ? (origin, length) : null;

    private static long? ReadPartialOrigin(ImapArguments arguments) =>
        ReadNumber(arguments) is { } origin && arguments.TryReadByte((byte)'.') ? origin : null;

    private static long? ReadPartialLength(ImapArguments arguments) =>
        ReadNumber(arguments) is > 0 and var length && arguments.TryReadByte((byte)'>') ? length : null;

    // A number: digits, at most 32 bits (RFC 3501, section 9).
    private static long? ReadNumber(ImapArguments arguments) =>
        arguments.ReadRun(value => char.IsAsciiDigit((char)value)) is { } digits
            && uint.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number : null;
}
