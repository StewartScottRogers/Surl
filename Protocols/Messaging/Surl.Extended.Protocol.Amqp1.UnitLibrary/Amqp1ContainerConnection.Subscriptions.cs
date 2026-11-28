using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Durable and shared topic subscriptions (Part 3 sections 3.5.3, 3.5.5 and 3.5.6; Part 2 section 2.6.6;
/// ADR-0146 decision 3): a bus queue bound to the topic filter that keeps what is published while no
/// link is attached, filtered at delivery.
/// </summary>
public sealed partial class Amqp1ContainerConnection
{
    // The kept subscription a client receiver's attach with no source names, if one of its name exists.
    private Amqp1KeptSubscription? FindKeptSubscription(Amqp1Attach attach)
    {
        foreach (var queueName in Amqp1KeptSubscription.QueueNamesFor(clientContainerId, attach.Name))
        {
            var inspected = busConnection.InspectQueue(queueName);
            if (inspected.Outcome == MessageBusOutcome.Succeeded
                && Amqp1KeptSubscription.FromDescription(queueName, inspected.SubscriptionDescription) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // Judges access, then finds, resumes, replaces or makes the subscription's queue and consumes from it;
    // null when the link may go on, or the error that refuses the attach.
    private Amqp1Error? OpenKeptSubscription(Amqp1Session session, Amqp1Link link, Amqp1KeptSubscription kept)
    {
        var found = busConnection.InspectQueue(kept.QueueName);
        var existing = Amqp1KeptSubscription.FromDescription(kept.QueueName, found.SubscriptionDescription);
        var refusal = Judge(kept.TopicFilter)
            ?? JudgeFound(existing)
            ?? (link.IsSubscriptionLookup ? null : MakeQueueAndConsume(session, link, kept, found));

        // A lookup makes no queue: the one a closed detach of it may delete is the one judged here.
        if (link.IsSubscriptionLookup)
        {
            link.Kept = existing;
        }

        return refusal;
    }

    // ADR-0146 decision 3.7: the Subscribe right on the topic filter, judged at every attach, before
    // anything changes; a live subscription made and ended at once is how the bus tells.
    private Amqp1Error? Judge(string topicFilter)
    {
        // GC.KeepAlive is the callback that does nothing; the probe is gone before a message could reach it.
        var probe = busConnection.Subscribe(topicFilter, GC.KeepAlive);
        probe.Subscription?.Dispose();
        return RefusalUnless(probe.Outcome);
    }

    // The subscription already under this name may be on another filter: replacing it, or being told
    // of it, needs the right on that filter too.
    private Amqp1Error? JudgeFound(Amqp1KeptSubscription? existing) =>
        existing is null ? null : Judge(existing.TopicFilter);

    private Amqp1Error? MakeQueueAndConsume(Amqp1Session session, Amqp1Link link, Amqp1KeptSubscription kept, BusQueueInspectResult found)
    {
        var refusal = ClearStaleQueue(kept, found) ?? DeclareAndBind(kept);
        if (refusal is not null)
        {
            return refusal;
        }

        var consumed = busConnection.ConsumeSubscriptionQueue(
            kept.QueueName, kept.Description, kept.TopicFilter, OfferKeptDelivery(link), MaxWaitingDeliveries, exclusive: !kept.IsShared);
        if (RefusalUnless(consumed.Outcome) is { } consumeRefusal)
        {
            return consumeRefusal;
        }

        link.Consumer = consumed.Consumer;
        link.Consumer!.Start();
        if (kept.EndsWithSession)
        {
            session.SessionSubscriptions[kept.QueueName] = kept.Description;
        }

        return null;
    }

    // ADR-0146 decision 3.3: a queue another link holds is locked; one with another description is a
    // different subscription, replaced - only while it still is the one judged and no link holds it.
    private Amqp1Error? ClearStaleQueue(Amqp1KeptSubscription kept, BusQueueInspectResult found)
    {
        if (found.Outcome == MessageBusOutcome.UnknownQueue)
        {
            return null;
        }

        var differs = found.SubscriptionDescription != kept.Description;
        if (found.Outcome != MessageBusOutcome.Succeeded || (found.ConsumerCount > 0 && (differs || !kept.IsShared)))
        {
            return SubscriptionInUse();
        }

        return differs ? RefusalUnless(busConnection.DeleteQueueIfDescribed(kept.QueueName, found.SubscriptionDescription, ifUnused: true)) : null;
    }

    private Amqp1Error? DeclareAndBind(Amqp1KeptSubscription kept)
    {
        var flags = kept.IsPersistent ? new BusQueueFlags(Durable: true) : new BusQueueFlags(Exclusive: true);
        var declared = busConnection.DeclareQueue(kept.QueueName, flags, kept.Description);
        return RefusalUnless(declared.Outcome) ?? RefusalUnless(busConnection.BindQueue(kept.QueueName, kept.TopicFilter));
    }

    // ADR-0146 decision 3.2: filters apply as a message leaves the queue, one they refuse is acknowledged
    // (the queue is the subscription's alone), and so is one the client cannot name.
    private Action<BusQueueDelivery> OfferKeptDelivery(Amqp1Link link) => delivery =>
    {
        if (!link.Filters.Accepts(delivery.Message, queueName: null)
            || bus.Names.FromCanonicalTopic(Amqp1BusNodeDirectory.Scheme, delivery.Message.Topic).Name is not { } address)
        {
            link.Consumer!.Acknowledge(delivery.DeliveryTag);
            return;
        }

        link.Offer(new(delivery.Message, address, delivery.DeliveryTag, delivery.Redelivered), int.MaxValue);
        WakePump();
    };

    // Part 2 section 2.6.6: a detach with closed true destroys the terminus. A shared subscription goes
    // with its last link only: the delete is refused while another consumer takes from it. It is refused
    // too once the queue carries another description than the link was judged on.
    private void DestroyKeptSubscription(Amqp1Session session, Amqp1Link link)
    {
        if (link.Kept is not { } kept)
        {
            return;
        }

        if (busConnection.DeleteQueueIfDescribed(kept.QueueName, kept.Description, ifUnused: true) == MessageBusOutcome.Succeeded)
        {
            session.SessionSubscriptions.Remove(kept.QueueName);
        }
    }

    // The subscriptions kept until the session ends go with it; a queue replaced since by one with
    // another description is not this session's, and stays.
    private void EndSessionSubscriptions(Amqp1Session session)
    {
        foreach (var (queueName, description) in session.SessionSubscriptions)
        {
            busConnection.DeleteQueueIfDescribed(queueName, description);
        }

        session.SessionSubscriptions.Clear();
    }

    // Null when the bus carried the step out; otherwise the error its outcome is.
    private static Amqp1Error? RefusalUnless(MessageBusOutcome outcome) => outcome == MessageBusOutcome.Succeeded ? null : KeptRefusal(outcome);

    private static Amqp1Error KeptRefusal(MessageBusOutcome outcome) => outcome switch
    {
        MessageBusOutcome.AccessDenied => NodeRefusal(Amqp1NodeOutcome.Unauthorized),
        MessageBusOutcome.TooManySubscriptions or MessageBusOutcome.TooManyQueues => NodeRefusal(Amqp1NodeOutcome.ResourceLimitExceeded),
        _ => NodeRefusal(Amqp1NodeOutcome.NotFound),
    };

    // Part 2 section 2.8.15: another link or connection holds the subscription.
    private static Amqp1Error SubscriptionInUse() => Error(Amqp1ErrorCondition.ResourceLocked, "subscription in use");
}
