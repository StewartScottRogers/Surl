using Surl.Protocol.Abstractions;

namespace Surl.MailStore;

/// <summary>
/// How a test prepares and rewinds the store: seed, reset, snapshot and restore (ADR-0124
/// decision 3; ADR-0131).
/// </summary>
public sealed partial class MailboxStore
{
    /// <summary>
    /// Delivers one message to the recipient <paramref name="recipientPath"/> names, as SMTP would
    /// deliver it after <c>RCPT TO</c> and <c>DATA</c>: resolved by <see cref="LookUpRecipient"/>,
    /// stored byte for byte in the <c>INBOX</c> of each owner it names under that mailbox's next
    /// UID, with no flags and the clock's time as its internal date, and held to every bound and
    /// quota a delivery is. It never consults the fault plan, and publishes
    /// <see cref="StoreChange.Seeded"/> for each copy. A store on disk is seeded as it is written
    /// by SMTP; call <see cref="SaveChangesAsync"/> afterwards, as the servers do.
    /// </summary>
    /// <param name="recipientPath">The recipient as <c>RCPT TO:</c> would give it, with or without
    /// its angle brackets.</param>
    /// <param name="message">The message's bytes.</param>
    /// <returns>How the recipient resolved, and how the delivery went when it named owners.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recipientPath"/> is
    /// <see langword="null"/>.</exception>
    public MailSeedResult Seed(string recipientPath, ReadOnlySpan<byte> message)
    {
        var lookup = LookUpRecipient(recipientPath, out var recipient);
        return new(lookup, recipient is null ? null : SeedTo([.. recipient.Owners], message));
    }

    /// <summary>
    /// Returns the store to its configured state: every owner it was made with holds exactly the
    /// mailboxes it was made with - its <c>INBOX</c>, its configured folders, and the shared
    /// folders - each empty, with a <c>UIDVALIDITY</c> the store has never given before and next
    /// UID 1. Mailboxes made since are gone. Aliases, quotas and shared-folder members are
    /// configuration and are unchanged. Every POP3 maildrop lock is released; a session that held
    /// one still reads the messages it was given and removes none at <c>QUIT</c>. Publishes one
    /// <see cref="StoreChange.Reset"/> naming the whole store (an empty name) with byte count 0.
    /// </summary>
    /// <exception cref="InvalidOperationException">The store is on disk (made by
    /// <see cref="LoadAsync"/>), so nothing is changed; or the clock is so near 2106-02-07T06:28:15Z
    /// that the mailboxes cannot all get a new <c>UIDVALIDITY</c>, so nothing is changed.</exception>
    public void Reset()
    {
        lock (storeLock)
        {
            var configured = ThrowIfOnDisk("reset");
            ThrowIfUidValiditiesRunOut(configured.Values.Sum(names => names.Length));
            foreach (var (owner, names) in configured)
            {
                EmptyOwner(owner);
                foreach (var name in names)
                {
                    owner.Mailboxes.Add(name, new StoredMailbox(name, IssueUidValidity(), 1));
                }
            }

            FinishReplacing(StoreChange.Reset, byteCount: 0);
        }
    }

    /// <summary>
    /// Takes a snapshot of the whole store: every owner's mailboxes and every message with its
    /// UID, flags, internal date and bytes. Changes nothing and publishes nothing.
    /// </summary>
    /// <returns>The snapshot, which only this store can restore.</returns>
    /// <exception cref="InvalidOperationException">The store is on disk (made by
    /// <see cref="LoadAsync"/>).</exception>
    public MailStoreSnapshot TakeSnapshot()
    {
        lock (storeLock)
        {
            ThrowIfOnDisk("snapshotted");
            return new(this, owners.Values.ToDictionary(owner => owner, owner => (IReadOnlyList<MailboxCopy>)[.. owner.Mailboxes.Values.Select(CopyMailbox)]));
        }
    }

    /// <summary>
    /// Replaces the store's contents with <paramref name="snapshot"/>'s: every owner holds exactly
    /// the mailboxes it held then, each with the messages, UIDs, flags, internal dates and next
    /// UID it held then, and a <c>UIDVALIDITY</c> the store has never given before, so an IMAP
    /// client resynchronises. Every POP3 maildrop lock is released, as by <see cref="Reset"/>.
    /// Publishes one <see cref="StoreChange.Restored"/> naming the whole store (an empty name) with
    /// the snapshot's <see cref="MailStoreSnapshot.ByteCount"/>.
    /// </summary>
    /// <param name="snapshot">A snapshot this store took.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="snapshot"/> was taken from another store.</exception>
    /// <exception cref="InvalidOperationException">As <see cref="Reset"/> throws it.</exception>
    public void Restore(MailStoreSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Store != this)
        {
            throw new ArgumentException("The mail store snapshot was taken from another store.", nameof(snapshot));
        }

        lock (storeLock)
        {
            ThrowIfOnDisk("restored");
            ThrowIfUidValiditiesRunOut(snapshot.MailboxCount);
            foreach (var (owner, mailboxes) in snapshot.Owners)
            {
                EmptyOwner(owner);
                foreach (var copy in mailboxes)
                {
                    owner.Mailboxes.Add(copy.Name, RestoreMailbox(copy));
                }
            }

            FinishReplacing(StoreChange.Restored, snapshot.ByteCount);
        }
    }

    private MailStoreOutcome SeedTo(List<OwnerMailboxes> inboxOwners, ReadOnlySpan<byte> message)
    {
        using var pending = CreatePendingMessage();
        pending.Body.Write(message);
        pending.Close();
        var outcome = CheckPending(pending);
        return outcome == MailStoreOutcome.Succeeded ? DeliverPending(inboxOwners, pending, StoreChange.Seeded) : outcome;
    }

    /// <summary>
    /// The mailboxes each owner was made with, for a store made by the constructor.
    /// </summary>
    /// <exception cref="InvalidOperationException">The store is on disk: a test fixture must
    /// never empty a real directory (ADR-0124 decision 3).</exception>
    private Dictionary<OwnerMailboxes, string[]> ThrowIfOnDisk(string done) =>
        files is null
            ? configuredMailboxes!
            : throw new InvalidOperationException($"The mail store is on disk under {files.IndexPath}, so it cannot be {done}.");

    private void ThrowIfUidValiditiesRunOut(int mailboxes)
    {
        if (NextUidValidity() + mailboxes - 1 > uint.MaxValue)
        {
            throw new InvalidOperationException("The mail store has too few UIDVALIDITY values left to give every mailbox a new one.");
        }
    }

    /// <summary>
    /// Removes every mailbox of <paramref name="owner"/> and every message in them.
    /// </summary>
    private void EmptyOwner(OwnerMailboxes owner)
    {
        foreach (var mailbox in owner.Mailboxes.Values)
        {
            RemoveMessages(mailbox, [.. mailbox.Messages.Keys]);
        }

        owner.Mailboxes.Clear();
    }

    private StoredMailbox RestoreMailbox(MailboxCopy copy)
    {
        var mailbox = new StoredMailbox(copy.Name, IssueUidValidity(), copy.NextUid);
        foreach (var message in copy.Messages)
        {
            mailbox.Messages.Add(message.Uid, new StoredMessage(message.Uid, message.Body, message.InternalDate, message.Flags));
            Reference(message.Body);
            messageCount++;
        }

        return mailbox;
    }

    /// <summary>
    /// Adds one reference to <paramref name="body"/>, counting its bytes again when nothing
    /// referred to it.
    /// </summary>
    private void Reference(MessageBody body)
    {
        if (body.ReferenceCount++ == 0)
        {
            totalMessageBytes += body.Length;
        }
    }

    /// <summary>
    /// Counts the mailboxes again, releases every POP3 maildrop lock, counts one change and
    /// publishes it, after a reset or restore replaced the store's contents.
    /// </summary>
    private void FinishReplacing(StoreChange change, long byteCount)
    {
        mailboxCount = owners.Values.Sum(owner => owner.Mailboxes.Count - (owner.Mailboxes.ContainsKey(MailboxName.Inbox) ? 1 : 0));
        lockedMaildrops.Clear();
        changeCount++;
        events.Publish(new StoreChangedEvent(StoreKind.Mail, change, string.Empty, byteCount, ExchangeId: null));
    }

    private static MailboxCopy CopyMailbox(StoredMailbox mailbox) =>
        new(mailbox.Name, mailbox.NextUid, [.. mailbox.Messages.Values.Select(message => new MessageCopy(message.Uid, message.Body, message.InternalDate, message.Flags))]);
}
