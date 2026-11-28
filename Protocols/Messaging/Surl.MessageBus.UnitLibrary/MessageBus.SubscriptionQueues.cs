namespace Surl.MessageBus;

/// <summary>
/// Consuming and deleting a subscription queue - a named queue carrying a subscription description and
/// bound to one topic filter, as the AMQP 1.0 container keeps a durable or shared subscription
/// (ADR-0146, decisions 3.2, 3.4 and 3.7). The right a consume asks for is <c>sub</c> on the topic
/// filter, and a delete by description takes only the queue still carrying it. Such a queue takes no
/// direct send, so none brings it a message from outside its bindings.
/// </summary>
public sealed partial class MessageBus
{
    internal MessageBusOutcome DeleteQueueIfDescribed(BusConnection connection, string queueName, string? subscriptionDescription, bool ifUnused) =>
        DeleteQueueUnlessRefused(
            connection,
            queueName,
            queue => string.Equals(queue.SubscriptionDescription, subscriptionDescription, StringComparison.Ordinal)
                ? queue.DeleteRefusal(ifUnused, ifEmpty: false)
                : MessageBusOutcome.QueueDescriptionDiffers).Outcome;

    internal BusConsumeResult ConsumeSubscriptionQueue(
        BusConnection connection, string queueName, string subscriptionDescription, string topicFilter, Action<BusQueueDelivery> deliver, int maxUnacknowledged, bool exclusive)
    {
        if (!accessPolicy.MaySubscribe(connection.Account, topicFilter))
        {
            return new(MessageBusOutcome.AccessDenied, null);
        }

        lock (stateLock)
        {
            var (outcome, queue) = Access(connection, queueName);
            if (queue is null)
            {
                return new(outcome, null);
            }

            var refusal = SubscriptionQueueRefusal(queue, subscriptionDescription, topicFilter);
            return refusal == MessageBusOutcome.Succeeded
                ? AddDeliveringConsumer(connection, queue, deliver, maxUnacknowledged, exclusive, accepts: null)
                : new(refusal, null);
        }
    }

    // The right on the filter reaches only the queue of that very subscription, bound to that filter
    // and nothing else now.
    private static MessageBusOutcome SubscriptionQueueRefusal(NamedQueue queue, string subscriptionDescription, string topicFilter) =>
        !string.Equals(queue.SubscriptionDescription, subscriptionDescription, StringComparison.Ordinal) ? MessageBusOutcome.QueueDescriptionDiffers
        : !queue.IsBoundToExactly(topicFilter) ? MessageBusOutcome.AccessDenied
        : MessageBusOutcome.Succeeded;
}
