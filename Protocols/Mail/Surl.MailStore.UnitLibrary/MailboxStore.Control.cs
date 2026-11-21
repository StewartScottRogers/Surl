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
            ThrowIfUidValiditiesRunOut(configured.Values.Sum(folders => folders.Length));
            DropOwnersNotIn(configured.Keys);
            foreach (var (owner, folders) in configured)
            {
                EmptyOwner(owner);
                foreach (var (name, use) in folders)
                {
                    owner.Mailboxes.Add(name, new StoredMailbox(name, IssueUidValidity(), 1) { Use = use });
                }
            }

            ReachAccounts(configuredAccountNames);

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
            return new(
                this,
                owners.Values.ToDictionary(owner => owner, owner => (IReadOnlyList<MailboxCopy>)[.. owner.Mailboxes.Values.Select(CopyMailbox)]),
                accounts.Keys);
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
            DropOwnersNotIn(snapshot.Owners.Keys);
            foreach (var (owner, mailboxes) in snapshot.Owners)
            {
                owners[owner.Name] = owner;
                EmptyOwner(owner);
                foreach (var copy in mailboxes)
                {
                    owner.Mailboxes.Add(copy.Name, RestoreMailbox(copy));
                }
            }

            ReachAccounts(snapshot.ReachedAccountNames);

            FinishReplacing(StoreChange.Restored, snapshot.ByteCount);
        }
    }

    /// <summary>
    /// Reaches the owner of an account named <paramref name="accountName"/> that the server gained
    /// while it ran (ADR-0185 decisions 1, 2, 5 and 6). A name never seen gets an owner with an empty
    /// <c>INBOX</c> (a new <c>UIDVALIDITY</c>, next UID 1) that does not count against
    /// <see cref="MaxMailboxes"/>, and one <see cref="StoreChange.Created"/> is published for it. A
    /// retired owner is reached again with its mail as it was, publishing nothing. A store on disk
    /// counts the new owner as a change, which the next <see cref="SaveChangesAsync"/> writes.
    /// </summary>
    /// <param name="accountName">The account's name.</param>
    /// <returns><see langword="true"/> when an owner was made or reached again; <see langword="false"/>,
    /// changing and publishing nothing, for an empty name, a name that is already reached, a name that
    /// is a mail-domain mailbox address, and any call on a store opened with
    /// <c>--allow-anonymous</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="accountName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="accountName"/> is
    /// <see cref="SharedFolderOwnerName"/>.</exception>
    /// <exception cref="InvalidOperationException">The clock is past 2106-02-07T06:28:15Z, so the
    /// <c>INBOX</c> can get no <c>UIDVALIDITY</c>; nothing is changed.</exception>
    public bool AddOwner(string accountName)
    {
        ArgumentNullException.ThrowIfNull(accountName);
        if (accountName == SharedFolderOwnerName)
        {
            throw new ArgumentException($"No account may be named {SharedFolderOwnerName}, the shared folders' owner.", nameof(accountName));
        }

        lock (storeLock)
        {
            if (anonymousOwner is not null || accountName.Length == 0 || accounts.FindExact(accountName) is not null || mailboxOwners.FindExact(accountName) is not null)
            {
                return false;
            }

            if (owners.TryGetValue(accountName, out var retired))
            {
                accounts.Add(accountName, retired);
                return true;
            }

            var owner = new OwnerMailboxes(accountName);
            var inbox = new StoredMailbox(MailboxName.Inbox, IssueUidValidity(), 1);
            owner.Mailboxes.Add(MailboxName.Inbox, inbox);
            owners.Add(accountName, owner);
            accounts.Add(accountName, owner);
            changeCount++;
            PublishMailbox(StoreChange.Created, owner, inbox, exchange: null);
            return true;
        }
    }

    /// <summary>
    /// Retires the owner of the account named <paramref name="accountName"/> (ADR-0185 decision 2):
    /// its mail stays in the store, unreached, so <see cref="ViewFor"/> gives it an empty view,
    /// <see cref="LookUpRecipient"/> answers <see cref="MailRecipientLookup.NoSuchAccount"/> and
    /// <see cref="Seed"/> stores nothing, while the mail still counts against the bounds.
    /// <see cref="AddOwner"/> with the same name reaches it again. Publishes nothing: no mail changes.
    /// </summary>
    /// <param name="accountName">The account's name.</param>
    /// <returns><see langword="true"/> when a reached owner was retired; <see langword="false"/> for a
    /// name that is no reached owner and for a store opened with <c>--allow-anonymous</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="accountName"/> is <see langword="null"/>.</exception>
    public bool RetireOwner(string accountName)
    {
        ArgumentNullException.ThrowIfNull(accountName);
        lock (storeLock)
        {
            if (accounts.FindExact(accountName) is null)
            {
                return false;
            }

            accounts.Remove(accountName);
            return true;
        }
    }

    /// <summary>
    /// Empties and forgets every owner not in <paramref name="kept"/>, which a reset or restore drops.
    /// </summary>
    private void DropOwnersNotIn(IEnumerable<OwnerMailboxes> kept)
    {
        var keptSet = kept.ToHashSet();
        foreach (var dropped in owners.Values.Where(owner => !keptSet.Contains(owner)).ToList())
        {
            EmptyOwner(dropped);
            owners.Remove(dropped.Name);
        }
    }

    /// <summary>
    /// Makes the owners of exactly <paramref name="accountNames"/> the reached ones.
    /// </summary>
    private void ReachAccounts(IEnumerable<string> accountNames)
    {
        accounts.Clear();
        foreach (var name in accountNames)
        {
            accounts.Add(name, owners[name]);
        }
    }

    private MailStoreOutcome SeedTo(List<OwnerMailboxes> inboxOwners, ReadOnlySpan<byte> message)
    {
        using var pending = CreatePendingMessage();
        pending.Body.Write(message);
        pending.Close();
        var outcome = CheckPending(pending);
        return outcome == MailStoreOutcome.Succeeded ? DeliverPending(inboxOwners.ConvertAll(owner => new OwnerDelivery(owner, null)), pending, StoreChange.Seeded, exchange: null) : outcome;
    }

    /// <summary>
    /// The mailboxes each owner was made with, for a store made by the constructor.
    /// </summary>
    /// <exception cref="InvalidOperationException">The store is on disk: a test fixture must
    /// never empty a real directory (ADR-0124 decision 3).</exception>
    private Dictionary<OwnerMailboxes, (string Name, string? Use)[]> ThrowIfOnDisk(string done) =>
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
        var mailbox = new StoredMailbox(copy.Name, IssueUidValidity(), copy.NextUid) { Use = copy.Use };
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
        new(mailbox.Name, mailbox.NextUid, [.. mailbox.Messages.Values.Select(message => new MessageCopy(message.Uid, message.Body, message.InternalDate, message.Flags))], mailbox.Use);
}
