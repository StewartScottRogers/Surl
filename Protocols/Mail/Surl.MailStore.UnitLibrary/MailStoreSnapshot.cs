namespace Surl.MailStore;

/// <summary>
/// The whole contents of one in-memory <see cref="MailboxStore"/> at one moment, as
/// <see cref="MailboxStore.TakeSnapshot"/> took it (ADR-0124 decision 3; ADR-0131): every owner's
/// mailboxes with their names and next UIDs, and every message with its UID, flags, internal
/// date and bytes. Opaque: only the store that took it can restore it, any number of times. Not
/// to be confused with <see cref="MailboxSnapshot"/>, one mailbox as IMAP's <c>SELECT</c> reports
/// it.
/// </summary>
/// <remarks>
/// A message's bytes never change once stored, so the snapshot shares them with the store rather
/// than copying them; everything else is copied.
/// </remarks>
public sealed class MailStoreSnapshot
{
    internal MailStoreSnapshot(MailboxStore store, IReadOnlyDictionary<OwnerMailboxes, IReadOnlyList<MailboxCopy>> owners)
    {
        Store = store;
        Owners = owners;
        var messages = owners.Values.SelectMany(mailboxes => mailboxes).SelectMany(mailbox => mailbox.Messages).ToList();
        MessageCount = messages.Count;
        MailboxCount = owners.Values.Sum(mailboxes => mailboxes.Count);
        ByteCount = messages.Select(message => message.Body).Distinct().Sum(body => body.Length);
    }

    /// <summary>
    /// How many messages the snapshot holds, every owner and mailbox together.
    /// </summary>
    public int MessageCount { get; }

    /// <summary>
    /// How many mailboxes the snapshot holds, <c>INBOX</c>es included.
    /// </summary>
    public int MailboxCount { get; }

    /// <summary>
    /// The bytes of the distinct messages the snapshot holds, counted as the store counts them
    /// against <see cref="MailboxStore.MaxTotalMessageBytes"/>: a message held by several mailboxes
    /// once. What a whole instance's snapshot adds up against its bound.
    /// </summary>
    public long ByteCount { get; }

    internal MailboxStore Store { get; }

    internal IReadOnlyDictionary<OwnerMailboxes, IReadOnlyList<MailboxCopy>> Owners { get; }
}
