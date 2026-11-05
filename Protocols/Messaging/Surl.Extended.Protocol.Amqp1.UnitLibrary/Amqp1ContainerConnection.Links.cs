namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Links: <c>attach</c> and <c>detach</c>, the handles, and the nodes they attach to (Part 2 sections
/// 2.6.3, 2.6.4, 2.7.3 and 2.7.7; Part 3 section 3.5; ADR-0107 decision 4).
/// </summary>
public sealed partial class Amqp1ContainerConnection
{
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

        var link = new Amqp1Link(containerHandle);
        session.LinksByClientHandle.Add(attach.Handle, link);
        var (nodeTerminus, refusal) = ResolveNode(attach);
        await WriteAsync(session.OutgoingChannel, AnsweringAttach(attach, containerHandle, nodeTerminus));
        if (refusal is not null)
        {
            // Part 2 section 2.6.3: a null terminus, then at once a closing detach with the error.
            link.IsDetachedByContainer = true;
            await WriteAsync(session.OutgoingChannel, new Amqp1Detach(containerHandle) { Closed = true, Error = refusal });
        }

        return true;
    }

    // The container's terminus for the node the client's source (a client receiver) or target
    // (a client sender) names; or null and the error that refuses the attach.
    private (Amqp1Described? NodeTerminus, Amqp1Error? Refusal) ResolveNode(Amqp1Attach attach)
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

        var node = nodes.Resolve(request);
        return node.CanonicalAddress is { } nodeAddress
            ? (NodeTerminus(attach, terminus, nodeAddress), null)
            : (null, NodeRefusal(node.Outcome));
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
    private static Amqp1Described NodeTerminus(Amqp1Attach attach, Amqp1Terminus terminus, string nodeAddress)
    {
        var answeredAddress = terminus.IsDynamic ? nodeAddress : terminus.Address!;
        return attach.IsReceiver
            ? Amqp1Terminus.Source(answeredAddress, terminus.IsDynamic, Amqp1NodeAddress.IsTopic(nodeAddress))
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

    private async ValueTask<bool> AnswerDetachAsync(Amqp1Session session, Amqp1Detach detach)
    {
        if (!session.LinksByClientHandle.Remove(detach.Handle, out var link))
        {
            return await EndSessionWithErrorAsync(session, UnattachedHandle());
        }

        NoteClientError("detach", detach.Error);
        if (!link.IsDetachedByContainer)
        {
            // Nothing of a link is kept for resumption, so a suspending detach ends it all the same.
            await WriteAsync(session.OutgoingChannel, new Amqp1Detach(link.ContainerHandle) { Closed = detach.Closed });
        }

        return true;
    }

    private ValueTask<bool> RequireAttachedAsync(Amqp1Session session, uint handle) =>
        session.LinksByClientHandle.ContainsKey(handle)
            ? ValueTask.FromResult(true)
            : EndSessionWithErrorAsync(session, UnattachedHandle());

    private static Amqp1Error UnattachedHandle() => Error(Amqp1ErrorCondition.UnattachedHandle, "unattached handle");
}
