namespace Surl.MailStore;

/// <summary>
/// The owners an SMTP recipient maps to, as <see cref="MailboxStore.LookUpRecipient"/> found
/// them; what <see cref="MailboxStore.Deliver(IReadOnlyList{MailRecipient}, PendingMessage)"/>
/// delivers to. A recipient resolved by ADR-0050 decision 5 has one owner; an alias may have
/// several, each once (ADR-0102, decision 1.3). Only the store that found it may deliver to it.
/// </summary>
public sealed class MailRecipient
{
    internal MailRecipient(IReadOnlyList<OwnerMailboxes> owners)
    {
        Owners = owners;
        OwnerNames = [.. owners.Select(owner => owner.Name)];
    }

    /// <summary>
    /// The first of <see cref="OwnerNames"/>: the one owner's name for a recipient that is not
    /// an alias to several - an account's name, a mailbox's address, or the empty string for
    /// the anonymous owner.
    /// </summary>
    public string OwnerName => OwnerNames[0];

    /// <summary>
    /// Every owner's name, each once, in the order resolution found them.
    /// </summary>
    public IReadOnlyList<string> OwnerNames { get; }

    internal IReadOnlyList<OwnerMailboxes> Owners { get; }
}
