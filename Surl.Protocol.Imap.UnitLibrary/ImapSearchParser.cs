using System.Globalization;
using System.Text;
using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// Reads the arguments of <c>SEARCH</c> and <c>UID SEARCH</c> (RFC 3501, sections 6.4.4 and 9;
/// ADR-0055, decision 7): an optional <c>CHARSET</c>, then one or more search keys, all of which
/// a message must match.
/// </summary>
internal sealed class ImapSearchParser
{
    private static readonly Dictionary<string, Func<ImapSearchCandidate, bool>> KeysWithoutArguments = new(StringComparer.Ordinal)
    {
        ["ALL"] = _ => true,
        ["ANSWERED"] = candidate => candidate.Summary.Flags.HasFlag(MailFlags.Answered),
        ["DELETED"] = candidate => candidate.Summary.Flags.HasFlag(MailFlags.Deleted),
        ["DRAFT"] = candidate => candidate.Summary.Flags.HasFlag(MailFlags.Draft),
        ["FLAGGED"] = candidate => candidate.Summary.Flags.HasFlag(MailFlags.Flagged),
        ["SEEN"] = candidate => candidate.Summary.Flags.HasFlag(MailFlags.Seen),
        ["UNANSWERED"] = candidate => !candidate.Summary.Flags.HasFlag(MailFlags.Answered),
        ["UNDELETED"] = candidate => !candidate.Summary.Flags.HasFlag(MailFlags.Deleted),
        ["UNDRAFT"] = candidate => !candidate.Summary.Flags.HasFlag(MailFlags.Draft),
        ["UNFLAGGED"] = candidate => !candidate.Summary.Flags.HasFlag(MailFlags.Flagged),
        ["UNSEEN"] = candidate => !candidate.Summary.Flags.HasFlag(MailFlags.Seen),

        // \Recent is never set (ADR-0055, decision 5).
        ["NEW"] = _ => false,
        ["OLD"] = _ => true,
        ["RECENT"] = _ => false,
    };

    private static readonly Dictionary<string, Func<ImapSearchParser, Func<ImapSearchCandidate, bool>?>> KeysWithArguments = new(StringComparer.Ordinal)
    {
        ["BCC"] = parser => parser.ReadHeaderKey("Bcc"),
        ["CC"] = parser => parser.ReadHeaderKey("Cc"),
        ["FROM"] = parser => parser.ReadHeaderKey("From"),
        ["SUBJECT"] = parser => parser.ReadHeaderKey("Subject"),
        ["TO"] = parser => parser.ReadHeaderKey("To"),
        ["HEADER"] = parser => parser.ReadString() is { } name ? parser.ReadHeaderKey(name) : null,
        ["BODY"] = parser => parser.ReadStringKey((candidate, text) => ImapSearchCandidate.Holds(candidate.Message.Body, text)),
        ["TEXT"] = parser => parser.ReadStringKey((candidate, text) => ImapSearchCandidate.Holds(candidate.Message.Entity, text)),

        // Keywords are not kept (ADR-0055, decision 7).
        ["KEYWORD"] = parser => parser.ReadKeywordKey(matches: false),
        ["UNKEYWORD"] = parser => parser.ReadKeywordKey(matches: true),
        ["LARGER"] = parser => parser.ReadNumberKey((size, number) => size > number),
        ["SMALLER"] = parser => parser.ReadNumberKey((size, number) => size < number),
        ["BEFORE"] = parser => parser.ReadDateKey(candidate => candidate.InternalDate, (date, key) => date < key),
        ["ON"] = parser => parser.ReadDateKey(candidate => candidate.InternalDate, (date, key) => date == key),
        ["SINCE"] = parser => parser.ReadDateKey(candidate => candidate.InternalDate, (date, key) => date >= key),
        ["SENTBEFORE"] = parser => parser.ReadDateKey(candidate => candidate.SentDate, (date, key) => date < key),
        ["SENTON"] = parser => parser.ReadDateKey(candidate => candidate.SentDate, (date, key) => date == key),
        ["SENTSINCE"] = parser => parser.ReadDateKey(candidate => candidate.SentDate, (date, key) => date >= key),
        ["UID"] = parser => parser.ReadUidKey(),
        ["NOT"] = parser => parser.ReadNotKey(),
        ["OR"] = parser => parser.ReadOrKey(),
    };

    private static readonly HashSet<string> Charsets = new(["US-ASCII", "UTF-8"], StringComparer.OrdinalIgnoreCase);

    private readonly ImapArguments arguments;
    private readonly uint messageCount;
    private readonly uint highestUid;

    /// <summary>
    /// Starts reading the arguments.
    /// </summary>
    /// <param name="arguments">The command, just after <c>SEARCH</c>.</param>
    /// <param name="messageCount">How many messages the session sees, what <c>*</c> stands for
    /// in a message number set.</param>
    /// <param name="highestUid">The highest UID the session sees, what <c>*</c> stands for in a
    /// <c>UID</c> key.</param>
    public ImapSearchParser(ImapArguments arguments, uint messageCount, uint highestUid)
    {
        this.arguments = arguments;
        this.messageCount = messageCount;
        this.highestUid = highestUid;
    }

    /// <summary>
    /// Reads the arguments to the command's end.
    /// </summary>
    /// <param name="criteria">What a message must match, when the result is
    /// <see cref="ImapSearchParse.Read"/>.</param>
    /// <returns>Whether they were read, did not parse, or named a charset other than
    /// <c>US-ASCII</c> and <c>UTF-8</c>.</returns>
    public ImapSearchParse Read(out Func<ImapSearchCandidate, bool>? criteria)
    {
        criteria = null;
        var charset = arguments.TryReadSpace() ? ReadCharset() : ImapSearchParse.Invalid;
        if (charset != ImapSearchParse.Read)
        {
            return charset;
        }

        criteria = ReadKeys() is { } keys && arguments.IsAtEnd ? keys : null;
        return criteria is null ? ImapSearchParse.Invalid : ImapSearchParse.Read;
    }

    // An optional "CHARSET <astring> SP": Read when there is none, or it is one this server reads.
    private ImapSearchParse ReadCharset()
    {
        if (!arguments.TryReadAtom("CHARSET"))
        {
            return ImapSearchParse.Read;
        }

        var charset = arguments.ReadSpacedAString();
        if (charset is null || !arguments.TryReadSpace())
        {
            return ImapSearchParse.Invalid;
        }

        return Charsets.Contains(Encoding.UTF8.GetString(charset)) ? ImapSearchParse.Read : ImapSearchParse.UnsupportedCharset;
    }

    // One or more keys separated by single spaces, all of which must match.
    private Func<ImapSearchCandidate, bool>? ReadKeys()
    {
        List<Func<ImapSearchCandidate, bool>> keys = [];
        do
        {
            if (ReadKey() is not { } key)
            {
                return null;
            }

            keys.Add(key);
        }
        while (arguments.TryReadSpace());

        return candidate => keys.All(key => key(candidate));
    }

    private Func<ImapSearchCandidate, bool>? ReadKey()
    {
        if (arguments.TryReadByte((byte)'('))
        {
            return ReadGroupKey();
        }

        if (arguments.ReadRun(ImapSequenceSet.IsSequenceByte) is { } text)
        {
            return ReadSequenceKey(text);
        }

        return arguments.ReadAtom() is { } name ? ReadNamedKey(name) : null;
    }

    // "(" already read: keys, then ")".
    private Func<ImapSearchCandidate, bool>? ReadGroupKey()
    {
        var keys = ReadKeys();
        return arguments.TryReadByte((byte)')') ? keys : null;
    }

    // Message numbers; one past the messages the session sees matches nothing.
    private Func<ImapSearchCandidate, bool>? ReadSequenceKey(string text) =>
        ImapSequenceSet.Parse(text) is { } set ? candidate => set.Contains((uint)candidate.Number, messageCount) : null;

    private Func<ImapSearchCandidate, bool>? ReadNamedKey(string name) =>
        KeysWithoutArguments.TryGetValue(name, out var key) ? key
        : KeysWithArguments.TryGetValue(name, out var read) ? read(this)
        : null;

    private string? ReadString() => arguments.ReadSpacedAString() is { } text ? Encoding.UTF8.GetString(text) : null;

    private Func<ImapSearchCandidate, bool>? ReadHeaderKey(string name) => ReadStringKey((candidate, text) => candidate.HeaderHolds(name, text));

    private Func<ImapSearchCandidate, bool>? ReadStringKey(Func<ImapSearchCandidate, string, bool> matches) =>
        ReadString() is { } text ? candidate => matches(candidate, text) : null;

    private Func<ImapSearchCandidate, bool>? ReadKeywordKey(bool matches) =>
        arguments.TryReadSpace() && arguments.ReadAtom() is not null ? _ => matches : null;

    private Func<ImapSearchCandidate, bool>? ReadNumberKey(Func<long, uint, bool> compare) =>
        arguments.TryReadSpace()
            && arguments.ReadRun(value => char.IsAsciiDigit((char)value)) is { } digits
            && uint.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? candidate => compare(candidate.Summary.Size, number)
            : null;

    // A date, d-MMM-yyyy, as an atom or a quoted string.
    private Func<ImapSearchCandidate, bool>? ReadDateKey(Func<ImapSearchCandidate, DateOnly?> dateOf, Func<DateOnly, DateOnly, bool> compare) =>
        ReadString() is { } text && ImapSearchCandidate.ParseDate(text) is { } key
            ? candidate => dateOf(candidate) is { } date && compare(date, key)
            : null;

    private Func<ImapSearchCandidate, bool>? ReadUidKey() =>
        ImapSequenceSet.ReadSpaced(arguments) is { } set ? candidate => set.Contains(candidate.Summary.Uid, highestUid) : null;

    private Func<ImapSearchCandidate, bool>? ReadNotKey() =>
        arguments.TryReadSpace() && ReadKey() is { } key ? candidate => !key(candidate) : null;

    private Func<ImapSearchCandidate, bool>? ReadOrKey()
    {
        var first = arguments.TryReadSpace() ? ReadKey() : null;
        var second = first is not null && arguments.TryReadSpace() ? ReadKey() : null;
        return second is null ? null : candidate => first!(candidate) || second(candidate);
    }
}
