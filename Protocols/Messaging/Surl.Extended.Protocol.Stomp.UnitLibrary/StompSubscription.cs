using Surl.MessageBus;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// One SUBSCRIBE of one connection (ADR-0118 decisions 4 and 5): its <c>id</c>, its ack mode, and
/// what holds it on the bus - a live topic subscription or a queue consumer.
/// </summary>
/// <param name="id">The SUBSCRIBE's <c>id</c>; in 1.0 without one, its destination.</param>
/// <param name="ackMode">How its messages are acknowledged.</param>
internal sealed class StompSubscription(string id, StompAckMode ackMode)
{
    /// <summary>The SUBSCRIBE's <c>id</c>, sent back as each MESSAGE's <c>subscription</c>.</summary>
    public string Id { get; } = id;

    /// <summary>How its messages are acknowledged.</summary>
    public StompAckMode AckMode { get; } = ackMode;

    /// <summary>Its live topic subscription, when it subscribed to a <c>/topic/</c> destination.</summary>
    public BusSubscription? Topic { get; set; }

    /// <summary>Its queue consumer, when it subscribed to a <c>/queue/</c> destination.</summary>
    public BusQueueConsumer? Queue { get; set; }

    /// <summary>Ends it on the bus; a queue consumer's unacknowledged messages go back to the queue.</summary>
    public void End()
    {
        Topic?.Dispose();
        Queue?.Dispose();
    }
}
