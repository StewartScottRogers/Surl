namespace Surl.MailStore;

/// <summary>
/// The mailboxes one IMAP or POP3 session acts on, as <see cref="MailboxStore.ViewFor"/> gave
/// them for its login (ADR-0050, decision 2). An empty view has no mailboxes: nothing to read,
/// and every write is refused as a missing mailbox. Only the store that gave it may use it.
/// </summary>
public sealed class MailView
{
    internal MailView(OwnerMailboxes? owner)
    {
        Owner = owner;
    }

    /// <summary>
    /// The name of the owner the view acts as - an account's name, or the empty string for the
    /// anonymous owner - or <see langword="null"/> for an empty view.
    /// </summary>
    public string? OwnerName => Owner?.Name;

    internal OwnerMailboxes? Owner { get; }
}
