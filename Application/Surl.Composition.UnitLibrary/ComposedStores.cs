using Surl.Authentication;
using Surl.Content;
using Surl.MailStore;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

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
public sealed class ComposedStores
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
    private readonly IReadOnlyList<StoreControl> controls;

    internal ComposedStores(
        ContentStore content,
        ObjectMetadataIndex metadataIndex,
        MailboxStore mail,
        MessageBus.MessageBus bus,
        AccountBook accounts,
        long maxSnapshotBytes = DefaultMaxSnapshotBytes)
    {
        this.content = content;
        MetadataIndex = metadataIndex;
        this.mail = mail;
        this.bus = bus;
        this.accounts = accounts;
        MaxSnapshotBytes = maxSnapshotBytes;
        controls =
        [
            new(StoreKind.Content, cancellationToken => content.ResetAsync(metadataIndex, cancellationToken), TakeContentSnapshotAsync),
            new(StoreKind.Mail, _ => Run(mail.Reset), TakeMailSnapshotAsync),
            new(StoreKind.Bus, _ => Run(bus.Reset), TakeBusSnapshotAsync),
            new(StoreKind.Accounts, _ => Run(accounts.Reset), TakeAccountSnapshotAsync),
        ];
    }

    /// <summary>
    /// The order a whole instance is reset, snapshotted and restored in: content, mail, bus, then
    /// accounts (ADR-0124, decision 3).
    /// </summary>
    public static IReadOnlyList<StoreKind> StoreOrder { get; } = [StoreKind.Content, StoreKind.Mail, StoreKind.Bus, StoreKind.Accounts];

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
    /// Writes a file into the content store as <see cref="ContentStore.SeedFileAsync"/> does.
    /// </summary>
    /// <param name="requestPath">Where the file goes, as a request path.</param>
    /// <param name="contents">The file's bytes, read to its end.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether the file was written, not permitted, or too large.</returns>
    public Task<ContentUploadResult> SeedFileAsync(string requestPath, Stream contents, CancellationToken cancellationToken) =>
        content.SeedFileAsync(requestPath, contents, cancellationToken);

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
    /// Adds an account while the server runs, as <see cref="AccountBook.Add"/> does: every scheme
    /// it may log in on judges logins with it from now on.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <exception cref="ArgumentException">An account of that user name exists.</exception>
    public void AddAccount(Account account) => accounts.Add(account);

    /// <summary>
    /// Removes an account while the server runs, as <see cref="AccountBook.Remove"/> does.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <returns>Whether an account had that name.</returns>
    public bool RemoveAccount(string userName) => accounts.Remove(userName);

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

    private static Task ResetAsync(StoreControl control, CancellationToken cancellationToken) =>
        RunNamingTheStoreAsync(control.Store, "reset", () => control.ResetAsync(cancellationToken));

    private StoreControl ControlOf(StoreKind store) =>
        controls.FirstOrDefault(control => control.Store == store)
        ?? throw new ArgumentOutOfRangeException(nameof(store), store, "The store is not one an instance holds.");

    private async Task<ComposedSnapshot> TakeSnapshotAsync(IReadOnlyList<StoreControl> taken, CancellationToken cancellationToken)
    {
        List<TakenSnapshot> snapshots = [];
        long byteCount = 0;
        foreach (var control in taken)
        {
            TakenSnapshot? snapshot = null;
            await RunNamingTheStoreAsync(
                control.Store,
                "snapshotted",
                async () => snapshot = await control.TakeAsync(MaxSnapshotBytes - byteCount, cancellationToken));
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

    // One store's reset and snapshot, in the instance's order.
    private sealed record StoreControl(
        StoreKind Store,
        Func<CancellationToken, Task> ResetAsync,
        Func<long, CancellationToken, Task<TakenSnapshot>> TakeAsync);
}
