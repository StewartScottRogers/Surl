namespace Surl.MessageBus;

/// <summary>
/// What <see cref="MessageBus.Publish"/> did with one message.
/// </summary>
/// <param name="Outcome">Whether the message was taken, or which refusal it met.</param>
/// <param name="DeliveredSubscriptionCount">How many live subscriptions the message was handed
/// to: one per matching subscription without a group, and one per matching group. Redis's
/// <c>PUBLISH</c> reply is this number (ADR-0100, decision 3.8). Zero when refused.</param>
public sealed record BusPublishResult(MessageBusOutcome Outcome, int DeliveredSubscriptionCount)
{
    /// <summary>
    /// How many named queues bound to the topic took a copy of the message.
    /// </summary>
    public int QueuedCount { get; init; }

    /// <summary>
    /// How many named queues bound to the topic refused the copy as
    /// <see cref="MessageBusOutcome.QueueFull"/>; the publish still reached everything else, so
    /// <see cref="Outcome"/> stays <see cref="MessageBusOutcome.Succeeded"/> (ADR-0100, decision 3.6).
    /// </summary>
    public int FullQueueCount { get; init; }
}
