using Surl.Composition;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A running <see cref="SurlServer"/>'s message bus, as a test seeds it (ADR-0124, decision 3): the one bus
/// <c>mqtt</c> and the other messaging servers publish to and consume from. Thread-safe.
/// </summary>
public sealed class SurlServerMessages
{
    private readonly ComposedStores stores;

    private readonly Action throwIfStopped;

    internal SurlServerMessages(ComposedStores stores, Action throwIfStopped)
    {
        this.stores = stores;
        this.throwIfStopped = throwIfStopped;
    }

    /// <summary>
    /// Publishes <paramref name="payload"/> on <paramref name="topic"/> as a client's publish would: live
    /// subscribers receive it, and with <paramref name="retain"/> it becomes the topic's retained message, which a
    /// later subscriber receives. The fault plan is not consulted. Publishes a <see cref="StoreChangedEvent"/> with
    /// <see cref="StoreChange.Seeded"/>.
    /// </summary>
    /// <param name="topic">The topic, such as <c>sensors/1</c>.</param>
    /// <param name="payload">The message's payload.</param>
    /// <param name="retain">Whether the message is retained for later subscribers.</param>
    /// <param name="cancellationToken">Cancels saving a bus on disk.</param>
    /// <returns>A task that completes once the message is published.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="topic"/> is null.</exception>
    /// <exception cref="SurlSeedRefusedException">The bus refused the publish, such as <c>InvalidTopic</c>,
    /// <c>MessageTooLarge</c> or <c>StoreFull</c>.</exception>
    public Task PublishAsync(string topic, ReadOnlyMemory<byte> payload, bool retain = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topic);
        return ThrowIfRefusedAsync(PublishOutcomeAsync(new BusMessage(topic, payload) { Retain = retain }, cancellationToken), $"A message on {topic}");
    }

    /// <summary>
    /// Puts <paramref name="payload"/> into the queue <paramref name="queue"/>, which must already exist, for its
    /// next consumer. The fault plan is not consulted. Publishes a <see cref="StoreChangedEvent"/> with
    /// <see cref="StoreChange.Seeded"/>.
    /// </summary>
    /// <param name="queue">The queue's name.</param>
    /// <param name="payload">The message's payload.</param>
    /// <param name="cancellationToken">Cancels saving a bus on disk.</param>
    /// <returns>A task that completes once the message is queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queue"/> is null.</exception>
    /// <exception cref="SurlSeedRefusedException">The bus refused it, such as <c>UnknownQueue</c>,
    /// <c>QueueFull</c> or <c>MessageTooLarge</c>.</exception>
    public Task EnqueueAsync(string queue, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        return ThrowIfRefusedAsync(stores.SeedEnqueueAsync(queue, new BusMessage(queue, payload), cancellationToken), $"A message for queue {queue}");
    }

    private async Task<MessageBusOutcome> PublishOutcomeAsync(BusMessage message, CancellationToken cancellationToken) =>
        (await stores.SeedPublishAsync(message, cancellationToken)).Outcome;

    // Publish and enqueue share one check, so a seed the bus refuses fails alike on either.
    private static async Task ThrowIfRefusedAsync(Task<MessageBusOutcome> seeding, string what)
    {
        var outcome = await seeding;
        if (outcome != MessageBusOutcome.Succeeded)
        {
            throw new SurlSeedRefusedException(StoreKind.Bus, outcome.ToString(), what);
        }
    }

    /// <summary>
    /// Lists a queue's ready messages in delivery order, one page at a time. Consumes, acknowledges and counts
    /// nothing, publishes nothing and asks no fault rule (ADR-0164, decisions 1 to 3).
    /// </summary>
    /// <param name="queue">The queue's name.</param>
    /// <param name="afterPosition">The position of the last message seen; -1 starts at the first.</param>
    /// <param name="limit">The most messages to return, 1 to 1000.</param>
    /// <param name="cancellationToken">Not used: the read does not wait.</param>
    /// <returns>The page, with the queue's ready and in-flight counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queue"/> is null.</exception>
    /// <exception cref="SurlNotFoundException">The queue does not exist.</exception>
    /// <exception cref="SurlReadRefusedException">The limit is outside 1 to 1000 or the position is below -1.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<SurlQueuePage> BrowseQueueAsync(string queue, int afterPosition = -1, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        throwIfStopped();
        return SurlReadResults.Completed(() =>
        {
            var page = SurlReadResults.ValueOrThrow(stores.BrowseQueue(queue, afterPosition, limit));
            return new SurlQueuePage(
                [.. page.Messages.Select(browsed => new SurlQueueMessage(
                    browsed.Position,
                    browsed.Message.Payload,
                    [.. browsed.Message.Headers.Select(header => KeyValuePair.Create(header.Name, header.Value))]))],
                page.ReadyCount,
                page.InFlightCount,
                page.HasMore);
        });
    }

    /// <summary>
    /// Reads a topic's retained message without dropping it. Publishes nothing and asks no fault rule
    /// (ADR-0164, decisions 1 to 3).
    /// </summary>
    /// <param name="topic">The topic, such as <c>sensors/1</c>.</param>
    /// <param name="cancellationToken">Not used: the read does not wait.</param>
    /// <returns>The retained message, or null when the topic has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="topic"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<SurlRetainedMessage?> ReadRetainedAsync(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topic);
        throwIfStopped();
        var read = stores.ReadRetainedMessage(topic);
        return Task.FromResult(read.IsFound
            ? new SurlRetainedMessage(topic, (int)read.Value!.QualityOfService, read.Value.Payload)
            : null);
    }

    /// <summary>
    /// Reads a topic log's records from an offset. Moves no subscription's or group's offset, publishes nothing
    /// and asks no fault rule (ADR-0164, decisions 1 to 3).
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <param name="fromOffset">The offset of the first record to read; 0 for the oldest the log holds.</param>
    /// <param name="limit">The most records to return, 1 to 1000.</param>
    /// <param name="cancellationToken">Not used: the read does not wait.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="topic"/> is null.</exception>
    /// <exception cref="SurlNotFoundException">The topic has no log.</exception>
    /// <exception cref="SurlReadRefusedException">The limit is outside 1 to 1000 or the offset is not one the log holds.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<SurlLogPage> ReadLogAsync(string topic, long fromOffset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topic);
        throwIfStopped();
        return SurlReadResults.Completed(() =>
        {
            var page = SurlReadResults.ValueOrThrow(stores.ReadLog(topic, fromOffset, limit));
            return new SurlLogPage(
                [.. page.Records.Select(record => new SurlLogRecord(record.Offset, record.Message.PublishedAt, record.Message.Key, record.Message.Payload))],
                page.NextOffset,
                page.HasMore);
        });
    }
}
