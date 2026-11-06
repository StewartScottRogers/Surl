using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// One Surl instance's event stream (ADR-0124, decision 1): stamps each published event with the
/// next <see cref="SurlEvent.Sequence"/> and the time from its <see cref="TimeProvider"/>, and
/// hands it to every <see cref="SurlEventSubscription"/>, so every subscriber sees one total order.
/// Publishing never blocks and never waits for a subscriber. Nothing is static: two streams never
/// see each other's events.
/// </summary>
/// <param name="timeProvider">The instance's clock, which stamps <see cref="SurlEvent.At"/>.</param>
public sealed class SurlEventStream(TimeProvider timeProvider) : ISurlEventPublisher
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly Lock publishLock = new();
    private SurlEventSubscription[] subscriptions = [];
    private long lastSequence;
    private int bytesSubscriptionCount;

    /// <inheritdoc/>
    /// <remarks><see langword="true"/> while at least one subscription asked for bytes.</remarks>
    public bool IsPublishingBytes => Volatile.Read(ref bytesSubscriptionCount) > 0;

    /// <summary>
    /// The <see cref="SurlEvent.Sequence"/> of the last event published; 0 before the first.
    /// </summary>
    public long LastSequence
    {
        get
        {
            lock (publishLock)
            {
                return lastSequence;
            }
        }
    }

    /// <summary>
    /// Subscribes to the events published from now on; there is no replay.
    /// </summary>
    /// <param name="options">The subscription's capacity, whether it wants bytes, and its filter.</param>
    /// <returns>The subscription; dispose it to stop delivery.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="SurlEventSubscriptionOptions.Capacity"/> is outside
    /// <see cref="SurlEventSubscriptionOptions.MinCapacity"/> to <see cref="SurlEventSubscriptionOptions.MaxCapacity"/>.
    /// </exception>
    public SurlEventSubscription Subscribe(SurlEventSubscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Capacity, SurlEventSubscriptionOptions.MinCapacity, nameof(options));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Capacity, SurlEventSubscriptionOptions.MaxCapacity, nameof(options));

        var subscription = new SurlEventSubscription(options, Unsubscribe);

        lock (publishLock)
        {
            subscriptions = [.. subscriptions, subscription];
            CountBytesSubscription(subscription, 1);
        }

        return subscription;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="surlEvent"/> is <see langword="null"/>.</exception>
    public void Publish(SurlEvent surlEvent)
    {
        ArgumentNullException.ThrowIfNull(surlEvent);

        lock (publishLock)
        {
            var stamped = surlEvent with { Sequence = ++lastSequence, At = timeProvider.GetUtcNow() };

            foreach (var subscription in subscriptions)
            {
                subscription.Offer(stamped);
            }
        }
    }

    private void Unsubscribe(SurlEventSubscription subscription)
    {
        lock (publishLock)
        {
            var remaining = Array.FindAll(subscriptions, kept => !ReferenceEquals(kept, subscription));

            if (remaining.Length != subscriptions.Length)
            {
                subscriptions = remaining;
                CountBytesSubscription(subscription, -1);
            }
        }
    }

    private void CountBytesSubscription(SurlEventSubscription subscription, int change)
    {
        if (subscription.IncludesBytes)
        {
            Interlocked.Add(ref bytesSubscriptionCount, change);
        }
    }
}
