using Surl.Authentication;
using Surl.Content;
using Surl.Extended.Protocol.DnsQuery;
using Surl.Extended.Protocol.Redis;
using Surl.MailStore;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ldap;

namespace Surl.Composition;

/// <summary>
/// The stores of one composed instance, as a test controls them (ADR-0124, decision 3): the content
/// store with its object metadata index, the mail store, the message bus and the account book. Seed
/// each through the path a protocol writes by; reset, snapshot and restore one store or all of them
/// together. <c>Surl.Api</c>, <c>Surl.Mcp</c> and the loopback control endpoint all drive this
/// surface, so they behave alike.
/// </summary>
/// <remarks>
/// <para>
/// Reset and restore of the whole instance run one store at a time in the fixed order of
/// <see cref="StoreOrder"/> - content, mail, bus, then accounts (ADR-0100 decision 1's load order,
/// accounts last) - and the first store that refuses stops it with a
/// <see cref="ComposedStoreException"/> naming that store; the stores before it are already reset
/// or restored. A snapshot is taken in the same order.
/// </para>
/// <para>
/// With <c>--directory</c>, the content store, the mail store and the message bus are on disk and
/// refuse reset, snapshot and restore, so a test fixture never empties a real directory; seeding
/// them is ordinary writing and is allowed. Accounts are always in memory and always controllable.
/// </para>
/// <para>
/// Public because more than one host calls it, but not a supported programming interface: it may
/// change in any release (ADR-0099, decision 1).
/// </para>
/// </remarks>
public sealed partial class ComposedStores
{
    /// <summary>
    /// The most bytes a snapshot of this instance's stores may hold together unless a host sets
    /// another bound: 268435456 (256 MiB), ADR-0124 decision 3's <c>MaxSnapshotBytes</c>.
    /// </summary>
    public const long DefaultMaxSnapshotBytes = 268_435_456;

    private readonly ContentStore content;
    private readonly MailboxStore mail;
    private readonly MessageBus.MessageBus bus;
    private readonly AccountBook accounts;
    private readonly LdapProtocolServer? ldapServer;
    private readonly RedisKeyspace? redisKeyspace;
    private readonly DnsZoneSet? dnsZones;
    private readonly List<ComposedStoreRegistration> controls;
    private readonly AccountBook? proxyAccounts;
    private readonly ISurlEventPublisher events;

    internal ComposedStores(
        ContentStore content,
        ObjectMetadataIndex metadataIndex,
        MailboxStore mail,
        MessageBus.MessageBus bus,
        AccountBook accounts,
        long maxSnapshotBytes = DefaultMaxSnapshotBytes,
        IReadOnlyList<ComposedStoreRegistration>? additionalRegistrations = null,
        LdapProtocolServer? ldapServer = null,
        RedisKeyspace? redisKeyspace = null,
        DnsZoneSet? dnsZones = null,
        AccountBook? proxyAccounts = null,
        ISurlEventPublisher? events = null)
    {
        this.proxyAccounts = proxyAccounts;
        this.events = events ?? DiscardingSurlEventPublisher.Instance;
        this.redisKeyspace = redisKeyspace;
        this.dnsZones = dnsZones;
        this.content = content;
        MetadataIndex = metadataIndex;
        this.mail = mail;
        this.bus = bus;
        this.accounts = accounts;
        this.ldapServer = ldapServer;
        MaxSnapshotBytes = maxSnapshotBytes;
        controls =
        [
            new(StoreKind.Content, cancellationToken => content.ResetAsync(metadataIndex, cancellationToken), TakeContentSnapshotAsync),
            new(StoreKind.Mail, _ => Run(mail.Reset), TakeMailSnapshotAsync),
            new(StoreKind.Bus, _ => Run(bus.Reset), TakeBusSnapshotAsync),
            new(StoreKind.Accounts, _ => Run(accounts.Reset), TakeAccountSnapshotAsync),
            .. ProxyAccountsRegistration(),
            .. additionalRegistrations ?? [],
        ];
    }

    /// <summary>
    /// The order a whole instance is reset, snapshotted and restored in: content, mail, bus, then
    /// accounts (ADR-0124, decision 3).
    /// </summary>
    public static IReadOnlyList<StoreKind> StoreOrder { get; } = [StoreKind.Content, StoreKind.Mail, StoreKind.Bus, StoreKind.Accounts];

    /// <summary>
    /// The order this instance's registrations run in: <see cref="StoreOrder"/>, then any store
    /// registered after the four.
    /// </summary>
    public IReadOnlyList<StoreKind> RegisteredStoreOrder => [.. controls.Select(control => control.Store)];

    /// <summary>
    /// The most bytes a snapshot may hold, every store's together; a snapshot past it is refused,
    /// never truncated.
    /// </summary>
    public long MaxSnapshotBytes { get; }

    /// <summary>The content store, for the tests.</summary>
    internal ContentStore Content => content;

    /// <summary>The content store's object metadata index, reset, snapshotted and restored with it.</summary>
    internal ObjectMetadataIndex MetadataIndex { get; }

    /// <summary>The mail store, for the tests.</summary>
    internal MailboxStore Mail => mail;

    /// <summary>The message bus, for the tests.</summary>
    internal MessageBus.MessageBus Bus => bus;

    /// <summary>The account book, for the tests.</summary>
    internal AccountBook Accounts => accounts;

    /// <summary>
    /// Whether <c>--mail-delivered-to</c> is on: SMTP then writes a <c>Delivered-To:</c> line into each
    /// stored copy. Seeding never writes one; a host that seeds for the option reads this.
    /// </summary>
    public bool MailDeliveredToEnabled => mail.DeliveredToEnabled;

    /// <summary>
    /// Delivers <paramref name="message"/> to <paramref name="recipient"/> as SMTP would, through
    /// <see cref="MailboxStore.Seed"/>, then saves the mail store, so a store on disk holds it too.
    /// Never consults the fault plan; publishes <see cref="StoreChange.Seeded"/> for each copy.
    /// </summary>
    /// <param name="recipient">The recipient as <c>RCPT TO:</c> would give it.</param>
    /// <param name="message">The message's bytes.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>How the recipient resolved, and how the delivery went.</returns>
    /// <exception cref="IOException">The store is on disk and its index cannot be written; the
    /// message stays in memory and the next save writes it.</exception>
    public async Task<MailSeedResult> SeedMailAsync(string recipient, ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        var result = mail.Seed(recipient, message.Span);
        await mail.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// The most bytes one item a read returns may hold: 16 MiB, as seeding (ADR-0164, decision 2).
    /// </summary>
    public const long MaxReadBytes = 16_777_216;

    /// <summary>
    /// Lists an account's mailboxes with their message and unseen counts, as the IMAP server's
    /// logged-in owner sees them. Publishes nothing, asks no fault rule and changes no flag, and
    /// works on a <c>--directory</c> mail store (ADR-0164, decision 3).
    /// </summary>
    /// <param name="owner">The account name.</param>
    /// <returns><see cref="ComposedReadOutcome.Found"/> with the mailboxes in ordinal order of name, or
    /// <see cref="ComposedReadOutcome.NotFound"/> when the account owns no mailbox.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
    public ComposedReadResult<IReadOnlyList<ComposedMailboxInfo>> ListMailboxes(string owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var view = mail.ViewFor(owner);
        List<ComposedMailboxInfo> boxes = [];
        foreach (var name in mail.ListMailboxes(view))
        {
            _ = mail.ReadMailbox(view, name, out var snapshot);
            boxes.Add(new(snapshot!.Name, snapshot.Messages.Count, snapshot.Messages.Count(message => !message.Flags.HasFlag(MailFlags.Seen)), snapshot.UidValidity, snapshot.Use));
        }

        return boxes.Count == 0
            ? ComposedReadResult<IReadOnlyList<ComposedMailboxInfo>>.Failed(ComposedReadOutcome.NotFound, $"No mailboxes for {owner}.")
            : ComposedReadResult<IReadOnlyList<ComposedMailboxInfo>>.Of(boxes);
    }

    /// <summary>
    /// Lists one mailbox's messages (UID, size, flags, internal date) in UID order, one page at a time.
    /// Publishes nothing, asks no fault rule and changes no flag (ADR-0164, decisions 1 to 3).
    /// </summary>
    /// <param name="owner">The account name.</param>
    /// <param name="mailbox">The mailbox's name; <c>INBOX</c> in any case.</param>
    /// <param name="afterUid">The last UID seen; the page starts above it. 0 starts at the first.</param>
    /// <param name="limit">The most messages to return, 1 to 1000 (default 100).</param>
    /// <returns>The page; <see cref="ComposedReadOutcome.NotFound"/> for an unknown owner or mailbox;
    /// <see cref="ComposedReadOutcome.Refused"/> for a limit outside 1 to 1000.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="mailbox"/> is <see langword="null"/>.</exception>
    public ComposedReadResult<ComposedMailMessagePage> ListMessages(string owner, string mailbox, uint afterUid = 0, int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(mailbox);
        if (limit is < 1 or > 1000)
        {
            return ComposedReadResult<ComposedMailMessagePage>.Failed(ComposedReadOutcome.Refused, "The limit must be 1 to 1000.");
        }

        if (mail.ReadMailbox(mail.ViewFor(owner), mailbox, out var snapshot) != MailStoreOutcome.Succeeded)
        {
            return ComposedReadResult<ComposedMailMessagePage>.Failed(ComposedReadOutcome.NotFound, $"No mailbox {mailbox} for {owner}.");
        }

        var after = snapshot!.Messages.Where(message => message.Uid > afterUid).ToList();
        var page = after.Take(limit).Select(Summarize).ToList();
        return ComposedReadResult<ComposedMailMessagePage>.Of(new(page, after.Count > limit));
    }

    /// <summary>
    /// Reads one message's exact bytes with its summary. A message past <see cref="MaxReadBytes"/> is
    /// refused, never cut short. Publishes nothing, asks no fault rule and never sets <c>\Seen</c> or
    /// any other flag (ADR-0164, decisions 1 to 3).
    /// </summary>
    /// <param name="owner">The account name.</param>
    /// <param name="mailbox">The mailbox's name; <c>INBOX</c> in any case.</param>
    /// <param name="uid">The message's UID.</param>
    /// <returns>The message; <see cref="ComposedReadOutcome.NotFound"/> for an unknown owner, mailbox or UID;
    /// <see cref="ComposedReadOutcome.TooLarge"/> past the bound.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="mailbox"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">A message file of a <c>--directory</c> store cannot be read.</exception>
    public ComposedReadResult<ComposedMailMessage> ReadMessage(string owner, string mailbox, uint uid)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(mailbox);
        var view = mail.ViewFor(owner);
        _ = mail.ReadMailbox(view, mailbox, out var snapshot);
        var found = snapshot?.Messages.Cast<MailMessageSummary?>().FirstOrDefault(message => message!.Value.Uid == uid);
        if (found is null)
        {
            return ComposedReadResult<ComposedMailMessage>.Failed(ComposedReadOutcome.NotFound, $"No message {uid} in {mailbox} for {owner}.");
        }

        var summary = Summarize(found.Value);
        if (summary.Size > MaxReadBytes)
        {
            return ComposedReadResult<ComposedMailMessage>.Failed(ComposedReadOutcome.TooLarge, $"Message {uid} is {summary.Size} bytes; a read returns at most {MaxReadBytes}.");
        }

        _ = mail.FetchMessage(view, mailbox, uid, out var bytes);
        return ComposedReadResult<ComposedMailMessage>.Of(new(summary, bytes.ToArray()));
    }

    private static ComposedMailMessageSummary Summarize(MailMessageSummary message) =>
        new(message.Uid, message.Size, message.Flags, message.InternalDate);

    /// <summary>
    /// Writes a file into the content store as <see cref="ContentStore.SeedFileAsync"/> does.
    /// </summary>
    /// <param name="requestPath">Where the file goes, as a request path.</param>
    /// <param name="contents">The file's bytes, read to its end.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether the file was written, not permitted, or too large.</returns>
    public Task<ContentUploadResult> SeedFileAsync(string requestPath, Stream contents, CancellationToken cancellationToken) =>
        content.SeedFileAsync(requestPath, contents, cancellationToken);

    /// <summary>
    /// Reads one file's exact bytes with its size, modification time and content type. A file past
    /// <see cref="MaxReadBytes"/> is refused, never cut short. Publishes nothing, asks no fault rule and works
    /// on a <c>--directory</c> store (ADR-0164, decisions 1 to 4).
    /// </summary>
    /// <param name="requestPath">The file's path, as a request path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file; <see cref="ComposedReadOutcome.NotFound"/> for nothing, a folder, or a path under
    /// <c>/.surl</c> or hidden by the exposure options; <see cref="ComposedReadOutcome.TooLarge"/> past the bound;
    /// <see cref="ComposedReadOutcome.Refused"/> for a path a request may not name, such as one with <c>..</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="requestPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public async Task<ComposedReadResult<ComposedFileContents>> ReadFileAsync(string requestPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestPath);
        var mapping = content.MapRequestPathForTheTest(requestPath);
        if (!mapping.IsMapped)
        {
            return ComposedReadResult<ComposedFileContents>.Failed(ComposedReadOutcome.Refused, $"The path is not one a request may name ({mapping.Refusal}).");
        }

        var status = content.GetFileStatus(mapping);
        if (status is null)
        {
            return ComposedReadResult<ComposedFileContents>.Failed(ComposedReadOutcome.NotFound, $"No file at {requestPath}.");
        }

        if (status.Length > MaxReadBytes)
        {
            return ComposedReadResult<ComposedFileContents>.Failed(ComposedReadOutcome.TooLarge, $"The file is {status.Length} bytes; a read returns at most {MaxReadBytes}.");
        }

        using var buffer = new MemoryStream();
        _ = await content.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(status.Length), buffer, cancellationToken);
        return ComposedReadResult<ComposedFileContents>.Of(new(buffer.ToArray(), status.Length, status.LastModifiedUtc, ContentMediaTypes.Of(requestPath)));
    }

    /// <summary>
    /// Lists a folder's children in ordinal order of name, whatever the listing switch says. Publishes nothing and
    /// asks no fault rule (ADR-0164, decisions 1 to 4).
    /// </summary>
    /// <param name="requestPath">The folder's path, as a request path; <c>/</c> for the root.</param>
    /// <param name="afterName">The last name seen, or <see langword="null"/> to start at the first.</param>
    /// <param name="limit">The most children to return, 1 to 1000.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The page; <see cref="ComposedReadOutcome.NotFound"/> for nothing, a file, or a path under
    /// <c>/.surl</c>; <see cref="ComposedReadOutcome.Refused"/> for a limit outside 1 to 1000 or a path a request
    /// may not name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="requestPath"/> is <see langword="null"/>.</exception>
    public ComposedReadResult<ComposedFolderPage> ListFolder(string requestPath, string? afterName, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestPath);
        if (limit is < 1 or > 1000)
        {
            return ComposedReadResult<ComposedFolderPage>.Failed(ComposedReadOutcome.Refused, "The limit must be 1 to 1000.");
        }

        var mapping = content.MapRequestPathForTheTest(requestPath);
        if (!mapping.IsMapped)
        {
            return ComposedReadResult<ComposedFolderPage>.Failed(ComposedReadOutcome.Refused, $"The path is not one a request may name ({mapping.Refusal}).");
        }

        var listing = content.ListDirectoryWhateverTheListingSwitchSays(mapping, cancellationToken);
        if (!listing.IsListed)
        {
            return ComposedReadResult<ComposedFolderPage>.Failed(ComposedReadOutcome.NotFound, $"No folder at {requestPath}.");
        }

        var after = listing.Entries
            .Where(entry => afterName is null || string.CompareOrdinal(entry.Name, afterName) > 0)
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();
        var page = after.Take(limit)
            .Select(entry => new ComposedFolderEntry(entry.Name, entry.Kind == ContentEntryKind.Directory, entry.Length))
            .ToList();
        return ComposedReadResult<ComposedFolderPage>.Of(new(page, after.Count > limit));
    }

    /// <summary>
    /// Deletes a file from the content store as <see cref="ContentStore.DeleteFileForTheTest"/> does:
    /// never consults the fault plan; publishes <see cref="StoreChange.Deleted"/>.
    /// </summary>
    /// <param name="requestPath">The file's path, as a request path.</param>
    /// <returns>Done, or Absent when no file is there.</returns>
    /// <exception cref="ArgumentException"><paramref name="requestPath"/> is a path a request may not name.</exception>
    public Task<ContentChangeResult> DeleteFileAsync(string requestPath) =>
        Task.FromResult(content.DeleteFileForTheTest(requestPath));

    /// <summary>
    /// Creates a folder in the content store as <see cref="ContentStore.SeedFolder"/> does.
    /// </summary>
    /// <param name="requestPath">Where the folder goes, as a request path.</param>
    /// <returns>Whether the folder was made, was there already, or is not permitted.</returns>
    public ContentChangeResult SeedFolder(string requestPath) => content.SeedFolder(requestPath);

    /// <summary>
    /// Publishes <paramref name="message"/> on the message bus as <see cref="MessageBus.MessageBus.SeedPublish"/>
    /// does, then saves the bus, so a bus on disk holds it too.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The publish's outcome.</returns>
    /// <exception cref="IOException">The bus is on disk and a file cannot be written; the message
    /// stays in memory and the next save writes it.</exception>
    public async Task<BusPublishResult> SeedPublishAsync(BusMessage message, CancellationToken cancellationToken)
    {
        var result = bus.SeedPublish(message);
        await SaveBusAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Puts <paramref name="message"/> into the named queue as <see cref="MessageBus.MessageBus.SeedEnqueue"/>
    /// does, then saves the bus, so a bus on disk holds it too.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The outcome, such as <see cref="MessageBusOutcome.UnknownQueue"/>.</returns>
    /// <exception cref="IOException">The bus is on disk and a file cannot be written.</exception>
    public async Task<MessageBusOutcome> SeedEnqueueAsync(string queueName, BusMessage message, CancellationToken cancellationToken)
    {
        var outcome = bus.SeedEnqueue(queueName, message);
        await SaveBusAsync(cancellationToken);
        return outcome;
    }

    /// <summary>
    /// Lists a queue's ready messages in delivery order, one page at a time, as
    /// <see cref="MessageBus.MessageBus.BrowseQueue"/> does. Consumes, acknowledges and counts nothing,
    /// publishes nothing, asks no fault rule, and works on a <c>--directory</c> bus (ADR-0164, decisions 1 to 3).
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="afterPosition">The position of the last message seen; -1 starts at the first.</param>
    /// <param name="limit">The most messages to return, 1 to 1000 (default 100).</param>
    /// <returns>The page; <see cref="ComposedReadOutcome.NotFound"/> for an unknown queue;
    /// <see cref="ComposedReadOutcome.Refused"/> for a limit outside 1 to 1000 or a position below -1.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> is <see langword="null"/>.</exception>
    public ComposedReadResult<ComposedQueuePage> BrowseQueue(string queueName, int afterPosition = -1, int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        if (limit is < 1 or > 1000 || afterPosition < -1)
        {
            return ComposedReadResult<ComposedQueuePage>.Failed(ComposedReadOutcome.Refused, "The limit must be 1 to 1000 and the position at least -1.");
        }

        var browsed = bus.BrowseQueue(queueName, afterPosition, limit);
        return browsed.Outcome == MessageBusOutcome.Succeeded
            ? ComposedReadResult<ComposedQueuePage>.Of(new(browsed.Messages, browsed.ReadyCount, browsed.InFlightCount, browsed.HasMore))
            : ComposedReadResult<ComposedQueuePage>.Failed(ComposedReadOutcome.NotFound, $"No queue {queueName}.");
    }

    /// <summary>
    /// Reads a topic's retained message without dropping it, as <see cref="MessageBus.MessageBus.GetRetainedMessage"/>
    /// does. Publishes nothing and asks no fault rule (ADR-0164, decisions 1 to 3).
    /// </summary>
    /// <param name="topic">The canonical topic name.</param>
    /// <returns>The message; <see cref="ComposedReadOutcome.NotFound"/> when the topic has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="topic"/> is <see langword="null"/>.</exception>
    public ComposedReadResult<BusMessage> ReadRetainedMessage(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        var retained = bus.GetRetainedMessage(topic);
        return retained is null
            ? ComposedReadResult<BusMessage>.Failed(ComposedReadOutcome.NotFound, $"No retained message on {topic}.")
            : ComposedReadResult<BusMessage>.Of(retained);
    }

    /// <summary>
    /// Reads a topic log's records from an offset, as <see cref="MessageBus.MessageBus.ReadLog"/> does.
    /// Moves no subscription's or group's offset, publishes nothing and asks no fault rule (ADR-0164, decisions 1 to 3).
    /// </summary>
    /// <param name="topic">The canonical topic name.</param>
    /// <param name="fromOffset">The offset of the first record to read; 0 for the oldest the log still holds.</param>
    /// <param name="limit">The most records to return, 1 to 1000 (default 100).</param>
    /// <returns>The page; <see cref="ComposedReadOutcome.NotFound"/> for a topic with no log;
    /// <see cref="ComposedReadOutcome.Refused"/> for a limit outside 1 to 1000 or an offset the log does not hold.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="topic"/> is <see langword="null"/>.</exception>
    public ComposedReadResult<ComposedLogPage> ReadLog(string topic, long fromOffset = 0, int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(topic);
        if (limit is < 1 or > 1000)
        {
            return ComposedReadResult<ComposedLogPage>.Failed(ComposedReadOutcome.Refused, "The limit must be 1 to 1000.");
        }

        if (!bus.GetLogTopics().Contains(topic))
        {
            return ComposedReadResult<ComposedLogPage>.Failed(ComposedReadOutcome.NotFound, $"No log for topic {topic}.");
        }

        var read = bus.ReadLog(topic, fromOffset, MaxReadBytes);
        if (read.Outcome != MessageBusOutcome.Succeeded)
        {
            return ComposedReadResult<ComposedLogPage>.Failed(ComposedReadOutcome.Refused, $"Offset {fromOffset} is outside the log, which holds {read.LogStartOffset} to {read.HighWatermark}.");
        }

        var records = read.Records.Take(limit).ToList();
        var next = fromOffset + records.Count;
        return ComposedReadResult<ComposedLogPage>.Of(new(records, next, read.HighWatermark, next < read.HighWatermark));
    }

    /// <summary>
    /// Reads one live Redis key back with its string value, as <see cref="RedisKeyspace.ReadKey"/> does. Publishes
    /// nothing, asks no fault rule and removes no expired key (ADR-0164, decisions 1 to 3 and 9).
    /// </summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="key">The key's bytes.</param>
    /// <returns><see cref="ComposedReadOutcome.Found"/> with the key (its value is <see langword="null"/> for a
    /// type other than string, which is listed and not read); <see cref="ComposedReadOutcome.NotFound"/> for an
    /// absent or expired key or an instance with no Redis server; <see cref="ComposedReadOutcome.TooLarge"/> for a
    /// string over <see cref="MaxReadBytes"/>; <see cref="ComposedReadOutcome.Refused"/> for a database outside 0 to 15.</returns>
    public ComposedReadResult<RedisKeyContents> ReadKey(int database, ReadOnlyMemory<byte> key)
    {
        if (database is < 0 or >= 16)
        {
            return ComposedReadResult<RedisKeyContents>.Failed(ComposedReadOutcome.Refused, "The database must be 0 to 15.");
        }

        var read = redisKeyspace?.ReadKey(database, key);
        if (read is null)
        {
            return ComposedReadResult<RedisKeyContents>.Failed(ComposedReadOutcome.NotFound, "No such key.");
        }

        return read.Info.ByteCount > MaxReadBytes
            ? ComposedReadResult<RedisKeyContents>.Failed(ComposedReadOutcome.TooLarge, $"The value holds {read.Info.ByteCount} bytes, more than the {MaxReadBytes} a read returns.")
            : ComposedReadResult<RedisKeyContents>.Of(read);
    }

    /// <summary>
    /// Lists live Redis keys matching a <c>KEYS</c> glob in ordinal order of their bytes, one page at a time, as
    /// <see cref="RedisKeyspace.ListKeys"/> does. Publishes nothing and asks no fault rule; no value is read
    /// (ADR-0164, decisions 2, 3 and 9).
    /// </summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="pattern">The glob; <c>*</c> lists every key.</param>
    /// <param name="after">The last key seen; empty starts at the first.</param>
    /// <param name="limit">The most keys to return, 1 to 1000 (default 100).</param>
    /// <returns>The page; <see cref="ComposedReadOutcome.Refused"/> for a limit outside 1 to 1000 or a database
    /// outside 0 to 15; an empty page when the instance composes no Redis server.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is <see langword="null"/>.</exception>
    public ComposedReadResult<ComposedKeyPage> ListKeys(int database, string pattern, ReadOnlyMemory<byte> after = default, int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (limit is < 1 or > 1000 || database is < 0 or >= 16)
        {
            return ComposedReadResult<ComposedKeyPage>.Failed(ComposedReadOutcome.Refused, "The limit must be 1 to 1000 and the database 0 to 15.");
        }

        var page = redisKeyspace?.ListKeys(database, pattern, after, limit);
        return ComposedReadResult<ComposedKeyPage>.Of(page is null ? new([], false) : new(page.Keys, page.HasMore));
    }

    /// <summary>
    /// Writes one string key into the Redis keyspace as <see cref="RedisKeyspace.SeedKey"/> does: never consults the
    /// fault plan; publishes <see cref="StoreChange.Seeded"/> (ADR-0158 decision 3).
    /// </summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="key">The key, at least 1 byte.</param>
    /// <param name="value">The value.</param>
    /// <param name="expiry">How long the key lives, or <see langword="null"/> for no expiry.</param>
    /// <param name="cancellationToken">Cancels the seed before it starts.</param>
    /// <returns>A task that completes once the key is written.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="database"/> or <paramref name="expiry"/> is out of range.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The key and value are too large, the keyspace is full, or this
    /// instance composes no Redis server.</exception>
    public Task SeedKeyAsync(int database, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, TimeSpan? expiry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keyspace = redisKeyspace ?? throw new InvalidOperationException("This instance holds no Redis keyspace.");
        keyspace.SeedKey(database, key, value, expiry);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds one zone to the DNS zones as <see cref="DnsZoneSet.SeedZone"/> does: never consults the fault plan;
    /// publishes <see cref="StoreChange.Seeded"/> named for the apex (ADR-0158 decision 3). A reset drops it.
    /// </summary>
    /// <param name="apex">The zone's apex, a domain name with or without its final dot.</param>
    /// <param name="zoneFileText">The zone file's text, at most <see cref="DnsZoneSet.MaxSeedTextBytes"/> bytes.</param>
    /// <param name="cancellationToken">Cancels the seed before it starts.</param>
    /// <returns>A task that completes once the zone is added.</returns>
    /// <exception cref="DnsZoneFileException">The text cannot be a zone, a zone with the apex is held, or the bounds would be passed.</exception>
    /// <exception cref="InvalidOperationException">This instance composes no DNS server.</exception>
    public Task SeedZoneAsync(string apex, string zoneFileText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var zones = dnsZones ?? throw new InvalidOperationException("This instance holds no DNS zones.");
        zones.SeedZone(apex, zoneFileText);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads the LDAP directory back as LDIF, as <see cref="LdapProtocolServer.ExportDirectoryLdif"/> does, every
    /// <c>userPassword</c> value omitted. Publishes nothing and asks no fault rule (ADR-0164, decisions 2, 3 and 9).
    /// </summary>
    /// <returns><see cref="ComposedReadOutcome.Found"/> with the LDIF text;
    /// <see cref="ComposedReadOutcome.NotFound"/> for an instance with no LDAP directory;
    /// <see cref="ComposedReadOutcome.TooLarge"/> when the text is over <see cref="MaxReadBytes"/> bytes.</returns>
    public ComposedReadResult<string> ReadDirectoryLdif()
    {
        if (ldapServer is null)
        {
            return ComposedReadResult<string>.Failed(ComposedReadOutcome.NotFound, "This instance holds no LDAP directory.");
        }

        var ldif = ldapServer.ExportDirectoryLdif();
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(ldif);
        return byteCount > MaxReadBytes
            ? ComposedReadResult<string>.Failed(ComposedReadOutcome.TooLarge, $"The directory holds {byteCount} bytes of LDIF, more than the {MaxReadBytes} a read returns.")
            : ComposedReadResult<string>.Of(ldif);
    }

    /// <summary>
    /// Lists the apexes of the DNS zones held, in the order they were added, each with its final dot
    /// (<c>example.com.</c>). Publishes nothing and asks no fault rule (ADR-0164, decisions 2, 3 and 9).
    /// </summary>
    /// <returns>The apexes; none for an instance with no DNS zones.</returns>
    public IReadOnlyList<string> ListZoneApexes() =>
        dnsZones is null ? [] : [.. dnsZones.Apexes.Select(apex => apex.ToString())];

    /// <summary>
    /// Reads one DNS zone back as master-file text, as <see cref="DnsZoneSet.ExportZone"/> does. Publishes nothing and
    /// asks no fault rule (ADR-0164, decisions 2, 3 and 9).
    /// </summary>
    /// <param name="apex">The zone's apex, with or without its final dot.</param>
    /// <returns><see cref="ComposedReadOutcome.Found"/> with the zone-file text;
    /// <see cref="ComposedReadOutcome.NotFound"/> when no zone has the apex or the instance holds no DNS zones;
    /// <see cref="ComposedReadOutcome.TooLarge"/> when the text is over <see cref="MaxReadBytes"/> bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="apex"/> is <see langword="null"/>.</exception>
    public ComposedReadResult<string> ReadZone(string apex)
    {
        ArgumentNullException.ThrowIfNull(apex);
        var text = dnsZones?.ExportZone(apex);
        if (text is null)
        {
            return ComposedReadResult<string>.Failed(ComposedReadOutcome.NotFound, "No such zone.");
        }

        var byteCount = System.Text.Encoding.UTF8.GetByteCount(text);
        return byteCount > MaxReadBytes
            ? ComposedReadResult<string>.Failed(ComposedReadOutcome.TooLarge, $"The zone holds {byteCount} bytes of text, more than the {MaxReadBytes} a read returns.")
            : ComposedReadResult<string>.Of(text);
    }

    /// <summary>
    /// Adds LDIF content records to the LDAP directory, all or none, as
    /// <see cref="LdapProtocolServer.SeedDirectoryEntries"/> does: never consults the fault plan; publishes
    /// <see cref="StoreChange.Seeded"/> for each entry (ADR-0158 decision 3).
    /// </summary>
    /// <param name="ldif">LDIF content records (RFC 2849), at most 16 MiB.</param>
    /// <param name="cancellationToken">Cancels the seed before it starts.</param>
    /// <returns>A task that completes once the entries are added.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ldif"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The text is not LDIF content the directory can add, or this
    /// instance composes no LDAP directory.</exception>
    public Task SeedEntriesAsync(string ldif, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var server = ldapServer ?? throw new InvalidOperationException("This instance holds no LDAP directory.");
        server.SeedDirectoryEntries(ldif);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds an account while the server runs, as <see cref="AccountBook.Add"/> does: every scheme
    /// it may log in on judges logins with it from now on.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <remarks>
    /// An account that may log in on a mail scheme and has a name also gets its mail-store owner with an
    /// empty <c>INBOX</c>, or has its retired owner reached again (ADR-0185).
    /// </remarks>
    /// <exception cref="ArgumentException">An account of that user name exists.</exception>
    public void AddAccount(Account account)
    {
        accounts.Add(account);
        if (Account.MailSchemes.Any(account.MayLogInOn))
        {
            mail.AddOwner(account.UserName);
        }
    }

    /// <summary>
    /// Gives an account a new password while the server runs, as <see cref="AccountBook.SetPassword"/> does:
    /// <c>AccountRemoved</c> then <c>AccountAdded</c> are published, the mail-store owner is untouched
    /// (ADR-0132 amendment 1).
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The new password, never empty.</param>
    /// <returns>Whether an account had that name; when none did, nothing changed.</returns>
    public bool SetAccountPassword(string userName, string password)
    {
        if (!accounts.UserNames.Contains(userName))
        {
            return false;
        }

        accounts.SetPassword(userName, password);

        return true;
    }

    /// <summary>
    /// Removes an account while the server runs, as <see cref="AccountBook.Remove"/> does.
    /// </summary>
    /// <remarks>
    /// The account's mail-store owner is retired, keeping its mail unreached (ADR-0185 decision 2).
    /// </remarks>
    /// <param name="userName">The user name.</param>
    /// <returns>Whether an account had that name.</returns>
    public bool RemoveAccount(string userName)
    {
        var removed = accounts.Remove(userName);
        if (removed)
        {
            mail.RetireOwner(userName);
        }

        return removed;
    }

    /// <summary>Whether any account is left, so a login is required (no account: the server needs no login).</summary>
    public bool HasAccounts => accounts.HasAccounts;

    /// <summary>Whether any proxy account is left, so a proxy login is required; false too when no proxy account store is composed.</summary>
    public bool HasProxyAccounts => proxyAccounts?.HasAccounts ?? false;

    /// <summary>
    /// Returns every store to its configured state, one at a time in <see cref="StoreOrder"/>.
    /// Each publishes one <see cref="StoreChange.Reset"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for the content store's index lock.</param>
    /// <returns>A task that completes once every store is reset.</returns>
    /// <exception cref="ComposedStoreException">A store refused, such as one on disk; it names the
    /// store, and the stores before it are reset.</exception>
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        foreach (var control in controls)
        {
            await ResetAsync(control, cancellationToken);
        }
    }

    /// <summary>
    /// Returns one store to its configured state, publishing one <see cref="StoreChange.Reset"/>.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="cancellationToken">Cancels the wait for the content store's index lock.</param>
    /// <returns>A task that completes once the store is reset.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="store"/> names no store.</exception>
    /// <exception cref="ComposedStoreException">The store refused, such as one on disk.</exception>
    public Task ResetAsync(StoreKind store, CancellationToken cancellationToken) =>
        ResetAsync(ControlOf(store), cancellationToken);

    /// <summary>
    /// Takes a snapshot of every store, in <see cref="StoreOrder"/>. Changes nothing and publishes
    /// nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for the content store's index lock.</param>
    /// <returns>The snapshot, which only this instance restores.</returns>
    /// <exception cref="ComposedStoreException">A store refused, such as one on disk, or the
    /// snapshot would pass <see cref="MaxSnapshotBytes"/> at that store.</exception>
    public Task<ComposedSnapshot> TakeSnapshotAsync(CancellationToken cancellationToken) =>
        TakeSnapshotAsync(controls, cancellationToken);

    /// <summary>
    /// Takes a snapshot of one store. Changes nothing and publishes nothing.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="cancellationToken">Cancels the wait for the content store's index lock.</param>
    /// <returns>The snapshot, holding that store alone, which only this instance restores.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="store"/> names no store.</exception>
    /// <exception cref="ComposedStoreException">The store refused, or the snapshot would pass
    /// <see cref="MaxSnapshotBytes"/>.</exception>
    public Task<ComposedSnapshot> TakeSnapshotAsync(StoreKind store, CancellationToken cancellationToken) =>
        TakeSnapshotAsync([ControlOf(store)], cancellationToken);

    /// <summary>
    /// Replaces the contents of each store <paramref name="snapshot"/> holds with the snapshot's,
    /// one at a time in <see cref="StoreOrder"/>. Each publishes one <see cref="StoreChange.Restored"/>;
    /// every mailbox gets a new <c>UIDVALIDITY</c>.
    /// </summary>
    /// <param name="snapshot">A snapshot this instance took.</param>
    /// <param name="cancellationToken">Cancels the wait for the content store's index lock.</param>
    /// <returns>A task that completes once every store the snapshot holds is restored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="snapshot"/> was taken by another instance.</exception>
    /// <exception cref="ComposedStoreException">A store refused; it names the store, and the stores
    /// before it are restored.</exception>
    public async Task RestoreAsync(ComposedSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!ReferenceEquals(snapshot.Owner, this))
        {
            throw new ArgumentException("The snapshot was taken by another instance.", nameof(snapshot));
        }

        foreach (var taken in snapshot.Taken)
        {
            await RunNamingTheStoreAsync(taken.Store, "restored", () => taken.RestoreAsync(cancellationToken));
        }
    }

    private static Task Run(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private static async Task RunNamingTheStoreAsync(StoreKind store, string done, Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (InvalidOperationException refusal)
        {
            throw new ComposedStoreException(store, done, refusal);
        }
    }

    private static Task ResetAsync(ComposedStoreRegistration control, CancellationToken cancellationToken) =>
        RunNamingTheStoreAsync(control.Store, "reset", () => control.ResetAsync(cancellationToken));

    private ComposedStoreRegistration ControlOf(StoreKind store) =>
        controls.FirstOrDefault(control => control.Store == store)
        ?? throw new ArgumentOutOfRangeException(nameof(store), store, "The store is not one an instance holds.");

    private async Task<ComposedSnapshot> TakeSnapshotAsync(IReadOnlyList<ComposedStoreRegistration> taken, CancellationToken cancellationToken)
    {
        List<TakenSnapshot> snapshots = [];
        long byteCount = 0;
        foreach (var control in taken)
        {
            TakenSnapshot? snapshot = null;
            await RunNamingTheStoreAsync(
                control.Store,
                "snapshotted",
                async () => snapshot = await control.TakeSnapshotAsync(MaxSnapshotBytes - byteCount, cancellationToken));
            byteCount += snapshot!.ByteCount;
            if (byteCount > MaxSnapshotBytes)
            {
                throw new ComposedStoreException(
                    control.Store, $"The snapshot would hold {byteCount} bytes, more than the {MaxSnapshotBytes} it may hold.");
            }

            snapshots.Add(snapshot);
        }

        return new ComposedSnapshot(this, snapshots);
    }

    private async Task<TakenSnapshot> TakeContentSnapshotAsync(long maxBytes, CancellationToken cancellationToken)
    {
        var snapshot = await content.TakeSnapshotAsync(MetadataIndex, cancellationToken);
        return new(StoreKind.Content, snapshot.ByteCount, restoreCancellation => content.RestoreAsync(snapshot, restoreCancellation));
    }

    private Task<TakenSnapshot> TakeMailSnapshotAsync(long maxBytes, CancellationToken cancellationToken)
    {
        var snapshot = mail.TakeSnapshot();
        return Task.FromResult(new TakenSnapshot(StoreKind.Mail, snapshot.ByteCount, _ => Run(() => mail.Restore(snapshot))));
    }

    private Task<TakenSnapshot> TakeBusSnapshotAsync(long maxBytes, CancellationToken cancellationToken)
    {
        var snapshot = bus.TakeSnapshot(maxBytes);
        return Task.FromResult(new TakenSnapshot(StoreKind.Bus, snapshot.ByteCount, _ => Run(() => bus.Restore(snapshot))));
    }

    private Task<TakenSnapshot> TakeAccountSnapshotAsync(long maxBytes, CancellationToken cancellationToken)
    {
        var snapshot = accounts.TakeSnapshot();
        return Task.FromResult(new TakenSnapshot(StoreKind.Accounts, 0, _ => Run(() => accounts.Restore(snapshot))));
    }

    private async Task SaveBusAsync(CancellationToken cancellationToken)
    {
        var saved = await bus.SaveChangesAsync(cancellationToken);
        if (saved.FailureMessage is { } failure)
        {
            throw new IOException(failure);
        }
    }

}
