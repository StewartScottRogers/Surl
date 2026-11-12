using System.Runtime.CompilerServices;
using Surl.Authentication;
using Surl.Cli;
using Surl.Content;
using Surl.Control;
using Surl.Core;
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
internal sealed class ComposedControlTarget(SurlEventStream events, FaultPlan faults, ComposedStores stores) : IControlTarget
{
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
    public Task AddAccountAsync(string user, string password, CancellationToken cancellationToken)
    {
        stores.RemoveAccount(user);
        stores.AddAccount(new Account(user, password));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> RemoveAccountAsync(string user, CancellationToken cancellationToken) =>
        Task.FromResult(stores.RemoveAccount(user));

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

    internal static ControlRefusalKind RefusalKindOf(MessageBusOutcome outcome) => outcome switch
    {
        MessageBusOutcome.UnknownQueue => ControlRefusalKind.NotFound,
        MessageBusOutcome.InvalidTopic or MessageBusOutcome.InvalidQueueName or MessageBusOutcome.MessageTooLarge => ControlRefusalKind.BadRequest,
        _ => ControlRefusalKind.Conflict,
    };

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
