namespace Surl.MessageBus;

/// <summary>
/// What <see cref="MessageBus.Publish"/> did with one message.
/// </summary>
/// <param name="Outcome">Whether the message was taken, or which refusal it met.</param>
/// <param name="DeliveredSubscriptionCount">How many live subscriptions the message was handed
/// to: one per matching subscription without a group, and one per matching group. Redis's
/// <c>PUBLISH</c> reply is this number (ADR-0100, decision 3.8). Zero when refused.</param>
public sealed record BusPublishResult(MessageBusOutcome Outcome, int DeliveredSubscriptionCount);
