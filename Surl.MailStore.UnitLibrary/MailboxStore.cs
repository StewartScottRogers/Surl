namespace Surl.MailStore;

/// <summary>
/// The mailboxes and messages the SMTP, IMAP and POP3 servers share (ADR-0050, decisions 2 to
/// 6): SMTP delivers into it, IMAP and POP3 serve from it. One store is given to all three
/// servers. Safe for concurrent use: every operation is atomic with respect to every other,
/// and a session sees another's changes at once, except a POP3 session's
/// <see cref="MaildropLock"/>, whose view is fixed when it is taken.
/// </summary>
/// <remarks>
/// <para>
/// It keeps a mailbox set per owner. Without <c>--allow-anonymous</c> every account with a
/// non-empty name is an owner, and each has an <c>INBOX</c> from the start. With it there is
/// one owner reached, the anonymous owner, whose name is the empty string: every recipient is
/// delivered to its <c>INBOX</c>, and every session acts as it.
/// </para>
/// <para>
/// It is bounded, because every peer can fill it: at most <see cref="MaxMessages"/> messages,
/// <see cref="MaxTotalMessageBytes"/> bytes of distinct messages (a message delivered to
/// several recipients, or copied, is counted once) and <see cref="MaxMailboxes"/> mailboxes
/// other than the owners' <c>INBOX</c>es. An operation that would pass a bound stores nothing.
/// </para>
/// <para>
/// Its mailboxes and flags live in memory. <see cref="ChangeCount"/> rises by one with every
/// change that alters the store, and not with a refused or no-op one. A store made by
/// <see cref="LoadAsync"/> starts with what its <see cref="MailStoreFiles"/> hold, streams each
/// new message body into a pending file that becomes its message file when it is stored, reads
/// a message's bytes from that file when a server fetches them, and writes the index back when
/// <see cref="SaveChangesAsync"/> is called, so its mail survives a restart (ADR-0050,
/// decision 7). A store made by the constructor has no files: it holds every message's bytes in
/// memory, and its changes are never written anywhere.
/// </para>
/// </remarks>
public sealed class MailboxStore
{
    /// <summary>
    /// The default for <see cref="MaxMessages"/>: 100000 messages.
    /// </summary>
    public const int DefaultMaxMessages = 100_000;

    /// <summary>
    /// The default for <see cref="MaxTotalMessageBytes"/>: 268435456 bytes (256 MiB), the same
    /// as the in-memory content file system's bound.
    /// </summary>
    public const long DefaultMaxTotalMessageBytes = 268_435_456;

    /// <summary>
    /// The default for <see cref="MaxMailboxes"/>: 10000 mailboxes.
    /// </summary>
    public const int DefaultMaxMailboxes = 10_000;

    /// <summary>
    /// The most UTF-8 bytes one mailbox name may hold.
    /// </summary>
    public const int MaxMailboxNameBytes = 1024;

    private const MailFlags SystemFlags =
        MailFlags.Seen | MailFlags.Answered | MailFlags.Flagged | MailFlags.Deleted | MailFlags.Draft;

    private readonly Lock storeLock = new();
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private readonly TimeProvider timeProvider;
    private readonly Dictionary<string, OwnerMailboxes> owners = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OwnerMailboxes> accounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OwnerMailboxes?> accountsIgnoringCase = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<OwnerMailboxes> lockedMaildrops = [];
    private readonly HashSet<MessageBody> unwrittenBodies = [];
    private readonly List<ulong> releasedFileNumbers = [];
    private OwnerMailboxes? anonymousOwner;
    private MailStoreFiles? files;
    private ulong nextFileNumber;
    private uint lastUidValidity;
    private int messageCount;
    private long totalMessageBytes;
    private int mailboxCount;
    private long changeCount;
    private long savedChangeCount;

    /// <summary>
    /// Creates an empty store whose owners are <paramref name="accountNames"/>, or the anonymous
    /// owner alone when <paramref name="allowAnonymous"/> is set, each with an empty
    /// <c>INBOX</c>.
    /// </summary>
    /// <param name="accountNames">The configured accounts' names. An empty name - a Bearer
    /// token's account - is not an owner, and a repeated name is one owner.</param>
    /// <param name="allowAnonymous">Whether <c>--allow-anonymous</c> is set.</param>
    /// <param name="timeProvider">The clock for internal dates and <c>UIDVALIDITY</c>.</param>
    /// <param name="maxMessageBytes">The value of <see cref="MaxMessageBytes"/>; 0 means no limit.</param>
    /// <param name="maxMessages">The value of <see cref="MaxMessages"/>; at least 1.</param>
    /// <param name="maxTotalMessageBytes">The value of <see cref="MaxTotalMessageBytes"/>; at least 1.</param>
    /// <param name="maxMailboxes">The value of <see cref="MaxMailboxes"/>; at least 0.</param>
    /// <exception cref="ArgumentNullException"><paramref name="accountNames"/> or
    /// <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound is out of range.</exception>
    /// <exception cref="InvalidOperationException">The clock is past 2106-02-07T06:28:15Z, so an
    /// <c>INBOX</c> can get no <c>UIDVALIDITY</c>.</exception>
    public MailboxStore(
        IEnumerable<string> accountNames,
        bool allowAnonymous,
        TimeProvider timeProvider,
        long maxMessageBytes = 0,
        int maxMessages = DefaultMaxMessages,
        long maxTotalMessageBytes = DefaultMaxTotalMessageBytes,
        int maxMailboxes = DefaultMaxMailboxes)
        : this(timeProvider, maxMessageBytes, maxMessages, maxTotalMessageBytes, maxMailboxes)
    {
        ArgumentNullException.ThrowIfNull(accountNames);
        anonymousOwner = ReachOwners(accountNames, allowAnonymous);

        // A new store starts with no change: its INBOXes are where it begins, not a change to it.
        changeCount = 0;
    }

    private MailboxStore(TimeProvider timeProvider, long maxMessageBytes, int maxMessages, long maxTotalMessageBytes, int maxMailboxes)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegative(maxMessageBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTotalMessageBytes, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(maxMailboxes);

        this.timeProvider = timeProvider;
        MaxMessageBytes = maxMessageBytes;
        MaxMessages = maxMessages;
        MaxTotalMessageBytes = maxTotalMessageBytes;
        MaxMailboxes = maxMailboxes;
    }

    /// <summary>
    /// Creates a store that holds what <paramref name="files"/> hold and writes every later
    /// change back to them through <see cref="SaveChangesAsync"/> (ADR-0050, decision 7). A
    /// missing index is an empty store; leftover temporary files, and message files the index
    /// does not name, are ignored. Owners the index holds that are no longer accounts are kept,
    /// unreached. Every owner reached that lacks an <c>INBOX</c> gets one, which counts as a
    /// change, so the first <see cref="SaveChangesAsync"/> writes the index.
    /// </summary>
    /// <param name="files">The files the store is read from now and written to after each change.</param>
    /// <param name="accountNames">The configured accounts' names, as the constructor takes them.</param>
    /// <param name="allowAnonymous">Whether <c>--allow-anonymous</c> is set.</param>
    /// <param name="timeProvider">The clock for internal dates and <c>UIDVALIDITY</c>.</param>
    /// <param name="maxMessageBytes">The value of <see cref="MaxMessageBytes"/>; 0 means no limit.</param>
    /// <param name="maxMessages">The value of <see cref="MaxMessages"/>; at least 1.</param>
    /// <param name="maxTotalMessageBytes">The value of <see cref="MaxTotalMessageBytes"/>; at least 1.</param>
    /// <param name="maxMailboxes">The value of <see cref="MaxMailboxes"/>; at least 0.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The loaded store.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound is out of range.</exception>
    /// <exception cref="MailStoreLoadException">The index cannot be read or does not parse, or
    /// holds more than the bounds allow, or a message file it names is missing, unreadable or
    /// not the size it gives; nothing is loaded or written.</exception>
    public static async Task<MailboxStore> LoadAsync(
        MailStoreFiles files,
        IEnumerable<string> accountNames,
        bool allowAnonymous,
        TimeProvider timeProvider,
        long maxMessageBytes = 0,
        int maxMessages = DefaultMaxMessages,
        long maxTotalMessageBytes = DefaultMaxTotalMessageBytes,
        int maxMailboxes = DefaultMaxMailboxes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(accountNames);
        var store = new MailboxStore(timeProvider, maxMessageBytes, maxMessages, maxTotalMessageBytes, maxMailboxes);
        var index = await files.ReadIndexAsync(cancellationToken);
        if (index is not null)
        {
            store.Restore(files, index);
        }

        store.anonymousOwner = store.ReachOwners(accountNames, allowAnonymous);
        store.files = files;
        return store;
    }

    /// <summary>
    /// The most bytes one message may hold, <c>--max-filesize</c> as each server reads it,
    /// checked again here as a second line of defence; 0 means no limit.
    /// </summary>
    public long MaxMessageBytes { get; }

    /// <summary>
    /// The most messages the store holds, every owner and mailbox together.
    /// </summary>
    public int MaxMessages { get; }

    /// <summary>
    /// The most bytes of distinct messages the store holds; copies of one message count once.
    /// </summary>
    public long MaxTotalMessageBytes { get; }

    /// <summary>
    /// The most mailboxes the store holds other than the owners' <c>INBOX</c>es.
    /// </summary>
    public int MaxMailboxes { get; }

    /// <summary>
    /// How many changes have altered the store since it was created. A refused or no-op
    /// operation does not count.
    /// </summary>
    public long ChangeCount
    {
        get
        {
            lock (storeLock)
            {
                return changeCount;
            }
        }
    }

    /// <summary>
    /// Writes the store's index to its files when the store has changed since the last write,
    /// then deletes the files of messages no longer held; does nothing for a store made without
    /// files (ADR-0050, decision 7). A message's own file is in place already, renamed from its
    /// pending file when the message was stored. Writes are serialised, and each writes the
    /// store as it is when the write starts, so the index always holds a state the store held.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write; the next save writes the change instead.</param>
    /// <returns>A task that completes when the files hold the store's latest state.</returns>
    /// <exception cref="IOException">The index cannot be written: the change stays in memory,
    /// and the next save writes the index. Or a message file no longer held cannot be deleted:
    /// the index is written, and the file is left behind, ignored at the next load. The caller
    /// notes the message with <c>IExchangeLog.Note</c>; nothing ends. Other exceptions the file system throws, such as
    /// <see cref="UnauthorizedAccessException"/>, mean the same.</exception>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (files is null)
        {
            return;
        }

        await saveLock.WaitAsync(cancellationToken);
        try
        {
            await WriteChangesAsync(files, cancellationToken);
        }
        finally
        {
            saveLock.Release();
        }
    }

    /// <summary>
    /// Maps an SMTP <c>RCPT TO</c> forward path to its owner by the path's local part (ADR-0050,
    /// decision 5). The domain is ignored. The local part matches an account ordinally, or else
    /// the one account it equals ignoring case, if exactly one does. With
    /// <c>--allow-anonymous</c> every valid path maps to the anonymous owner.
    /// </summary>
    /// <param name="path">The path as <c>RCPT TO:</c> gave it, with or without its angle brackets.</param>
    /// <param name="recipient">The owner to deliver to when the outcome is
    /// <see cref="MailRecipientLookup.Deliverable"/>; otherwise <see langword="null"/>.</param>
    /// <returns>What the path names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public MailRecipientLookup LookUpRecipient(string path, out MailRecipient? recipient)
    {
        ArgumentNullException.ThrowIfNull(path);
        recipient = null;
        if (!MailRecipientPath.TryReadLocalPart(path, out var localPart))
        {
            return MailRecipientLookup.InvalidAddress;
        }

        var owner = anonymousOwner ?? accounts.GetValueOrDefault(localPart) ?? accountsIgnoringCase.GetValueOrDefault(localPart);
        if (owner is null)
        {
            return MailRecipientLookup.NoSuchAccount;
        }

        recipient = new MailRecipient(owner);
        return MailRecipientLookup.Deliverable;
    }

    /// <summary>
    /// The mailboxes a session logged in as <paramref name="accountName"/> acts on (ADR-0050,
    /// decision 2): the anonymous owner's with <c>--allow-anonymous</c>, whatever the name;
    /// otherwise the account's, matched ordinally, or an empty view for a name that names no
    /// account.
    /// </summary>
    /// <param name="accountName">The login's account name, or <see langword="null"/> when the
    /// login carried none.</param>
    /// <returns>The session's view.</returns>
    public MailView ViewFor(string? accountName) =>
        new(anonymousOwner ?? (accountName is null ? null : accounts.GetValueOrDefault(accountName)));

    /// <summary>
    /// Starts a message body on its way into the store (ADR-0050, decision 7): with a data
    /// directory it is streamed into a new pending file, otherwise held in memory. Hand it to
    /// <see cref="Deliver(IReadOnlyList{MailRecipient}, PendingMessage)"/> or
    /// <see cref="Append(MailView, string, PendingMessage, MailFlags, DateTimeOffset?, out MailStoredUid)"/>
    /// once its bytes are written, or dispose it to abandon it.
    /// </summary>
    /// <returns>The pending message. A pending file that cannot be created does not throw; the
    /// message is refused with <see cref="MailStoreOutcome.StorageFailed"/> when it is handed over.</returns>
    public PendingMessage CreatePendingMessage() =>
        new(files, MaxMessageBytes > 0 ? Math.Min(MaxMessageBytes, MaxTotalMessageBytes) : MaxTotalMessageBytes);

    /// <summary>
    /// Delivers one message, given whole, to the <c>INBOX</c> of every recipient, one copy per
    /// entry, all of them or none. The copies share the message's bytes, counted once. With a
    /// data directory the bytes are held in memory until <see cref="SaveChangesAsync"/> writes
    /// their message file, and read from it after that; a write that fails leaves them held for
    /// the next save (BL-191's shape, kept until the SMTP server streams its bodies through
    /// <see cref="Deliver(IReadOnlyList{MailRecipient}, PendingMessage)"/>).
    /// </summary>
    /// <param name="recipients">The owners <see cref="LookUpRecipient"/> found; an owner named
    /// twice gets two copies.</param>
    /// <param name="message">The message's bytes, stored exactly.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/>,
    /// <see cref="MailStoreOutcome.MessageTooLarge"/> or <see cref="MailStoreOutcome.StoreFull"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recipients"/> is <see langword="null"/>.</exception>
    public MailStoreOutcome Deliver(IReadOnlyList<MailRecipient> recipients, ReadOnlySpan<byte> message)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        if (IsTooLarge(message.Length))
        {
            return MailStoreOutcome.MessageTooLarge;
        }

        lock (storeLock)
        {
            var inboxes = recipients.Select(recipient => recipient.Owner.Inbox).ToList();
            if (inboxes.Count == 0)
            {
                return MailStoreOutcome.Succeeded;
            }

            if (!HasRoomFor(inboxes, message.Length))
            {
                return MailStoreOutcome.StoreFull;
            }

            DeliverBody(inboxes, HoldBody(message));
            return MailStoreOutcome.Succeeded;
        }
    }

    /// <summary>
    /// Delivers one message, streamed in through <paramref name="message"/>, to the
    /// <c>INBOX</c> of every recipient, one copy per entry, all of them or none. The copies share
    /// the message's bytes and its message file, counted once. <paramref name="message"/> is
    /// disposed whatever the outcome: its pending file becomes the message file when it is
    /// stored, and is deleted when it is not.
    /// </summary>
    /// <param name="recipients">The owners <see cref="LookUpRecipient"/> found; an owner named
    /// twice gets two copies.</param>
    /// <param name="message">The pending message, its bytes all written.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/>,
    /// <see cref="MailStoreOutcome.MessageTooLarge"/>, <see cref="MailStoreOutcome.StorageFailed"/>
    /// or <see cref="MailStoreOutcome.StoreFull"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="message"/> was stored, refused
    /// or disposed already.</exception>
    public MailStoreOutcome Deliver(IReadOnlyList<MailRecipient> recipients, PendingMessage message)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(message);
        message.Close();
        using (message)
        {
            var outcome = CheckPending(message);
            return outcome != MailStoreOutcome.Succeeded || recipients.Count == 0
                ? outcome
                : DeliverPending(recipients, message);
        }
    }

    private MailStoreOutcome DeliverPending(IReadOnlyList<MailRecipient> recipients, PendingMessage message)
    {
        lock (storeLock)
        {
            var inboxes = recipients.Select(recipient => recipient.Owner.Inbox).ToList();
            if (!HasRoomFor(inboxes, message.Length))
            {
                return MailStoreOutcome.StoreFull;
            }

            var body = KeepBody(message);
            if (body is null)
            {
                return MailStoreOutcome.StorageFailed;
            }

            DeliverBody(inboxes, body);
            return MailStoreOutcome.Succeeded;
        }
    }

    /// <summary>
    /// Appends one message to a named mailbox, as IMAP's <c>APPEND</c> does.
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="mailboxName">The mailbox's name.</param>
    /// <param name="message">The message's bytes, stored exactly.</param>
    /// <param name="flags">The message's flags.</param>
    /// <param name="internalDate">The date <c>APPEND</c> gave, or <see langword="null"/> for now.</param>
    /// <param name="stored">Where the message was stored, when the outcome is
    /// <see cref="MailStoreOutcome.Succeeded"/>.</param>
    /// <returns>As <see cref="Append(MailView, string, PendingMessage, MailFlags, DateTimeOffset?, out MailStoredUid)"/> returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> or
    /// <paramref name="mailboxName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="flags"/> holds a bit that is no flag.</exception>
    public MailStoreOutcome Append(
        MailView view,
        string mailboxName,
        ReadOnlySpan<byte> message,
        MailFlags flags,
        DateTimeOffset? internalDate,
        out MailStoredUid stored)
    {
        ThrowIfNotFlags(flags);
        var pending = CreatePendingMessage();
        pending.Body.Write(message);
        return Append(view, mailboxName, pending, flags, internalDate, out stored);
    }

    /// <summary>
    /// Appends one message, streamed in through <paramref name="message"/>, to a named mailbox,
    /// as IMAP's <c>APPEND</c> does. <paramref name="message"/> is disposed whatever the
    /// outcome: its pending file becomes the message file when it is stored, and is deleted when
    /// it is not.
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="mailboxName">The mailbox's name.</param>
    /// <param name="message">The pending message, its bytes all written.</param>
    /// <param name="flags">The message's flags.</param>
    /// <param name="internalDate">The date <c>APPEND</c> gave, or <see langword="null"/> for now.</param>
    /// <param name="stored">Where the message was stored, when the outcome is
    /// <see cref="MailStoreOutcome.Succeeded"/>.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/>,
    /// <see cref="MailStoreOutcome.MailboxMissing"/>,
    /// <see cref="MailStoreOutcome.MessageTooLarge"/>, <see cref="MailStoreOutcome.StorageFailed"/>
    /// or <see cref="MailStoreOutcome.StoreFull"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="flags"/> holds a bit that is no flag.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="message"/> was stored, refused
    /// or disposed already.</exception>
    public MailStoreOutcome Append(
        MailView view,
        string mailboxName,
        PendingMessage message,
        MailFlags flags,
        DateTimeOffset? internalDate,
        out MailStoredUid stored)
    {
        ArgumentNullException.ThrowIfNull(message);
        stored = default;
        message.Close();
        using (message)
        {
            ThrowIfNotFlags(flags);
            lock (storeLock)
            {
                var mailbox = FindMailbox(view, mailboxName);
                var outcome = mailbox is null ? MailStoreOutcome.MailboxMissing : CheckRoomForOne(mailbox, message);
                var body = outcome == MailStoreOutcome.Succeeded ? KeepBody(message) : null;
                if (body is null)
                {
                    return outcome == MailStoreOutcome.Succeeded ? MailStoreOutcome.StorageFailed : outcome;
                }

                var uid = AddMessage(mailbox!, body, internalDate ?? timeProvider.GetUtcNow(), flags);
                stored = new MailStoredUid(mailbox!.UidValidity, uid);
                changeCount++;
                return MailStoreOutcome.Succeeded;
            }
        }
    }

    /// <summary>
    /// The names of the view's mailboxes in ordinal order; none for an empty view.
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <returns>The mailbox names, <c>INBOX</c> in capitals.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<string> ListMailboxes(MailView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        lock (storeLock)
        {
            return view.Owner is null ? [] : [.. view.Owner.Mailboxes.Keys.Order(StringComparer.Ordinal)];
        }
    }

    /// <summary>
    /// A snapshot of one mailbox, as IMAP's <c>SELECT</c>, <c>EXAMINE</c> and <c>STATUS</c>
    /// report it.
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="mailboxName">The mailbox's name.</param>
    /// <param name="snapshot">The mailbox as it is now, when the outcome is
    /// <see cref="MailStoreOutcome.Succeeded"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/> or <see cref="MailStoreOutcome.MailboxMissing"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> or
    /// <paramref name="mailboxName"/> is <see langword="null"/>.</exception>
    public MailStoreOutcome ReadMailbox(MailView view, string mailboxName, out MailboxSnapshot? snapshot)
    {
        lock (storeLock)
        {
            snapshot = FindMailbox(view, mailboxName)?.Snapshot();
            return snapshot is null ? MailStoreOutcome.MailboxMissing : MailStoreOutcome.Succeeded;
        }
    }

    /// <summary>
    /// The bytes of one message, read whole: with a data directory from its message file, as it
    /// is now; otherwise from memory (ADR-0050, decision 7).
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="mailboxName">The mailbox's name.</param>
    /// <param name="uid">The message's UID.</param>
    /// <param name="message">The message's bytes when the outcome is
    /// <see cref="MailStoreOutcome.Succeeded"/>; otherwise empty.</param>
    /// <returns>As <see cref="OpenMessage"/> returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> or
    /// <paramref name="mailboxName"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">As <see cref="OpenMessage"/> throws it, or the message file
    /// cannot be read.</exception>
    public MailStoreOutcome FetchMessage(MailView view, string mailboxName, uint uid, out ReadOnlyMemory<byte> message)
    {
        var outcome = OpenMessage(view, mailboxName, uid, out var stream);
        message = stream is null ? ReadOnlyMemory<byte>.Empty : ReadAll(stream);
        return outcome;
    }

    /// <summary>
    /// Opens the bytes of one message to read: with a data directory from its message file, as
    /// it is now; otherwise from memory (ADR-0050, decision 7).
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="mailboxName">The mailbox's name.</param>
    /// <param name="uid">The message's UID.</param>
    /// <param name="message">A readable stream of the message's bytes when the outcome is
    /// <see cref="MailStoreOutcome.Succeeded"/>, which the caller disposes; otherwise
    /// <see langword="null"/>.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/>,
    /// <see cref="MailStoreOutcome.MailboxMissing"/> or <see cref="MailStoreOutcome.MessageMissing"/>
    /// (a message another session expunged meanwhile is missing).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> or
    /// <paramref name="mailboxName"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The message file cannot be opened, as when something other
    /// than the store removed it. Other exceptions the file system throws, such as
    /// <see cref="UnauthorizedAccessException"/>, mean the same.</exception>
    public MailStoreOutcome OpenMessage(MailView view, string mailboxName, uint uid, out Stream? message)
    {
        lock (storeLock)
        {
            var outcome = FindMessage(view, mailboxName, uid, out var stored);
            message = stored is null ? null : OpenBody(stored.Body);
            return outcome;
        }
    }

    /// <summary>
    /// Sets, clears or replaces one message's flags, as IMAP's <c>STORE</c> does.
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="mailboxName">The mailbox's name.</param>
    /// <param name="uid">The message's UID.</param>
    /// <param name="change">How <paramref name="flags"/> combine with the message's flags.</param>
    /// <param name="flags">The flags to set, clear or replace with.</param>
    /// <param name="result">The message's flags afterwards, when the outcome is
    /// <see cref="MailStoreOutcome.Succeeded"/>.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/>,
    /// <see cref="MailStoreOutcome.MailboxMissing"/> or <see cref="MailStoreOutcome.MessageMissing"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> or
    /// <paramref name="mailboxName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="change"/> is undefined, or
    /// <paramref name="flags"/> holds a bit that is no flag.</exception>
    public MailStoreOutcome ChangeFlags(
        MailView view,
        string mailboxName,
        uint uid,
        MailFlagChange change,
        MailFlags flags,
        out MailFlags result)
    {
        ThrowIfNotFlags(flags);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)change, (uint)MailFlagChange.Replace, nameof(change));
        lock (storeLock)
        {
            var outcome = FindMessage(view, mailboxName, uid, out var stored);
            result = stored?.Flags ?? MailFlags.None;
            if (stored is null)
            {
                return outcome;
            }

            result = Combine(stored.Flags, change, flags);
            changeCount += result == stored.Flags ? 0 : 1;
            stored.Flags = result;
            return outcome;
        }
    }

    /// <summary>
    /// Removes every message flagged <c>\Deleted</c> from a mailbox, as IMAP's <c>EXPUNGE</c>
    /// does. Their UIDs are never given again.
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="mailboxName">The mailbox's name.</param>
    /// <param name="expungedUids">The UIDs removed, in ascending order.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/> or <see cref="MailStoreOutcome.MailboxMissing"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> or
    /// <paramref name="mailboxName"/> is <see langword="null"/>.</exception>
    public MailStoreOutcome Expunge(MailView view, string mailboxName, out IReadOnlyList<uint> expungedUids)
    {
        lock (storeLock)
        {
            var mailbox = FindMailbox(view, mailboxName);
            expungedUids = [];
            if (mailbox is null)
            {
                return MailStoreOutcome.MailboxMissing;
            }

            expungedUids = mailbox.DeletedUids();
            changeCount += RemoveMessages(mailbox, expungedUids) == 0 ? 0 : 1;
            return MailStoreOutcome.Succeeded;
        }
    }

    /// <summary>
    /// Copies messages to another mailbox of the same view, as IMAP's <c>COPY</c> does: each
    /// copy keeps its flags and internal date, gets a new UID there, and shares the original's
    /// bytes. A UID the source does not hold is skipped. All of them or none are copied.
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="sourceMailboxName">The mailbox copied from.</param>
    /// <param name="uids">The UIDs to copy.</param>
    /// <param name="destinationMailboxName">The mailbox copied to; may be the source.</param>
    /// <param name="copied">What was copied, when the outcome is <see cref="MailStoreOutcome.Succeeded"/>;
    /// otherwise <see langword="null"/>.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/>,
    /// <see cref="MailStoreOutcome.MailboxMissing"/> or <see cref="MailStoreOutcome.StoreFull"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MailStoreOutcome Copy(
        MailView view,
        string sourceMailboxName,
        IEnumerable<uint> uids,
        string destinationMailboxName,
        out MailCopyResult? copied)
    {
        ArgumentNullException.ThrowIfNull(uids);
        copied = null;
        lock (storeLock)
        {
            var source = FindMailbox(view, sourceMailboxName);
            var destination = FindMailbox(view, destinationMailboxName);
            if (source is null || destination is null)
            {
                return MailStoreOutcome.MailboxMissing;
            }

            var originals = uids.Distinct().Order().Select(uid => source.Messages.GetValueOrDefault(uid)).OfType<StoredMessage>().ToList();
            if (!HasRoomForCopies(destination, originals.Count))
            {
                return MailStoreOutcome.StoreFull;
            }

            var copyUids = originals.Select(original => AddMessage(destination, original.Body, original.InternalDate, original.Flags)).ToList();
            changeCount += copyUids.Count == 0 ? 0 : 1;
            copied = new MailCopyResult(destination.UidValidity, [.. originals.Select(original => original.Uid)], copyUids);
            return MailStoreOutcome.Succeeded;
        }
    }

    /// <summary>
    /// Creates an empty mailbox, as IMAP's <c>CREATE</c> does, with a <c>UIDVALIDITY</c> the
    /// store has never given before.
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="mailboxName">The new mailbox's name.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/>,
    /// <see cref="MailStoreOutcome.MailboxMissing"/> (an empty view),
    /// <see cref="MailStoreOutcome.InvalidName"/>, <see cref="MailStoreOutcome.AlreadyExists"/> or
    /// <see cref="MailStoreOutcome.TooManyMailboxes"/>, or <see cref="MailStoreOutcome.StoreFull"/>
    /// when no <c>UIDVALIDITY</c> is left to give.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> or
    /// <paramref name="mailboxName"/> is <see langword="null"/>.</exception>
    public MailStoreOutcome CreateMailbox(MailView view, string mailboxName)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(mailboxName);
        lock (storeLock)
        {
            var outcome = CheckNewMailbox(view.Owner, mailboxName);
            if (outcome != MailStoreOutcome.Succeeded)
            {
                return outcome;
            }

            AddMailbox(view.Owner!, new StoredMailbox(mailboxName, IssueUidValidity(), 1));
            changeCount++;
            return outcome;
        }
    }

    /// <summary>
    /// Deletes a mailbox and its messages, as IMAP's <c>DELETE</c> does. <c>INBOX</c> cannot be
    /// deleted.
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="mailboxName">The mailbox's name.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/>,
    /// <see cref="MailStoreOutcome.MailboxMissing"/> or
    /// <see cref="MailStoreOutcome.InboxCannotBeDeleted"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> or
    /// <paramref name="mailboxName"/> is <see langword="null"/>.</exception>
    public MailStoreOutcome DeleteMailbox(MailView view, string mailboxName)
    {
        lock (storeLock)
        {
            var mailbox = FindMailbox(view, mailboxName);
            if (mailbox is null || mailbox.Name == MailboxName.Inbox)
            {
                return mailbox is null ? MailStoreOutcome.MailboxMissing : MailStoreOutcome.InboxCannotBeDeleted;
            }

            RemoveMessages(mailbox, [.. mailbox.Messages.Keys]);
            view.Owner!.Mailboxes.Remove(mailbox.Name);
            mailboxCount--;
            changeCount++;
            return MailStoreOutcome.Succeeded;
        }
    }

    /// <summary>
    /// Renames a mailbox, as IMAP's <c>RENAME</c> does. The renamed mailbox keeps its messages,
    /// their UIDs and its next UID, and gets a <c>UIDVALIDITY</c> the store has never given
    /// before. Renaming <c>INBOX</c> moves all its messages into a new mailbox of the new name
    /// and leaves <c>INBOX</c> empty, with its <c>UIDVALIDITY</c> and next UID unchanged (RFC
    /// 3501 section 6.3.5).
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="mailboxName">The mailbox's name.</param>
    /// <param name="newMailboxName">The name it gets.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/>,
    /// <see cref="MailStoreOutcome.MailboxMissing"/>, <see cref="MailStoreOutcome.InvalidName"/>,
    /// <see cref="MailStoreOutcome.AlreadyExists"/> or
    /// <see cref="MailStoreOutcome.TooManyMailboxes"/> (renaming <c>INBOX</c> adds a mailbox), or
    /// <see cref="MailStoreOutcome.StoreFull"/> when no <c>UIDVALIDITY</c> is left to give.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MailStoreOutcome RenameMailbox(MailView view, string mailboxName, string newMailboxName)
    {
        ArgumentNullException.ThrowIfNull(newMailboxName);
        lock (storeLock)
        {
            var mailbox = FindMailbox(view, mailboxName);
            var renamesInbox = mailbox?.Name == MailboxName.Inbox;
            var outcome = mailbox is null ? MailStoreOutcome.MailboxMissing : CheckNewMailbox(view.Owner, newMailboxName, renamesInbox);
            if (outcome != MailStoreOutcome.Succeeded)
            {
                return outcome;
            }

            var owner = view.Owner!;
            var renamed = new StoredMailbox(newMailboxName, IssueUidValidity(), mailbox!.NextUid) { Messages = mailbox.Messages };
            mailbox.Messages = [];
            if (!renamesInbox)
            {
                owner.Mailboxes.Remove(mailbox.Name);
                mailboxCount--;
            }

            AddMailbox(owner, renamed);
            changeCount++;
            return outcome;
        }
    }

    /// <summary>
    /// Takes the maildrop lock of the view's owner at a successful POP3 login, and fixes the
    /// session's view of its <c>INBOX</c> (ADR-0050, decision 4). An empty view gets an empty
    /// maildrop that locks nothing.
    /// </summary>
    /// <param name="view">The session's view.</param>
    /// <param name="maildrop">The lock, when the outcome is <see cref="MailStoreOutcome.Succeeded"/>;
    /// the session disposes it when it ends, however it ends. Otherwise <see langword="null"/>.</param>
    /// <returns><see cref="MailStoreOutcome.Succeeded"/> or
    /// <see cref="MailStoreOutcome.MaildropLocked"/> when another session holds the lock.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> is <see langword="null"/>.</exception>
    public MailStoreOutcome LockMaildrop(MailView view, out MaildropLock? maildrop)
    {
        ArgumentNullException.ThrowIfNull(view);
        maildrop = null;
        lock (storeLock)
        {
            var owner = view.Owner;
            if (owner is not null && !lockedMaildrops.Add(owner))
            {
                return MailStoreOutcome.MaildropLocked;
            }

            StoredMessage[] messages = owner is null ? [] : [.. owner.Inbox.Messages.Values];

            // Pinned while the lock is held, so a message removed meanwhile can still be read.
            Array.ForEach(messages, message => message.Body.ReferenceCount++);
            maildrop = new MaildropLock(this, owner, messages);
            return MailStoreOutcome.Succeeded;
        }
    }

    /// <summary>
    /// Opens a message's bytes to read: its message file, or its bytes in memory.
    /// </summary>
    internal Stream OpenBody(MessageBody body)
    {
        // Read once: a save may let go of held bytes meanwhile, once their file is written.
        var bytes = body.Bytes;
        return bytes is null ? files!.OpenMessage(body.FileNumber) : new MemoryStream(bytes, writable: false);
    }

    /// <summary>
    /// Reads <paramref name="stream"/> to its end and disposes it.
    /// </summary>
    internal static byte[] ReadAll(Stream stream)
    {
        using (stream)
        {
            var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }
    }

    /// <summary>
    /// Removes the messages with <paramref name="uids"/> from <paramref name="owner"/>'s
    /// <c>INBOX</c>, skipping any no longer there.
    /// </summary>
    /// <returns>How many were removed.</returns>
    internal int RemoveFromInbox(OwnerMailboxes owner, IReadOnlyList<uint> uids)
    {
        lock (storeLock)
        {
            var inbox = owner.Inbox;
            var removed = RemoveMessages(inbox, [.. uids.Distinct().Where(inbox.Messages.ContainsKey)]);
            changeCount += removed == 0 ? 0 : 1;
            return removed;
        }
    }

    /// <summary>
    /// Releases <paramref name="owner"/>'s maildrop lock, and unpins the bodies it held,
    /// releasing any no message refers to any more.
    /// </summary>
    internal void ReleaseMaildrop(OwnerMailboxes owner, IReadOnlyList<MessageBody> bodies)
    {
        lock (storeLock)
        {
            lockedMaildrops.Remove(owner);
            foreach (var body in bodies)
            {
                Unreference(body);
            }
        }
    }

    private static void ThrowIfNotFlags(MailFlags flags) =>
        ArgumentOutOfRangeException.ThrowIfNotEqual((byte)(flags & ~SystemFlags), (byte)0, nameof(flags));

    private static MailFlags Combine(MailFlags current, MailFlagChange change, MailFlags flags) => change switch
    {
        MailFlagChange.Add => current | flags,
        MailFlagChange.Remove => current & ~flags,
        _ => flags,
    };

    private static StoredMailbox? FindMailbox(MailView view, string mailboxName)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(mailboxName);
        return view.Owner?.Find(mailboxName);
    }

    private static MailStoreOutcome FindMessage(MailView view, string mailboxName, uint uid, out StoredMessage? message)
    {
        message = null;
        var mailbox = FindMailbox(view, mailboxName);
        if (mailbox is null)
        {
            return MailStoreOutcome.MailboxMissing;
        }

        message = mailbox.Messages.GetValueOrDefault(uid);
        return message is null ? MailStoreOutcome.MessageMissing : MailStoreOutcome.Succeeded;
    }

    private static void DeleteMessageFiles(MailStoreFiles files, IReadOnlyList<ulong> fileNumbers)
    {
        Exception? firstFailure = null;
        foreach (var fileNumber in fileNumbers)
        {
            try
            {
                files.DeleteMessage(fileNumber);
            }
            catch (Exception failure)
            {
                // Left behind: the index no longer names it, so the next load ignores it.
                firstFailure ??= failure;
            }
        }

        if (firstFailure is not null)
        {
            throw new IOException(firstFailure.Message, firstFailure);
        }
    }

    private void Restore(MailStoreFiles files, byte[] index)
    {
        MailStoreIndexContents contents;
        try
        {
            contents = MailStoreIndex.Decode(index, MaxMessages, MaxTotalMessageBytes, MaxMailboxes);
        }
        catch (InvalidDataException malformed)
        {
            throw new MailStoreLoadException(files.IndexPath, malformed.Message, malformed);
        }

        foreach (var body in contents.Bodies.Values)
        {
            files.RequireMessageFile(body.FileNumber, body.Length);
        }

        nextFileNumber = contents.NextFileNumber;
        lastUidValidity = contents.LastUidValidity;
        messageCount = contents.MessageCount;
        totalMessageBytes = contents.TotalMessageBytes;
        mailboxCount = contents.MailboxCount;
        contents.Owners.ForEach(owner => owners.Add(owner.Name, owner));
    }

    private async Task WriteChangesAsync(MailStoreFiles files, CancellationToken cancellationToken)
    {
        MessageBody[] bodies;
        byte[]? index;
        ulong[] released;
        long snapshotChangeCount;
        lock (storeLock)
        {
            // Unchanged since the last write: the index written then names none of the released
            // files, and every held body was written before it.
            bodies = [.. unwrittenBodies];
            index = changeCount == savedChangeCount ? null : MailStoreIndex.Encode(nextFileNumber, lastUidValidity, owners.Values);
            released = [.. releasedFileNumbers];
            snapshotChangeCount = changeCount;
        }

        foreach (var body in bodies)
        {
            await files.WriteMessageAsync(body.FileNumber, body.Bytes!, cancellationToken);
            MarkWritten(body);
        }

        if (index is not null)
        {
            await files.WriteIndexAsync(index, cancellationToken);
        }

        lock (storeLock)
        {
            savedChangeCount = snapshotChangeCount;
            releasedFileNumbers.RemoveAll(released.Contains);
        }

        DeleteMessageFiles(files, released);
    }

    /// <summary>
    /// Lets go of a held body's bytes once its message file is written: from then on they are
    /// read from the file.
    /// </summary>
    private void MarkWritten(MessageBody body)
    {
        lock (storeLock)
        {
            // Released while it was being written: its file goes once an index no longer names it.
            if (!unwrittenBodies.Remove(body))
            {
                releasedFileNumbers.Add(body.FileNumber);
            }

            body.Bytes = null;
        }
    }

    /// <summary>
    /// Makes the owners reached - the anonymous owner alone with <c>--allow-anonymous</c>,
    /// otherwise every account with a non-empty name - and gives each an <c>INBOX</c> it lacks.
    /// </summary>
    /// <returns>The anonymous owner with <c>--allow-anonymous</c>; otherwise <see langword="null"/>.</returns>
    private OwnerMailboxes? ReachOwners(IEnumerable<string> accountNames, bool allowAnonymous)
    {
        if (allowAnonymous)
        {
            return ReachOwner(string.Empty);
        }

        foreach (var name in accountNames.Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal))
        {
            AddAccount(ReachOwner(name));
        }

        return null;
    }

    private OwnerMailboxes ReachOwner(string name)
    {
        if (!owners.TryGetValue(name, out var owner))
        {
            owner = new OwnerMailboxes(name);
            owners.Add(name, owner);
        }

        if (!owner.Mailboxes.ContainsKey(MailboxName.Inbox))
        {
            owner.Mailboxes.Add(MailboxName.Inbox, new StoredMailbox(MailboxName.Inbox, IssueUidValidity(), 1));
            changeCount++;
        }

        return owner;
    }

    private void AddAccount(OwnerMailboxes owner)
    {
        accounts.Add(owner.Name, owner);

        // A name two accounts share ignoring case matches neither of them that way.
        accountsIgnoringCase[owner.Name] = accountsIgnoringCase.ContainsKey(owner.Name) ? null : owner;
    }

    /// <summary>
    /// The larger of the clock's Unix time in seconds and one more than the last
    /// <c>UIDVALIDITY</c> given, so no two mailboxes ever get the same one (RFC 3501 section
    /// 2.3.1.1).
    /// </summary>
    /// <exception cref="InvalidOperationException">That is past <see cref="uint.MaxValue"/>: the
    /// clock is past 2106-02-07T06:28:15Z, or every <c>UIDVALIDITY</c> has been given.</exception>
    private uint IssueUidValidity()
    {
        var next = NextUidValidity();
        lastUidValidity = next <= uint.MaxValue
            ? (uint)next
            : throw new InvalidOperationException("The mail store has no UIDVALIDITY left to give.");
        return lastUidValidity;
    }

    private long NextUidValidity() => Math.Max(timeProvider.GetUtcNow().ToUnixTimeSeconds(), lastUidValidity + 1L);

    private MailStoreOutcome CheckNewMailbox(OwnerMailboxes? owner, string mailboxName, bool addsMailbox = true)
    {
        if (owner is null)
        {
            return MailStoreOutcome.MailboxMissing;
        }

        if (!MailboxName.IsValid(mailboxName))
        {
            return MailStoreOutcome.InvalidName;
        }

        return owner.Find(mailboxName) is null ? CheckRoomForMailbox(addsMailbox) : MailStoreOutcome.AlreadyExists;
    }

    private MailStoreOutcome CheckRoomForMailbox(bool addsMailbox)
    {
        if (addsMailbox && mailboxCount >= MaxMailboxes)
        {
            return MailStoreOutcome.TooManyMailboxes;
        }

        // A new mailbox needs a UIDVALIDITY, and a uint runs out in 2106.
        return NextUidValidity() > uint.MaxValue ? MailStoreOutcome.StoreFull : MailStoreOutcome.Succeeded;
    }

    private void AddMailbox(OwnerMailboxes owner, StoredMailbox mailbox)
    {
        owner.Mailboxes.Add(mailbox.Name, mailbox);
        mailboxCount++;
    }

    private bool IsTooLarge(long size) => MaxMessageBytes > 0 && size > MaxMessageBytes;

    private bool HasRoomFor(List<StoredMailbox> inboxes, long size) =>
        messageCount + (long)inboxes.Count <= MaxMessages
        && totalMessageBytes + size <= MaxTotalMessageBytes
        && inboxes.GroupBy(inbox => inbox).All(group => group.Key.CanGiveUids(group.Count()));

    /// <summary>
    /// What refuses a closed pending message before the store is looked at: its size, or a
    /// pending file that failed.
    /// </summary>
    private MailStoreOutcome CheckPending(PendingMessage message)
    {
        if (IsTooLarge(message.Length))
        {
            return MailStoreOutcome.MessageTooLarge;
        }

        return message.StorageFailure is null ? MailStoreOutcome.Succeeded : MailStoreOutcome.StorageFailed;
    }

    private MailStoreOutcome CheckRoomForOne(StoredMailbox mailbox, PendingMessage message)
    {
        var outcome = CheckPending(message);
        if (outcome != MailStoreOutcome.Succeeded)
        {
            return outcome;
        }

        return HasRoomFor([mailbox], message.Length) ? MailStoreOutcome.Succeeded : MailStoreOutcome.StoreFull;
    }

    private bool HasRoomForCopies(StoredMailbox destination, int count) =>
        messageCount + (long)count <= MaxMessages && destination.CanGiveUids(count);

    private void DeliverBody(List<StoredMailbox> inboxes, MessageBody body)
    {
        var now = timeProvider.GetUtcNow();
        inboxes.ForEach(inbox => AddMessage(inbox, body, now, MailFlags.None));
        changeCount++;
    }

    /// <summary>
    /// Holds a message given whole under the next message file number; with a data directory
    /// its message file is written by the next save.
    /// </summary>
    private MessageBody HoldBody(ReadOnlySpan<byte> message)
    {
        totalMessageBytes += message.Length;
        var body = new MessageBody(nextFileNumber++, message.Length, message.ToArray());
        if (files is not null)
        {
            unwrittenBodies.Add(body);
        }

        return body;
    }

    /// <summary>
    /// Keeps a closed pending message's bytes under the next message file number: its pending
    /// file renamed to it, or its bytes taken from memory.
    /// </summary>
    /// <returns>The body; <see langword="null"/> when the pending file cannot be renamed, which
    /// <see cref="PendingMessage.StorageFailure"/> then says why.</returns>
    private MessageBody? KeepBody(PendingMessage message)
    {
        if (!message.TryKeep(nextFileNumber, out var bytes))
        {
            return null;
        }

        totalMessageBytes += message.Length;
        return new MessageBody(nextFileNumber++, message.Length, bytes);
    }

    /// <summary>
    /// Drops one reference to <paramref name="body"/>, and lets go of it when none is left: its
    /// bytes leave the count, and its message file is deleted after the next index.
    /// </summary>
    private void Unreference(MessageBody body)
    {
        if (--body.ReferenceCount > 0)
        {
            return;
        }

        // Bytes held and never written are never written; bytes held by a store without files have no file.
        totalMessageBytes -= body.Length;
        if (!unwrittenBodies.Remove(body) && body.Bytes is null)
        {
            releasedFileNumbers.Add(body.FileNumber);
        }
    }

    private uint AddMessage(StoredMailbox mailbox, MessageBody body, DateTimeOffset internalDate, MailFlags flags)
    {
        var uid = mailbox.NextUid++;
        mailbox.Messages.Add(uid, new StoredMessage(uid, body, internalDate, flags));
        body.ReferenceCount++;
        messageCount++;
        return uid;
    }

    /// <summary>
    /// Removes the messages with <paramref name="uids"/>, all held by
    /// <paramref name="mailbox"/>, releasing bytes no message refers to any more.
    /// </summary>
    /// <returns>How many were removed.</returns>
    private int RemoveMessages(StoredMailbox mailbox, IReadOnlyList<uint> uids)
    {
        foreach (var uid in uids)
        {
            var body = mailbox.Messages[uid].Body;
            mailbox.Messages.Remove(uid);
            messageCount--;
            Unreference(body);
        }

        return uids.Count;
    }
}
