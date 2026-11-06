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

    internal SurlServerMessages(ComposedStores stores) => this.stores = stores;

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
}
