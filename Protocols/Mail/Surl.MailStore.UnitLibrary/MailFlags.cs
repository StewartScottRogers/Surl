namespace Surl.MailStore;

/// <summary>
/// The five permanent system flags a stored message carries (ADR-0050, decision 3). Keywords
/// and <c>\Recent</c> are not kept.
/// </summary>
[Flags]
public enum MailFlags : byte
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary><c>\Seen</c>: the message has been read.</summary>
    Seen = 1,

    /// <summary><c>\Answered</c>: the message has been answered.</summary>
    Answered = 2,

    /// <summary><c>\Flagged</c>: the message is marked for attention.</summary>
    Flagged = 4,

    /// <summary><c>\Deleted</c>: the message is removed by the next expunge.</summary>
    Deleted = 8,

    /// <summary><c>\Draft</c>: the message is a draft.</summary>
    Draft = 16,
}
