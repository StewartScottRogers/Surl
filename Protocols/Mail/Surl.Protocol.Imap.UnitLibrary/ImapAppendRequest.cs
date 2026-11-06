using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// The arguments of <c>APPEND</c> before its message (RFC 3501, sections 6.3.11 and 9; ADR-0055,
/// decision 8): a mailbox, an optional flag list and an optional date-time, each after a space,
/// then the space before the message's literal.
/// </summary>
/// <param name="Mailbox">The mailbox's name as the client sent it.</param>
/// <param name="Flags">The system flags given; none when no flag list was.</param>
/// <param name="InternalDate">The date-time given, or <see langword="null"/> for the clock's.</param>
internal sealed record ImapAppendRequest(byte[] Mailbox, MailFlags Flags, DateTimeOffset? InternalDate)
{
    /// <summary>
    /// Whether the literal the command has reached is <c>APPEND</c>'s message: the command so far
    /// is a tag, <c>APPEND</c> and a mailbox. A literal before the mailbox is the mailbox itself,
    /// read like any other.
    /// </summary>
    /// <param name="text">The command up to the literal's <c>{</c>.</param>
    /// <returns>Whether the literal is the message.</returns>
    public static bool IsMessageNext(ImapCommandText text)
    {
        var arguments = new ImapArguments(text);
        return arguments.ReadTag() is not null && arguments.TryReadSpace() && arguments.TryReadAtom("APPEND") && arguments.ReadSpacedAString() is not null;
    }

    /// <summary>
    /// Reads the arguments, from the space after <c>APPEND</c> to the message's literal.
    /// </summary>
    /// <param name="arguments">The command up to the literal's <c>{</c>, just after <c>APPEND</c>.</param>
    /// <returns>The request, or <see langword="null"/> when the arguments do not parse.</returns>
    public static ImapAppendRequest? Read(ImapArguments arguments)
    {
        var mailbox = arguments.ReadSpacedAString();
        return mailbox is not null && arguments.TryReadSpace() && TryReadFlags(arguments, out var flags) && TryReadDate(arguments, out var date) && arguments.IsAtEnd
            ? new ImapAppendRequest(mailbox, flags, date)
            : null;
    }

    // An optional flag list and the space after it.
    private static bool TryReadFlags(ImapArguments arguments, out MailFlags flags)
    {
        flags = MailFlags.None;
        if (!arguments.IsAt((byte)'('))
        {
            return true;
        }

        var list = ImapFlagList.ReadList(arguments);
        flags = list.GetValueOrDefault();
        return list is not null && arguments.TryReadSpace();
    }

    // An optional date-time, a quoted string, and the space after it.
    private static bool TryReadDate(ImapArguments arguments, out DateTimeOffset? date)
    {
        date = null;
        if (!arguments.IsAt((byte)'"'))
        {
            return true;
        }

        date = arguments.ReadAString() is { } text ? ImapDateTime.Parse(text) : null;
        return date is not null && arguments.TryReadSpace();
    }
}
