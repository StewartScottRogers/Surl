using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Links: <c>attach</c> and <c>detach</c>, the handles, and the nodes they attach to (Part 2 sections
/// 2.6.3, 2.6.4, 2.7.3 and 2.7.7; Part 3 section 3.5; ADR-0107 decisions 4 and 5).
/// </summary>
public sealed partial class Amqp1ContainerConnection
{
    /// <summary>The most deliveries waiting on, or unsettled on, a sending link (ADR-0107 decision 7).</summary>
    public const int MaxWaitingDeliveries = 1000;

    private const byte SettledSenderSettleMode = 1;

    private const byte SecondReceiverSettleMode = 1;

    // The bus refusing a subscription or consume to this caller, whether or not the node exists.
    private static readonly HashSet<MessageBusOutcome> UnauthorizedSourceOutcomes =
        [MessageBusOutcome.AccessDenied, MessageBusOutcome.QueueLocked, MessageBusOutcome.ExclusiveConsumeRefused];

    private async ValueTask<bool> AnswerAttachAsync(Amqp1Session session, Amqp1Attach attach)
    {
        // Part 2 section 2.7.3 forbids a handle above handle-max and names no other condition for it.
        if (attach.Handle > ContainerHandleMax || session.LinksByClientHandle.ContainsKey(attach.Handle))
        {
            return await EndSessionWithErrorAsync(session, Error(Amqp1ErrorCondition.HandleInUse, "handle in use"));
        }

        if (session.LowestFreeContainerHandle(ContainerHandleMax) is not { } containerHandle)
        {
            return await EndSessionWithErrorAsync(session, Error(Amqp1ErrorCondition.ResourceLimitExceeded, "no handle free within handle-max"));
        }

        var link = new Amqp1Link(containerHandle)
        {
            ContainerReceives = !attach.IsReceiver,
            SenderSettles = attach.SenderSettleMode == SettledSenderSettleMode,
            ClientSettlesSecond = attach.ReceiverSettleMode == SecondReceiverSettleMode,
            DeliveryCount = attach.IsReceiver ? 0 : attach.InitialDeliveryCount ?? 0,
        };
        session.LinksByClientHandle.Add(attach.Handle, link);
        await AttachLinkAsync(session, link, attach);
        return true;
    }

    private async ValueTask AttachLinkAsync(Amqp1Session session, Amqp1Link link, Amqp1Attach attach)
    {
        var (nodeTerminus, refusal) = ResolveNode(attach, link);
        refusal ??= OpenNode(link);
        await WriteAsync(session.OutgoingChannel, AnsweringAttach(attach, link.ContainerHandle, refusal is null ? nodeTerminus : null));
        if (refusal is not null)
        {
            // Part 2 section 2.6.3: a null terminus, then at once a closing detach with the error.
            link.IsDetachedByContainer = true;
            link.EndSource();
            await WriteAsync(session.OutgoingChannel, new Amqp1Detach(link.ContainerHandle) { Closed = true, Error = refusal });
        }
        else if (link.ContainerReceives)
        {
            // ADR-0107 decision 5: credit 100 at once.
            link.DeliveryLimit = link.DeliveryCount + LinkCredit;
            await WriteLinkFlowAsync(session, link);
        }
    }

    // The container's terminus for the node the client's source (a client receiver) or target
    // (a client sender) names; or null and the error that refuses the attach.
    private (Amqp1Described? NodeTerminus, Amqp1Error? Refusal) ResolveNode(Amqp1Attach attach, Amqp1Link link)
    {
        var terminus = Amqp1Terminus.Read(attach.IsReceiver ? attach.Source : attach.Target);
        if (terminus.IsCoordinator)
        {
            return (null, Error(Amqp1ErrorCondition.NotImplemented, "transactions are not supported"));
        }

        if (NodeRequest(attach, terminus) is not { } request)
        {
            return (null, NodeRefusal(Amqp1NodeOutcome.NotFound));
        }

        // ADR-0146 decision 2.2: a refused selector refuses the attach before any node is made.
        var filters = attach.IsReceiver ? Amqp1SourceFilters.Read(attach.Source, request.IsTopic) : Amqp1SourceFilters.None;
        if (filters is null)
        {
            return (null, Error(Amqp1ErrorCondition.InvalidField, "selector not valid"));
        }

        var node = nodes.Resolve(request);
        if (node.CanonicalAddress is not { } nodeAddress)
        {
            return (null, NodeRefusal(node.Outcome));
        }

        link.NodeAddress = nodeAddress;
        link.Filters = filters;
        return (NodeTerminus(attach, terminus, nodeAddress, filters.Applied), null);
    }

    // What to ask the directory; null when the address names no node.
    private Amqp1NodeRequest? NodeRequest(Amqp1Attach attach, Amqp1Terminus terminus)
    {
        if (terminus.IsDynamic)
        {
            return new(null, attach.IsReceiver, accountName);
        }

        return Amqp1NodeAddress.Canonical(terminus.Address, forSending: !attach.IsReceiver) is { } canonicalAddress
            ? new(canonicalAddress, attach.IsReceiver, accountName)
            : null;
    }

    // A dynamic node is answered with the address the directory gave it; any other echoes the client's.
    private static Amqp1Described NodeTerminus(Amqp1Attach attach, Amqp1Terminus terminus, string nodeAddress, Amqp1Map? appliedFilters)
    {
        var answeredAddress = terminus.IsDynamic ? nodeAddress : terminus.Address!;
        return attach.IsReceiver
            ? Amqp1Terminus.Source(answeredAddress, terminus.IsDynamic, Amqp1NodeAddress.IsTopic(nodeAddress), appliedFilters)
            : Amqp1Terminus.Target(answeredAddress, terminus.IsDynamic);
    }

    // Fixed text, never the address (ADR-0107 decision 7).
    private static Amqp1Error NodeRefusal(Amqp1NodeOutcome outcome) => outcome switch
    {
        Amqp1NodeOutcome.Unauthorized => Error(Amqp1ErrorCondition.UnauthorizedAccess, "not authorized"),
        Amqp1NodeOutcome.ResourceLimitExceeded => Error(Amqp1ErrorCondition.ResourceLimitExceeded, "resource limit exceeded"),
        _ => Error(Amqp1ErrorCondition.NotFound, "no such node"),
    };

    // The opposite role, the client's name, a handle of the container's, the node's terminus where the
    // client named the node, and the client's other terminus echoed.
    private Amqp1Attach AnsweringAttach(Amqp1Attach attach, uint containerHandle, Amqp1Described? nodeTerminus) =>
        attach.IsReceiver
            ? new Amqp1Attach(attach.Name, containerHandle, IsReceiver: false)
            {
                SenderSettleMode = attach.SenderSettleMode,
                ReceiverSettleMode = attach.ReceiverSettleMode,
                Source = nodeTerminus,
                Target = attach.Target,
                InitialDeliveryCount = 0,
            }
            : new Amqp1Attach(attach.Name, containerHandle, IsReceiver: true)
            {
                SenderSettleMode = attach.SenderSettleMode,
                ReceiverSettleMode = Amqp1Attach.DefaultReceiverSettleMode,
                Source = attach.Source,
                Target = nodeTerminus,
                MaxMessageSize = (ulong)context.Limits.MaxUploadBytes,
            };

    // A client receiver's link becomes the topic's live subscription or the queue's consumer; null
    // when it may, or the error that refuses the attach.
    private Amqp1Error? OpenNode(Amqp1Link link)
    {
        if (link.ContainerReceives)
        {
            return null;
        }

        var outcome = Amqp1NodeAddress.IsTopic(link.NodeAddress) ? Subscribe(link) : Consume(link);
        if (outcome == MessageBusOutcome.Succeeded)
        {
            return null;
        }

        return NodeRefusal(
            UnauthorizedSourceOutcomes.Contains(outcome) ? Amqp1NodeOutcome.Unauthorized
            : outcome == MessageBusOutcome.TooManySubscriptions ? Amqp1NodeOutcome.ResourceLimitExceeded
            : Amqp1NodeOutcome.NotFound);
    }

    private MessageBusOutcome Subscribe(Amqp1Link link)
    {
        var subscribed = busConnection.Subscribe(link.NodeAddress[Amqp1NodeAddress.TopicPrefix.Length..], message => OfferTopicMessage(link, message), noLocal: link.Filters.NoLocal);
        link.Subscription = subscribed.Subscription;
        return subscribed.Outcome;
    }

    private MessageBusOutcome Consume(Amqp1Link link)
    {
        var queueName = link.NodeAddress[Amqp1NodeAddress.QueuePrefix.Length..];
        var consumed = busConnection.Consume(queueName, OfferQueueDelivery(link), MaxWaitingDeliveries, accepts: link.Filters.QueuePredicate(queueName));
        link.Consumer = consumed.Consumer;
        link.Consumer?.Start();
        return consumed.Outcome;
    }

    // On the bus's thread: a message the link's filters refuse is not sent and never waits (ADR-0146
    // decision 2.4), nor one on a topic the client cannot name (ADR-0100 decision 3.2), and one past the
    // link's 1000 waiting is dropped for this link (ADR-0107 decision 7).
    private void OfferTopicMessage(Amqp1Link link, BusMessage message)
    {
        if (link.Filters.Accepts(message, queueName: null)
            && bus.Names.FromCanonicalTopic(Amqp1BusNodeDirectory.Scheme, message.Topic).Name is { } address
            && link.Offer(new(message, address, null, false), MaxWaitingDeliveries))
        {
            WakePump();
        }
    }

    // On the bus's thread: the consumer's window already bounds what waits.
    private Action<BusQueueDelivery> OfferQueueDelivery(Amqp1Link link) => delivery =>
    {
        link.Offer(new(delivery.Message, Amqp1NodeAddress.QueuePrefix + delivery.QueueName, delivery.DeliveryTag, delivery.Redelivered), int.MaxValue);
        WakePump();
    };

    private async ValueTask<bool> AnswerDetachAsync(Amqp1Session session, Amqp1Detach detach)
    {
        if (!session.LinksByClientHandle.Remove(detach.Handle, out var link))
        {
            return await EndSessionWithErrorAsync(session, UnattachedHandle());
        }

        NoteClientError("detach", detach.Error);
        EndLink(session, link);
        if (!link.IsDetachedByContainer)
        {
            // Nothing of a link is kept for resumption, so a suspending detach ends it all the same.
            await WriteAsync(session.OutgoingChannel, new Amqp1Detach(link.ContainerHandle) { Closed = detach.Closed });
        }

        return true;
    }

    // Part 2 section 2.6.5: the container detaches the link with an error and ignores its frames until
    // the client's detach, which it does not answer.
    private async ValueTask<bool> DetachWithErrorAsync(Amqp1Session session, Amqp1Link link, Amqp1Error error)
    {
        context.Log.Note($"Link {link.ContainerHandle} detached with {error.Condition.Value}: {error.Description}.");
        link.IsDetachedByContainer = true;
        EndLink(session, link);
        await WriteAsync(session.OutgoingChannel, new Amqp1Detach(link.ContainerHandle) { Closed = true, Error = error });
        return true;
    }

    // Its subscription or consumer ends, a consumer's unsettled deliveries going back to the queue, and
    // any partial delivery is discarded.
    private void EndLink(Amqp1Session session, Amqp1Link link)
    {
        link.EndSource();
        DiscardPartial(link);
        link.Sending = null;
        link.UnsettledCount = 0;
        foreach (var deliveryId in session.Unsettled.Where(entry => entry.Value.Link == link).Select(entry => entry.Key).ToList())
        {
            session.Unsettled.Remove(deliveryId);
        }

        if (link.DroppedCount > 0)
        {
            context.Log.Note($"Link {link.ContainerHandle} dropped {link.DroppedCount} topic messages past {MaxWaitingDeliveries} waiting.");
        }
    }

    private void EndLinks(Amqp1Session session)
    {
        foreach (var link in session.LinksByClientHandle.Values)
        {
            EndLink(session, link);
        }

        session.LinksByClientHandle.Clear();
    }

    // The attached link a frame names; null, with the session ended, when the handle is not attached.
    private async ValueTask<Amqp1Link?> AttachedLinkAsync(Amqp1Session session, uint handle)
    {
        if (session.LinksByClientHandle.TryGetValue(handle, out var link))
        {
            return link;
        }

        await EndSessionWithErrorAsync(session, UnattachedHandle());
        return null;
    }

    private static Amqp1Error UnattachedHandle() => Error(Amqp1ErrorCondition.UnattachedHandle, "unattached handle");
}
