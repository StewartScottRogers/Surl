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
        return new(lookup, recipient is null ? null : SeedTo([.. recipient.Owners], message, [WithoutAngleBrackets(recipientPath)]));
    }

    private static string WithoutAngleBrackets(string path) =>
        path.Length > 1 && path.StartsWith('<') && path.EndsWith('>') ? path[1..^1] : path;

    /// <summary>
    /// Returns the store to its configured state: every owner it was made with holds exactly the
    /// mailboxes it was made with - its <c>INBOX</c>, its configured folders, and the shared
    /// folders - each empty, with a <c>UIDVALIDITY</c> the store has never given before and next
    /// UID 1. Mailboxes made since are gone. Owners <see cref="AddOwner"/> made since are dropped
    /// with their mail, and exactly the accounts the store was made with are reached, one retired
    /// since included. Aliases, quotas and shared-folder members are
    /// configuration and are unchanged. Every POP3 maildrop lock is released; a session that held
    /// one still reads the messages it was given and removes none at <c>QUIT</c>. Publishes one
    /// <see cref="StoreChange.Reset"/> naming the whole store (an empty name) with byte count 0.
    /// <para>
    /// A <see cref="MailView"/>, <see cref="MailRecipient"/> or <see cref="MaildropLock"/> taken of
    /// a dropped owner stays usable and never writes to it (ADR-0193): the view has no mailboxes of
    /// its own, so every operation on one is <see cref="MailStoreOutcome.MailboxMissing"/> and
    /// <see cref="LockMaildrop"/> gives an empty maildrop; a delivery to the recipient gives it no
    /// copy; and the lock removes nothing. An owner made later under the same name is another
    /// owner, which they do not reach. A <see cref="Restore"/> that brings the dropped owner back
    /// makes them reach it again.
    /// </para>
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
    /// UID, flags, internal date and bytes, and which accounts are reached. Changes nothing and
    /// publishes nothing.
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
    /// client resynchronises. An owner the snapshot does not hold is dropped with its mail, and
    /// exactly the accounts reached then are reached. Every POP3 maildrop lock is released, as by
    /// <see cref="Reset"/>, and views, recipients and maildrop locks of a dropped owner behave as
    /// <see cref="Reset"/> describes.
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
    /// while it ran (ADR-0185 decisions 1, 2, 5 and 6). A name the store holds no owner for gets one
    /// with an empty <c>INBOX</c> (a new <c>UIDVALIDITY</c>, next UID 1) that does not count against
    /// <see cref="MaxMailboxes"/>; one <see cref="StoreChange.Created"/> is published for it and
    /// <see cref="ChangeCount"/> rises by one, which a store on disk writes at the next
    /// <see cref="SaveChangesAsync"/>. An owner held but unreached - retired by
    /// <see cref="RetireOwner"/>, or held by the files <see cref="LoadAsync"/> read for a name that
    /// was no account then - is reached again with its mail as it was, publishing nothing and not
    /// raising <see cref="ChangeCount"/>.
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
    /// the name is no longer an account's, and the owner's mail stays in the store, still counted
    /// against the bounds. <see cref="AddOwner"/> with the same name reaches it again. Publishes
    /// nothing and does not raise <see cref="ChangeCount"/>: no mail changes.
    /// <para>
    /// <see cref="ViewFor"/> then gives the name no mailboxes of its own, but still every shared
    /// folder whose members include it, so the view is empty only when there is none.
    /// </para>
    /// <para>
    /// <see cref="LookUpRecipient"/> then answers a path naming it as for any name that is no
    /// account, which is not always <see cref="MailRecipientLookup.NoSuchAccount"/>. With no mail
    /// domain configured, the one remaining account whose name equals the local part ignoring case,
    /// if exactly one does, receives it. With a mail domain, a path outside every configured domain
    /// is <see cref="MailRecipientLookup.RelayingDenied"/> as before, and one inside can be
    /// delivered through an alias or mailbox of that address, such an account, or the domain's
    /// catch-all. <see cref="Seed"/> follows the same lookup, so it stores a message whenever that
    /// answers <see cref="MailRecipientLookup.Deliverable"/> and the delivery is not refused.
    /// </para>
    /// <para>
    /// A <see cref="MailView"/> or <see cref="MailRecipient"/> the store gave before the call still
    /// reaches the retired owner: a session already holding such a view goes on acting on its
    /// mailboxes, and a recipient already looked up is still delivered to.
    /// </para>
    /// </summary>
    /// <param name="accountName">The account's name, matched ordinally.</param>
    /// <returns><see langword="true"/> when the account's owner was retired; <see langword="false"/>,
    /// changing nothing, for a name that is no reached account's - a mail-domain mailbox address
    /// included, which stays reached - and for a store opened with <c>--allow-anonymous</c>.</returns>
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

    private MailStoreOutcome SeedTo(List<OwnerMailboxes> inboxOwners, ReadOnlySpan<byte> message, IReadOnlyList<string> deliveredFor)
    {
        using var pending = CreatePendingMessage();
        pending.Body.Write(message);
        pending.Close();
        var outcome = CheckPending(pending);
        return outcome == MailStoreOutcome.Succeeded ? DeliverPending(inboxOwners.ConvertAll(owner => new OwnerDelivery(owner, deliveredFor)), pending, StoreChange.Seeded, exchange: null) : outcome;
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
