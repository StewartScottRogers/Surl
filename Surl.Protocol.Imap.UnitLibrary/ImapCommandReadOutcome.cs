namespace Surl.Protocol.Imap;

/// <summary>
/// How an attempt to read one IMAP command, with its literals, ended (ADR-0055, decisions 9
/// and 12).
/// </summary>
internal enum ImapCommandReadOutcome
{
    /// <summary>A whole command was read.</summary>
    CommandRead,

    /// <summary>The peer closed the connection before a whole command arrived.</summary>
    Closed,

    /// <summary>A line, or the command as a whole, is past <c>--max-line</c>.</summary>
    LineTooLong,

    /// <summary>The head timeout ran out before a line or a literal was complete.</summary>
    HeadTimedOut,

    /// <summary>A synchronizing literal would take the command past <c>--max-line</c>; the
    /// client sends none of its bytes, so the session goes on.</summary>
    LiteralTooLong,

    /// <summary>A non-synchronizing literal (<c>{n+}</c>), whose bytes are already on the way.</summary>
    NonSynchronizingLiteral,

    /// <summary>An <c>APPEND</c> reached its message's synchronizing literal, which is not read:
    /// the session checks it, then answers with the continuation or a refusal (ADR-0055,
    /// decision 8).</summary>
    AppendMessage,
}
