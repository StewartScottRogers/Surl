using System.Runtime.CompilerServices;
using Surl.Authentication;
using Surl.Cli;
using Surl.Content;
using Surl.Control;
using Surl.Core;
using Surl.Extended.Protocol.DnsQuery;
using Surl.MailStore;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// What the loopback control endpoint drives on one composed instance (ADR-0124 decision 7, BL-816): its event
/// stream, its fault plan and its <see cref="ComposedStores"/>, the same surface <c>Surl.Api</c> and
/// <c>Surl.Mcp</c> drive. Each change is published by the store or plan it changes.
/// </summary>
/// <param name="events">The instance's event stream.</param>
/// <param name="faults">The instance's fault plan.</param>
/// <param name="stores">The instance's stores.</param>
/// <param name="log">The instance log, where removing the last account or proxy account is warned (BL-1477).</param>
internal sealed class ComposedControlTarget(SurlEventStream events, FaultPlan faults, ComposedStores stores, TextWriter log) : IControlTarget
{
    /// <summary>Creates a target whose warnings go nowhere.</summary>
    /// <param name="events">The instance's event stream.</param>
    /// <param name="faults">The instance's fault plan.</param>
    /// <param name="stores">The instance's stores.</param>
    public ComposedControlTarget(SurlEventStream events, FaultPlan faults, ComposedStores stores)
        : this(events, faults, stores, TextWriter.Null)
    {
    }

    // Any listen URL the parser accepts: the item is read as the command line reads --fault, so there is one grammar.
    private const string AnyListenUrl = "http://127.0.0.1:0";

    /// <inheritdoc/>
    public async IAsyncEnumerable<SurlEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var subscription = events.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });

        // Cancelling ends the subscription, so the read drains what is buffered and ends rather than throwing.
        await using var ending = cancellationToken.Register(() => _ = subscription.DisposeAsync().AsTask());
        await foreach (var surlEvent in subscription.ReadAllAsync(CancellationToken.None))
        {
            yield return surlEvent;
        }
    }

    /// <inheritdoc/>
    public string AddFault(string item)
    {
        var parsed = CommandLineParser.Parse(["--fault", item, AnyListenUrl], ExtendedSchemeDefaultPorts.Schemes);
        if (parsed.Failure is { } failure)
        {
            throw new ControlRefusalException(failure.Message);
        }

        try
        {
            return faults.Add(FaultPlanComposition.ToRule(parsed.CommandLine!.Faults[0])).Id;
        }
        catch (InvalidOperationException full)
        {
            throw new ControlRefusalException(ControlRefusalKind.Conflict, full.Message);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<ControlFault> ListFaults() =>
        [.. faults.Rules.Select(rule => new ControlFault(rule.Id, FaultItemText.Format(rule), rule.Times))];

    /// <inheritdoc/>
    public bool RemoveFault(string faultId) => faults.Remove(faultId);

    /// <inheritdoc/>
    public async Task SeedMailAsync(string recipient, ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        var result = await stores.SeedMailAsync(recipient, message, cancellationToken);
        if (result.Delivery is MailStoreOutcome.Succeeded)
        {
            return;
        }

        // Delivery is null unless the recipient was deliverable; NoSuchAccount and RelayingDenied are both a 404.
        throw result.Recipient switch
        {
            MailRecipientLookup.InvalidAddress => new ControlRefusalException($"{recipient} is not a mail address"),
            MailRecipientLookup.Deliverable => new ControlRefusalException(
                ControlRefusalKind.Conflict, $"the mail store did not deliver to {recipient}: {result.Delivery}"),
            _ => new ControlRefusalException(ControlRefusalKind.NotFound, $"no mailbox receives mail for {recipient}"),
        };
    }

    /// <inheritdoc/>
    public async Task WriteFileAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        using var contents = new MemoryStream(content.ToArray(), writable: false);
        var result = await RefusingABadPath(path, () => stores.SeedFileAsync(path, contents, cancellationToken));
        switch (result)
        {
            case ContentUploadResult.NotPermitted:
                throw new ControlRefusalException($"the content store does not permit a file at {path}");
            case ContentUploadResult.TooLarge:
                throw new ControlRefusalException(ControlRefusalKind.Conflict, $"the file at {path} is larger than the content store takes");
        }
    }

    /// <inheritdoc/>
    public Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken) =>
        RefusingABadPath(path, async () => await stores.DeleteFileAsync(path) == ContentChangeResult.Done);

    /// <inheritdoc/>
    public async Task PublishMessageAsync(ControlMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var outcome = message.Queue is { } queue
            ? await stores.SeedEnqueueAsync(queue, new BusMessage(queue, message.Payload), cancellationToken)
            : (await stores.SeedPublishAsync(new BusMessage(message.Topic!, message.Payload) { Retain = message.Retain }, cancellationToken)).Outcome;
        if (outcome != MessageBusOutcome.Succeeded)
        {
            throw new ControlRefusalException(RefusalKindOf(outcome), $"the message bus refused the message: {outcome}");
        }
    }

    /// <inheritdoc/>
    public async Task SeedKeyAsync(int database, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, TimeSpan? expiry, CancellationToken cancellationToken)
    {
        try
        {
            await stores.SeedKeyAsync(database, key, value, expiry, cancellationToken);
        }
        catch (ArgumentException refused)
        {
            throw new ControlRefusalException(refused.Message);
        }
        catch (InvalidOperationException refused)
        {
            throw new ControlRefusalException(ControlRefusalKind.Conflict, refused.Message);
        }
    }

    /// <inheritdoc/>
    public Task<string> ReadDirectoryLdifAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Found(stores.ReadDirectoryLdif()));

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListZonesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(stores.ListZoneApexes());

    /// <inheritdoc/>
    public Task<string> ReadZoneAsync(string apex, CancellationToken cancellationToken) =>
        Task.FromResult(Found(stores.ReadZone(apex)));

    /// <inheritdoc/>
    public async Task SeedDirectoryAsync(string ldif, CancellationToken cancellationToken)
    {
        try
        {
            await stores.SeedEntriesAsync(ldif, cancellationToken);
        }
        catch (InvalidOperationException refused)
        {
            throw new ControlRefusalException(refused.Message);
        }
    }

    /// <inheritdoc/>
    public async Task SeedZoneAsync(string apex, string zoneFileText, CancellationToken cancellationToken)
    {
        try
        {
            await stores.SeedZoneAsync(apex, zoneFileText, cancellationToken);
        }
        catch (DnsZoneFileException refused)
        {
            throw new ControlRefusalException(IsBoundOrDuplicate(refused) ? ControlRefusalKind.Conflict : ControlRefusalKind.BadRequest, refused.Message);
        }
        catch (InvalidOperationException refused)
        {
            throw new ControlRefusalException(ControlRefusalKind.Conflict, refused.Message);
        }
    }

    /// <inheritdoc/>
    public Task AddAccountAsync(string user, string password, CancellationToken cancellationToken)
    {
        try
        {
            stores.AddAccount(new Account(user, password));
        }
        catch (ArgumentException)
        {
            throw new ControlRefusalException(ControlRefusalKind.Conflict, $"user {user} already has an account");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> SetAccountPasswordAsync(string user, string password, CancellationToken cancellationToken) =>
        SettingAPassword(() => stores.SetAccountPassword(user, password), password);

    /// <inheritdoc/>
    public Task<bool> SetProxyAccountPasswordAsync(string user, string password, CancellationToken cancellationToken) =>
        SettingAPassword(() => stores.SetProxyAccountPassword(user, password), password);

    /// <inheritdoc/>
    public Task<bool> SetTlsSrpAccountPasswordAsync(string user, string password, CancellationToken cancellationToken) =>
        SettingAPassword(() => stores.SetTlsSrpAccountPassword(user, password), password);

    private static Task<bool> SettingAPassword(Func<bool> set, string password) =>
        password.Length == 0
            ? throw new ControlRefusalException(ControlRefusalKind.BadRequest, "the password must not be empty")
            : RefusingAnAbsentStore(set);

    /// <inheritdoc/>
    public Task<bool> RemoveAccountAsync(string user, CancellationToken cancellationToken)
    {
        var isRemoved = stores.RemoveAccount(user);
        if (isRemoved && !stores.HasAccounts)
        {
            log.WriteLine("surl: warning: no account left; the server now needs no login");
        }

        return Task.FromResult(isRemoved);
    }

    /// <inheritdoc/>
    public Task AddProxyAccountAsync(string user, string password, CancellationToken cancellationToken) =>
        RefusingAnAbsentStore(() => AddingANewName(() => stores.AddProxyAccount(new Account(user, password)), $"user {user} already has a proxy account"));

    /// <inheritdoc/>
    public Task<bool> RemoveProxyAccountAsync(string user, CancellationToken cancellationToken) =>
        RefusingAnAbsentStore(() =>
        {
            var isRemoved = stores.RemoveProxyAccount(user);
            if (isRemoved && !stores.HasProxyAccounts)
            {
                log.WriteLine("surl: warning: no proxy account left; the proxy now needs no login");
            }

            return isRemoved;
        });

    /// <inheritdoc/>
    public Task AddTlsSrpAccountAsync(string user, string password, CancellationToken cancellationToken) =>
        RefusingAnAbsentStore(() => AddingANewName(() => stores.AddTlsSrpAccount(user, password), $"user {user} already has a TLS-SRP account"));

    // A name that already has an account is a conflict and the account is kept (ADR-0132 amendment 1).
    private static bool AddingANewName(Action add, string conflictMessage)
    {
        try
        {
            add();
        }
        catch (ArgumentException)
        {
            throw new ControlRefusalException(ControlRefusalKind.Conflict, conflictMessage);
        }

        return true;
    }

    /// <inheritdoc/>
    public Task<bool> RemoveTlsSrpAccountAsync(string user, CancellationToken cancellationToken) =>
        RefusingAnAbsentStore(() => stores.RemoveTlsSrpAccount(user));

    // An account store that is not composed refuses with InvalidOperationException; that is a request the
    // instance's state cannot carry out, so it is a conflict carrying the store's own message.
    private static Task<T> RefusingAnAbsentStore<T>(Func<T> change)
    {
        try
        {
            return Task.FromResult(change());
        }
        catch (InvalidOperationException refused)
        {
            throw new ControlRefusalException(ControlRefusalKind.Conflict, refused.Message);
        }
    }

    /// <inheritdoc/>
    public Task ResetAsync(CancellationToken cancellationToken) =>
        RefusingAStoreConflict(async () =>
        {
            await stores.ResetAsync(cancellationToken);
            return true;
        });

    /// <inheritdoc/>
    public async Task<IControlSnapshot> SnapshotAsync(CancellationToken cancellationToken) =>
        await RefusingAStoreConflict(() => stores.TakeSnapshotAsync(cancellationToken));

    /// <inheritdoc/>
    public Task RestoreAsync(IControlSnapshot snapshot, CancellationToken cancellationToken) =>
        RefusingAStoreConflict(async () =>
        {
            await stores.RestoreAsync((ComposedSnapshot)snapshot, cancellationToken);
            return true;
        });

    /// <inheritdoc/>
    public Task<IReadOnlyList<ControlMailbox>> ListMailboxesAsync(string owner, CancellationToken cancellationToken)
    {
        var boxes = Found(stores.ListMailboxes(owner));

        return Task.FromResult<IReadOnlyList<ControlMailbox>>([.. boxes.Select(box => new ControlMailbox(box.Name, box.MessageCount, box.UnseenCount, box.UidValidity, box.Use))]);
    }

    /// <inheritdoc/>
    public Task<ControlMailMessagePage> ListMessagesAsync(string owner, string mailbox, uint afterUid, int limit, CancellationToken cancellationToken)
    {
        var page = Found(stores.ListMessages(owner, mailbox, afterUid, limit));

        return Task.FromResult(new ControlMailMessagePage([.. page.Messages.Select(Summarize)], page.HasMore));
    }

    /// <inheritdoc/>
    public Task<ControlMailMessage> ReadMessageAsync(string owner, string mailbox, uint uid, CancellationToken cancellationToken)
    {
        var message = Found(stores.ReadMessage(owner, mailbox, uid));

        return Task.FromResult(new ControlMailMessage(Summarize(message.Summary), message.Bytes));
    }

    /// <inheritdoc/>
    public async Task<ControlFileContents> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        var file = Found(await stores.ReadFileAsync(path, cancellationToken));

        return new ControlFileContents(file.Bytes, file.ByteCount, file.ModifiedAt, file.ContentType);
    }

    /// <inheritdoc/>
    public Task<ControlFolderPage> ListFolderAsync(string path, string? afterName, int limit, CancellationToken cancellationToken)
    {
        var page = Found(stores.ListFolder(path, afterName, limit, cancellationToken));

        return Task.FromResult(new ControlFolderPage([.. page.Entries.Select(entry => new ControlFolderEntry(entry.Name, entry.IsFolder, entry.ByteCount))], page.HasMore));
    }

    /// <inheritdoc/>
    public Task<ControlQueuePage> BrowseQueueAsync(string queue, int afterPosition, int limit, CancellationToken cancellationToken)
    {
        var page = Found(stores.BrowseQueue(queue, afterPosition, limit));
        var messages = page.Messages.Select(browsed => new ControlQueuedMessage(
            browsed.Position,
            browsed.Message.Payload,
            [.. browsed.Message.Headers.Select(header => new KeyValuePair<string, string>(header.Name, header.Value))]));

        return Task.FromResult(new ControlQueuePage([.. messages], page.ReadyCount, page.InFlightCount, page.HasMore));
    }

    /// <inheritdoc/>
    public Task<ControlRetainedMessage> ReadRetainedMessageAsync(string topic, CancellationToken cancellationToken)
    {
        var retained = Found(stores.ReadRetainedMessage(topic));

        return Task.FromResult(new ControlRetainedMessage(retained.Topic, (int)retained.QualityOfService, retained.Payload));
    }

    /// <inheritdoc/>
    public Task<ControlLogPage> ReadLogAsync(string topic, long fromOffset, int limit, CancellationToken cancellationToken)
    {
        var page = Found(stores.ReadLog(topic, fromOffset, limit));
        var records = page.Records.Select(record => new ControlLogRecord(record.Offset, record.Message.PublishedAt, record.Message.Key, record.Message.Payload));

        return Task.FromResult(new ControlLogPage([.. records], page.NextOffset, page.HasMore));
    }

    /// <inheritdoc/>
    public Task<ControlKeyContents> ReadKeyAsync(int database, ReadOnlyMemory<byte> key, CancellationToken cancellationToken)
    {
        var read = Found(stores.ReadKey(database, key));

        return Task.FromResult(new ControlKeyContents(KeyInfoOf(read.Info), read.Value));
    }

    /// <inheritdoc/>
    public Task<ControlKeyPage> ListKeysAsync(int database, string pattern, ReadOnlyMemory<byte> afterKey, int limit, CancellationToken cancellationToken)
    {
        var page = Found(stores.ListKeys(database, pattern, afterKey, limit));

        return Task.FromResult(new ControlKeyPage([.. page.Keys.Select(KeyInfoOf)], page.HasMore));
    }

    private static ControlKeyInfo KeyInfoOf(Surl.Extended.Protocol.Redis.RedisKeyInfo info) =>
        new(info.Key, info.Type, info.ByteCount, info.ExpiresAt);

    // A read that found nothing, was too large or was refused is the matching control refusal (ADR-0164 decisions 2 and 4).
    private static T Found<T>(ComposedReadResult<T> result) => result.Outcome switch
    {
        ComposedReadOutcome.Found => result.Value!,
        ComposedReadOutcome.NotFound => throw new ControlRefusalException(ControlRefusalKind.NotFound, result.Reason!),
        ComposedReadOutcome.TooLarge => throw new ControlRefusalException(ControlRefusalKind.TooLarge, result.Reason!),
        _ => throw new ControlRefusalException(ControlRefusalKind.BadRequest, result.Reason!),
    };

    private static ControlMailMessageSummary Summarize(ComposedMailMessageSummary message) =>
        new(message.Uid, message.Size, FlagNames(message.Flags), message.InternalDate);

    private static string[] FlagNames(MailFlags flags) =>
    [
        .. new[]
        {
            (MailFlags.Seen, "\\Seen"),
            (MailFlags.Answered, "\\Answered"),
            (MailFlags.Flagged, "\\Flagged"),
            (MailFlags.Deleted, "\\Deleted"),
            (MailFlags.Draft, "\\Draft"),
        }.Where(flag => flags.HasFlag(flag.Item1)).Select(flag => flag.Item2),
    ];

    internal static ControlRefusalKind RefusalKindOf(MessageBusOutcome outcome) => outcome switch
    {
        MessageBusOutcome.UnknownQueue => ControlRefusalKind.NotFound,
        MessageBusOutcome.InvalidTopic or MessageBusOutcome.InvalidQueueName or MessageBusOutcome.MessageTooLarge => ControlRefusalKind.BadRequest,
        _ => ControlRefusalKind.Conflict,
    };

    // The zone set's two refusals that are not about the text: a bound passed, or an apex already held (ADR-0158 decision 9).
    private static bool IsBoundOrDuplicate(DnsZoneFileException refused) =>
        refused.Fault.StartsWith("past the zones' bounds", StringComparison.Ordinal)
        || refused.Fault.StartsWith("a second zone", StringComparison.Ordinal);

    // A path a request may not name, such as one with a .. segment, is the client's mistake.
    private static async Task<T> RefusingABadPath<T>(string path, Func<Task<T>> change)
    {
        try
        {
            return await change();
        }
        catch (ArgumentException)
        {
            throw new ControlRefusalException($"{path} is not a path a request may name");
        }
    }

    // A store on disk asked to reset, snapshot or restore, or a snapshot past its bound, is 409 (ADR-0124 decision 7).
    private static async Task<T> RefusingAStoreConflict<T>(Func<Task<T>> control)
    {
        try
        {
            return await control();
        }
        catch (ComposedStoreException refused)
        {
            throw new ControlRefusalException(ControlRefusalKind.Conflict, refused.Message);
        }
    }
}
