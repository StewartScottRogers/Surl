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

    /// <summary>
    /// The part of this recipient that names owners not in <paramref name="reachedOwnerNames"/>:
    /// what one copy per owner still has to reach (ADR-0102, decision 1.3).
    /// </summary>
    /// <param name="reachedOwnerNames">Owner names that already get a copy.</param>
    /// <returns>
    /// This recipient when none of its owners was reached, a recipient of the others when some
    /// were, and <see langword="null"/> when all were.
    /// </returns>
    public MailRecipient? WithoutOwners(IEnumerable<string> reachedOwnerNames)
    {
        ArgumentNullException.ThrowIfNull(reachedOwnerNames);
        var reached = new HashSet<string>(reachedOwnerNames, StringComparer.Ordinal);
        var remaining = Owners.Where(owner => !reached.Contains(owner.Name)).ToList();
        if (remaining.Count == Owners.Count)
        {
            return this;
        }

        return remaining.Count == 0 ? null : new MailRecipient(remaining);
    }

    internal IReadOnlyList<OwnerMailboxes> Owners { get; }
}
