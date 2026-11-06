namespace Surl.MessageBus;

/// <summary>
/// One message a named queue hands to one of its consumers (ADR-0100, decision 3.3). It stays
/// in the queue, unacknowledged, until the consumer acknowledges or rejects
/// <see cref="DeliveryTag"/>, or ends.
/// </summary>
/// <param name="QueueName">The queue it came from.</param>
/// <param name="Message">The message.</param>
/// <param name="DeliveryTag">The number that names this delivery to
/// <see cref="BusQueueConsumer.Acknowledge"/> and <see cref="BusQueueConsumer.Reject"/>: unique
/// within the queue, and new for each delivery of the same message.</param>
/// <param name="Redelivered">Whether the message was handed to a consumer before and put back
/// (AMQP 0-9-1 <c>basic.deliver</c>'s <c>redelivered</c>).</param>
public sealed record BusQueueDelivery(string QueueName, BusMessage Message, long DeliveryTag, bool Redelivered);
