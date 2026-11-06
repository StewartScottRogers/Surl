namespace Surl.MailStore;

/// <summary>
/// One owner's mailboxes, keyed by name as ADR-0050 decision 3 matches them: <c>INBOX</c> in
/// any case, every other name ordinally.
/// </summary>
internal sealed class OwnerMailboxes(string name)
{
    public string Name { get; } = name;

    public Dictionary<string, StoredMailbox> Mailboxes { get; } = new(StringComparer.Ordinal);

    public StoredMailbox Inbox => Mailboxes[MailboxName.Inbox];

    public StoredMailbox? Find(string name) => Mailboxes.GetValueOrDefault(MailboxName.Canonical(name));
}
