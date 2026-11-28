namespace Surl.MessageBus;

/// <summary>
/// Consuming, getting from, deleting, purging and excluding a publisher from a subscription queue - a
/// named queue carrying a subscription description, which the AMQP 1.0 container binds to one topic
/// filter to keep a durable or shared subscription (ADR-0146, decisions 3.2, 3.4 and 3.7, and
/// Amendments 2, 3 and 5). A subscription consume asks for <c>sub</c> on the topic filter; a client's
/// get, consume, delete or purge by name asks for <c>sub</c> on every filter the queue is bound to,
/// a get and a consume beside their <c>$queue/&lt;name&gt;</c> grant; and a delete by description
/// takes only the queue still carrying it. Such a queue takes no direct send, so none brings it a
/// message from outside its bindings.
/// </summary>
public sealed partial class MessageBus
{
    // Called with the lock held. A client's get, consume, delete or purge by name of a subscription
    // queue takes or removes what its filters copied in, so it asks for sub on every filter the queue
    // is bound to now. A queue with no description asks nothing here.
    private bool SubscriptionFiltersAllow(string? account, NamedQueue queue) =>
        queue.SubscriptionDescription is null || accessPolicy.MaySubscribeToEvery(account, queue.Filters);

    // Called with the lock held. The queue as Access finds it, for a caller about to take or purge its
    // messages: AccessDenied, with no queue, when its subscription filters do not allow the caller.
    private (MessageBusOutcome Outcome, NamedQueue? Queue) AccessMessages(BusConnection connection, string queueName)
    {
        var (outcome, queue) = Access(connection, queueName);
        return queue is not null && !SubscriptionFiltersAllow(connection.Account, queue)
            ? (MessageBusOutcome.AccessDenied, null)
            : (outcome, queue);
    }

    // Called with the lock held, on a queue whose declare or redeclare succeeded. A declare naming no
    // publisher changes nothing (ADR-0146, Amendment 3).
    private void ExcludePublisher(NamedQueue queue, string? publisherName)
    {
        if (publisherName is not null && !string.Equals(publisherName, queue.ExcludedPublisherName, StringComparison.Ordinal))
        {
            queue.ExcludedPublisherName = publisherName;
            queuesVersion++;
        }
    }

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
