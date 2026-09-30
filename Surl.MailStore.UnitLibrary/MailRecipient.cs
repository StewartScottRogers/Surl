namespace Surl.MailStore;

/// <summary>
/// The owner an SMTP recipient maps to, as <see cref="MailboxStore.LookUpRecipient"/> found it;
/// what <see cref="MailboxStore.Deliver(IReadOnlyList{MailRecipient}, PendingMessage)"/> delivers to. Only the store that found it may
/// deliver to it.
/// </summary>
public sealed class MailRecipient
{
    internal MailRecipient(OwnerMailboxes owner)
    {
        Owner = owner;
    }

    /// <summary>
    /// The owner's name: the account's name, or the empty string for the anonymous owner.
    /// </summary>
    public string OwnerName => Owner.Name;

    internal OwnerMailboxes Owner { get; }
}
