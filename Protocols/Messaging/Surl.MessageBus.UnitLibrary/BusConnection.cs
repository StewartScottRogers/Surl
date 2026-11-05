namespace Surl.MessageBus;

/// <summary>
/// One server connection's place on a <see cref="MessageBus"/>, made by
/// <see cref="MessageBus.Connect"/>: it holds that connection's subscriptions, counts them
/// against <see cref="MessageBus.MaxSubscriptionsPerConnection"/>, and ends them all when
/// disposed, as the server does when its client goes.
/// </summary>
public sealed class BusConnection : IDisposable
{
    private readonly MessageBus bus;

    internal BusConnection(MessageBus bus) => this.bus = bus;

    internal int SubscriptionCount { get; set; }

    internal bool IsClosed { get; set; }

    /// <summary>
    /// Subscribes to every message later published on a topic <paramref name="filter"/> matches.
    /// </summary>
    /// <param name="filter">A canonical topic filter (<see cref="CanonicalTopicName.IsValidFilter"/>).</param>
    /// <param name="deliver">Called with each matching message, in publish order, one call at a
    /// time across the whole bus and never while the bus holds its lock. It must return quickly
    /// and not block: it hands the message to its connection's own outgoing queue. It may
    /// publish or subscribe. An exception it throws reaches the publisher whose call was
    /// delivering, and the deliveries still waiting go out with the next publish.</param>
    /// <param name="group">The subscription's fan-out group, or <see langword="null"/> for none
    /// (<see cref="BusSubscription.Group"/>).</param>
    /// <param name="includeRetained">Whether to return the retained messages the filter matches,
    /// as MQTT's subscribe does; no other protocol asks (ADR-0100, decision 3.3).</param>
    /// <returns>The outcome, the subscription when made, and the retained messages asked for.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="deliver"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public BusSubscribeResult Subscribe(string filter, Action<BusMessage> deliver, string? group = null, bool includeRetained = false)
    {
        ArgumentNullException.ThrowIfNull(deliver);
        return bus.Subscribe(this, filter, deliver, group, includeRetained);
    }

    /// <summary>
    /// Ends every subscription the connection holds; it can make no more. Disposing it again
    /// does nothing.
    /// </summary>
    public void Dispose() => bus.Disconnect(this);
}
