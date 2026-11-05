namespace Surl.MessageBus;

/// <summary>
/// What a <see cref="MessageBus"/> publish or subscribe did. Every refusal a peer can cause is
/// one of these rather than an exception (ADR-0100, decision 3.6, after ADR-0050 decision 3);
/// each server answers it in its own protocol's words. Past a bound nothing changes.
/// </summary>
public enum MessageBusOutcome
{
    /// <summary>The publish or subscribe was carried out.</summary>
    Succeeded,

    /// <summary>The topic is not a canonical topic name (<see cref="CanonicalTopicName.IsValidTopic"/>);
    /// nothing was stored or delivered.</summary>
    InvalidTopic,

    /// <summary>The filter is not a canonical topic filter (<see cref="CanonicalTopicName.IsValidFilter"/>);
    /// no subscription was made.</summary>
    InvalidFilter,

    /// <summary>The payload is longer than <see cref="MessageBus.MaxMessageBytes"/>; nothing was
    /// stored or delivered.</summary>
    MessageTooLarge,

    /// <summary>Retaining the message would pass <see cref="MessageBus.MaxTopics"/>; nothing was
    /// stored or delivered.</summary>
    TooManyTopics,

    /// <summary>Retaining the message would pass <see cref="MessageBus.MaxRetainedPayloadBytes"/>;
    /// nothing was stored or delivered.</summary>
    StoreFull,

    /// <summary>The connection already holds <see cref="MessageBus.MaxSubscriptionsPerConnection"/>
    /// subscriptions; no subscription was made.</summary>
    TooManySubscriptions,

    /// <summary>Declaring the queue would pass <see cref="MessageBus.MaxQueues"/>; no queue was
    /// made.</summary>
    TooManyQueues,

    /// <summary>The queue already holds <see cref="MessageBus.MaxMessagesPerQueue"/> messages,
    /// unacknowledged included, or the copy would pass <see cref="MessageBus.MaxQueuedBytes"/>;
    /// that queue did not take the message, and a topic publish still reached everything else
    /// (ADR-0100, decision 3.6).</summary>
    QueueFull,

    /// <summary>The queue name is empty; no queue was made.</summary>
    InvalidQueueName,

    /// <summary>No queue of that name exists (AMQP 0-9-1 <c>404 not-found</c>).</summary>
    UnknownQueue,

    /// <summary>The queue is exclusive to another connection (AMQP 0-9-1
    /// <c>405 resource-locked</c>); nothing changed.</summary>
    QueueLocked,

    /// <summary>The queue exists with other <see cref="BusQueueFlags"/> than the declare asked
    /// for (AMQP 0-9-1 <c>406 precondition-failed</c>); the queue is unchanged.</summary>
    QueueFlagsDiffer,

    /// <summary>The consumer holds no unacknowledged delivery with that tag (AMQP 0-9-1
    /// <c>406 precondition-failed</c>, "unknown delivery tag"); nothing changed.</summary>
    UnknownDeliveryTag,
}
