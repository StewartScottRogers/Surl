using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Durable and shared topic subscriptions (Part 3 sections 3.5.3, 3.5.5 and 3.5.6; Part 2 section 2.6.6;
/// ADR-0146 decision 3 and Amendment 4): a bus queue bound to the topic filter, filtered at delivery. A
/// kept one holds what is published while no link is attached; a non-durable shared one goes with its
/// last link.
/// </summary>
public sealed partial class Amqp1ContainerConnection
{
    // The kept subscription a client receiver's attach with no source names, if one of its name exists.
    private Amqp1SubscriptionQueue? FindKeptSubscription(Amqp1Attach attach)
    {
        foreach (var queueName in Amqp1SubscriptionQueue.QueueNamesFor(clientContainerId, attach.Name))
        {
            var inspected = busConnection.InspectQueue(queueName);
            if (inspected.Outcome == MessageBusOutcome.Succeeded
                && Amqp1SubscriptionQueue.FromDescription(queueName, inspected.SubscriptionDescription) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // Judges access, then finds, resumes, replaces or makes the subscription's queue and consumes from it;
    // null when the link may go on, or the error that refuses the attach.
    private Amqp1Error? OpenSubscriptionQueue(Amqp1Session session, Amqp1Link link, Amqp1SubscriptionQueue planned)
    {
        var found = busConnection.InspectQueue(planned.QueueName);
        var existing = Amqp1SubscriptionQueue.FromDescription(planned.QueueName, found.SubscriptionDescription);
        var refusal = Judge(planned.TopicFilter)
            ?? JudgeFound(existing)
            ?? (link.IsSubscriptionLookup ? null : MakeQueueAndConsume(session, link, planned, found));

        // A lookup makes no queue: the one a closed detach of it may delete is the one judged here.
        if (link.IsSubscriptionLookup)
        {
            link.SubscriptionQueue = existing;
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
    private Amqp1Error? JudgeFound(Amqp1SubscriptionQueue? existing) =>
        existing is null ? null : Judge(existing.TopicFilter);

    private Amqp1Error? MakeQueueAndConsume(Amqp1Session session, Amqp1Link link, Amqp1SubscriptionQueue planned, BusQueueInspectResult found)
    {
        if (ClearStaleQueue(planned, found) is { } stale)
        {
            return stale;
        }

        var outcome = DeclareBindAndConsume(link, planned);

        // ADR-0146 Amendment 4 item 5: the last other link of a non-durable shared subscription may end
        // between those calls, taking the queue with it; once more makes it again.
        if (outcome == MessageBusOutcome.UnknownQueue && !planned.IsKept)
        {
            outcome = DeclareBindAndConsume(link, planned);
        }

        if (outcome != MessageBusOutcome.Succeeded)
        {
            return SubscriptionQueueRefusal(outcome);
        }

        link.Consumer!.Start();
        if (planned.EndsWithSession)
        {
            session.SessionSubscriptions[planned.QueueName] = planned.Description;
        }

        return null;
    }

    // ADR-0146 Amendment 3: with no-local the queue takes no topic publish of any AMQP 1.0 connection
    // whose open carried this connection's container-id, attached or not.
    private MessageBusOutcome DeclareBindAndConsume(Amqp1Link link, Amqp1SubscriptionQueue planned)
    {
        var excludedPublisherName = link.Filters.NoLocal ? busConnection.PublisherName : null;
        var declared = busConnection.DeclareQueue(planned.QueueName, planned.QueueFlags, planned.Description, excludedPublisherName).Outcome;
        if (declared != MessageBusOutcome.Succeeded)
        {
            return declared;
        }

        var outcome = BindAndConsume(link, planned);

        // ADR-0146 Amendment 4 item 5: an attach refused after the declare leaves no non-durable queue
        // behind. An unknown queue is already gone.
        if (outcome is not (MessageBusOutcome.Succeeded or MessageBusOutcome.UnknownQueue) && !planned.IsKept)
        {
            busConnection.DeleteQueueIfDescribed(planned.QueueName, planned.Description, ifUnused: true);
        }

        return outcome;
    }

    private MessageBusOutcome BindAndConsume(Amqp1Link link, Amqp1SubscriptionQueue planned)
    {
        var bound = busConnection.BindQueue(planned.QueueName, planned.TopicFilter);
        if (bound != MessageBusOutcome.Succeeded)
        {
            return bound;
        }

        var consumed = busConnection.ConsumeSubscriptionQueue(
            planned.QueueName, planned.Description, planned.TopicFilter, OfferSubscriptionQueueDelivery(link), MaxWaitingDeliveries, exclusive: !planned.IsShared);
        link.Consumer = consumed.Consumer;
        return consumed.Outcome;
    }

    // ADR-0146 decision 3.3: a queue another link holds is locked; one with another description is a
    // different subscription, replaced - only while it still is the one judged and no link holds it.
    private Amqp1Error? ClearStaleQueue(Amqp1SubscriptionQueue planned, BusQueueInspectResult found)
    {
        if (found.Outcome == MessageBusOutcome.UnknownQueue)
        {
            return null;
        }

        var differs = found.SubscriptionDescription != planned.Description;
        if (found.Outcome != MessageBusOutcome.Succeeded || (found.ConsumerCount > 0 && (differs || !planned.IsShared)))
        {
            return SubscriptionInUse();
        }

        return differs ? RefusalUnless(busConnection.DeleteQueueIfDescribed(planned.QueueName, found.SubscriptionDescription, ifUnused: true)) : null;
    }

    // ADR-0146 decision 3.2: filters apply as a message leaves the queue, one they refuse is acknowledged
    // (the queue is the subscription's alone), and so is one the client cannot name.
    private Action<BusQueueDelivery> OfferSubscriptionQueueDelivery(Amqp1Link link) => delivery =>
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
    // too once the queue carries another description than the link was judged on. A non-durable shared
    // one asks for no delete: the bus removes it with its last consumer (ADR-0146 Amendment 4 item 5).
    private void DestroyKeptSubscription(Amqp1Session session, Amqp1Link link)
    {
        if (link.SubscriptionQueue is not { IsKept: true } kept)
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
    private static Amqp1Error? RefusalUnless(MessageBusOutcome outcome) => outcome == MessageBusOutcome.Succeeded ? null : SubscriptionQueueRefusal(outcome);

    private static Amqp1Error SubscriptionQueueRefusal(MessageBusOutcome outcome) => outcome switch
    {
        MessageBusOutcome.AccessDenied => NodeRefusal(Amqp1NodeOutcome.Unauthorized),
        MessageBusOutcome.TooManySubscriptions or MessageBusOutcome.TooManyQueues => NodeRefusal(Amqp1NodeOutcome.ResourceLimitExceeded),
        _ => NodeRefusal(Amqp1NodeOutcome.NotFound),
    };

    // Part 2 section 2.8.15: another link or connection holds the subscription.
    private static Amqp1Error SubscriptionInUse() => Error(Amqp1ErrorCondition.ResourceLocked, "subscription in use");
}
