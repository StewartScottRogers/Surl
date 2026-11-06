namespace Surl.Protocol.Abstractions;

/// <summary>
/// Which shared store a <see cref="StoreChangedEvent"/> names (ADR-0100).
/// </summary>
public enum StoreKind
{
    /// <summary>
    /// The content store: files and objects.
    /// </summary>
    Content,

    /// <summary>
    /// The mail store: mailboxes and messages.
    /// </summary>
    Mail,

    /// <summary>
    /// The message bus: topics, queues and their messages.
    /// </summary>
    Bus,

    /// <summary>
    /// The account book.
    /// </summary>
    Accounts,
}
