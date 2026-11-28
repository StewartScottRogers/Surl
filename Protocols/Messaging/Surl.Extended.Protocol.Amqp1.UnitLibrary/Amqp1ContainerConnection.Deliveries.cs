using System.Buffers.Binary;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The container as sender, and <c>flow</c>: client receivers' credit, the deliveries the bus hands their
/// links, the client's dispositions, and both session windows (Part 2 sections 2.5.6, 2.6.7, 2.6.12,
/// 2.7.4 and 2.7.6; ADR-0107 decisions 4 and 5).
/// </summary>
public sealed partial class Amqp1ContainerConnection
{
    private async ValueTask<bool> AnswerFlowAsync(Amqp1Session session, Amqp1Flow flow)
    {
        session.RemoteIncomingWindow = unchecked((flow.NextIncomingId ?? 0) + flow.IncomingWindow - session.NextOutgoingId);
        if (flow.Handle is not { } handle)
        {
            if (flow.Echo)
            {
                await WriteAsync(session.OutgoingChannel, SessionFlow(session));
            }

            return true;
        }

        if (await AttachedLinkAsync(session, handle) is { IsDetachedByContainer: false } link)
        {
            await (link.ContainerReceives ? AnswerSenderFlowAsync(session, link, flow) : AnswerReceiverFlowAsync(session, link, flow));
        }

        return true;
    }

    // A client sender's flow states its delivery-count; drain changes nothing (ADR-0107 decision 5).
    private async ValueTask AnswerSenderFlowAsync(Amqp1Session session, Amqp1Link link, Amqp1Flow flow)
    {
        link.DeliveryCount = flow.DeliveryCount ?? link.DeliveryCount;
        if (flow.Echo)
        {
            await WriteLinkFlowAsync(session, link);
        }
    }

    // Part 2 section 2.6.7: link-credit(snd) = delivery-count(rcv) + link-credit(rcv) - delivery-count(snd).
    private async ValueTask AnswerReceiverFlowAsync(Amqp1Session session, Amqp1Link link, Amqp1Flow flow)
    {
        // Part 4 section 4.4.3: a txn-id in the flow's properties acquires what is sent next in that transaction.
        link.AcquiringTransactionId = flow.TransactionId;
        if (flow.LinkCredit is { } credit)
        {
            var left = unchecked((int)((flow.DeliveryCount ?? 0) + credit - link.DeliveryCount));
            link.Credit = (uint)Math.Max(left, 0);
        }

        // A drain is answered only once nothing is held for the link: setting its consumer's credit had the
        // bus hold what was ready, and sending claims until none is left (ADR-0146 Amendment 7 item 3).
        await SendLinkAsync(session, link);
        if (flow.Drain && link.Credit > 0)
        {
            link.DeliveryCount += link.Credit;
            link.Credit = 0;
            link.GiveConsumerCredit(0);
            await WriteLinkFlowAsync(session, link, drain: true);
        }
        else if (flow.Echo)
        {
            await WriteLinkFlowAsync(session, link);
        }
    }

    private Amqp1Flow SessionFlow(Amqp1Session session) => new(ContainerWindow, session.NextOutgoingId, ContainerWindow)
    {
        NextIncomingId = session.NextIncomingId,
    };

    private ValueTask WriteLinkFlowAsync(Amqp1Session session, Amqp1Link link, bool drain = false) =>
        WriteAsync(session.OutgoingChannel, SessionFlow(session) with
        {
            Handle = link.ContainerHandle,
            DeliveryCount = link.DeliveryCount,
            LinkCredit = link.ContainerReceives ? CreditLeft(link) : link.Credit,
            Available = link.ContainerReceives ? null : AvailableToSend(link),
            Drain = drain,
        });

    // A queue link's available is its queue's ready count, a hint that counts what its filters would
    // refuse (ADR-0146 Amendment 7 item 3); a live topic link's is what waits on it.
    private uint AvailableToSend(Amqp1Link link) =>
        (uint)(link.Consumer is { } consumer ? busConnection.InspectQueue(consumer.QueueName).ReadyMessageCount : link.WaitingCount);

    // Every sending link of every session sends what its credit and its session's window allow, round
    // after round until one sends nothing: what one link could not keep may have gone to another.
    private async ValueTask SendWaitingAsync()
    {
        bool sent;
        do
        {
            var links = sessionsByIncomingChannel.Values
                .SelectMany(session => session.LinksByClientHandle.Values.Select(link => (session, link)))
                .ToList();

            // Before any link sends, what a link can no longer send is back in its queue for one that can.
            foreach (var (session, link) in links)
            {
                GiveConsumerWhatTheLinkCanSend(session, link);
            }

            sent = false;
            foreach (var (session, link) in links)
            {
                sent |= await SendLinkAsync(session, link);
            }
        }
        while (sent);
    }

    // ADR-0146 Amendment 7 items 1 and 3: the bus holds for a queue link's consumer at most the link's
    // credit, and nothing while its session's window is closed or it has no room for another delivery.
    private void GiveConsumerWhatTheLinkCanSend(Amqp1Session session, Amqp1Link link) =>
        link.GiveConsumerCredit(session.RemoteIncomingWindow > 0 && HasRoomForAnotherDelivery(link) ? link.Credit : 0);

    // Room is what the link holding its 1000 unsettled (ADR-0107 decision 7), or acquiring in
    // transactions that keep all the acquired deliveries they may, has none of.
    private bool HasRoomForAnotherDelivery(Amqp1Link link) => link.UnsettledCount < MaxWaitingDeliveries && !IsAcquisitionFull(link);

    // Frame by frame while the client's session window allows: a delivery larger than the window is
    // finished as later flows open it (Part 2 section 2.5.6). True when a frame was written.
    private async ValueTask<bool> SendLinkAsync(Amqp1Session session, Amqp1Link link)
    {
        var sent = false;
        GiveConsumerWhatTheLinkCanSend(session, link);
        while (!closed && link is { ContainerReceives: false, IsDetachedByContainer: false } && session.RemoteIncomingWindow > 0
            && (link.Sending ?? StartDelivery(session, link)) is { } sending)
        {
            await WriteFrameAsync(sending.Frames[sending.NextFrame], CancellationToken);
            sent = true;
            sending.NextFrame++;
            session.NextOutgoingId++;
            session.RemoteIncomingWindow--;
            if (sending.NextFrame == sending.Frames.Count)
            {
                FinishDelivery(link, sending);
            }
        }

        GiveConsumerWhatTheLinkCanSend(session, link);
        return sent;
    }

    // The link's next message as a delivery, its credit taken; null when the link has no credit, has no
    // room for another delivery, or has nothing to send.
    private Amqp1OutgoingTransfer? StartDelivery(Amqp1Session session, Amqp1Link link)
    {
        if (link.Credit == 0 || !HasRoomForAnotherDelivery(link) || TakeNextMessage(link) is not { } pending)
        {
            return null;
        }

        var deliveryId = session.NextDeliveryId++;
        var transaction = OpenTransaction(link.AcquiringTransactionId);
        var sending = new Amqp1OutgoingTransfer(DeliveryFrames(session, link, pending, deliveryId, transaction), pending.QueueDeliveryTag);
        link.NextDeliveryTag++;
        link.DeliveryCount++;
        link.Credit--;
        if (!link.SenderSettles)
        {
            var delivery = new Amqp1OutgoingDelivery(session, deliveryId, link, pending.QueueDeliveryTag);
            session.Unsettled.Add(deliveryId, delivery);
            link.UnsettledCount++;
            transaction?.Acquired.Add(delivery);
        }

        link.Sending = sending;
        return sending;
    }

    private Amqp1PendingMessage? TakeNextMessage(Amqp1Link link) =>
        link.Consumer is null ? link.TakeWaiting() : ClaimQueueMessage(link);

    // ADR-0146 Amendment 7 item 3: a queue delivery is claimed only as the container starts writing it.
    // One a subscription queue's link does not send is acknowledged, the queue being the subscription's
    // alone (decision 3.2), uses no link credit, and the next is claimed.
    private Amqp1PendingMessage? ClaimQueueMessage(Amqp1Link link)
    {
        while (link.TryClaimDelivery(out var delivery))
        {
            var address = link.SubscriptionQueue is null
                ? Amqp1NodeAddress.QueuePrefix + delivery.QueueName
                : SentTopicAddress(link, delivery.Message);
            if (address is not null)
            {
                return new(delivery.Message, address, delivery.DeliveryTag, delivery.Redelivered);
            }

            link.Consumer!.Acknowledge(delivery.DeliveryTag);
            link.GiveConsumerCredit(link.Credit);
        }

        return null;
    }

    private static void FinishDelivery(Amqp1Link link, Amqp1OutgoingTransfer sending)
    {
        link.Sending = null;
        if (link.SenderSettles && sending.QueueDeliveryTag is { } tag)
        {
            // ADR-0107 decision 5: a pre-settled queue delivery is acknowledged as it is written.
            link.Consumer!.Acknowledge(tag);
        }
    }

    // The transfers of one delivery, each at most the client's max-frame-size, more on all but the last
    // (Part 2 section 2.6.14).
    private List<Amqp1Frame> DeliveryFrames(Amqp1Session session, Amqp1Link link, Amqp1PendingMessage pending, uint deliveryId, Amqp1Transaction? transaction)
    {
        var payload = Amqp1BusMessages.ToTransferPayload(pending.Message, pending.Address, pending.Redelivered, context.TimeProvider.GetUtcNow());
        var tag = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(tag, link.NextDeliveryTag);
        Amqp1Transfer transfer = new(link.ContainerHandle)
        {
            DeliveryId = deliveryId,
            DeliveryTag = tag,
            MessageFormat = StandardMessageFormat,
            Settled = link.SenderSettles,
            State = transaction is null ? null : Amqp1DeliveryState.TransactionalAcquisition(transaction.TransactionId),
        };
        var maxFrameSize = (int)Math.Min(clientMaxFrameSize, offeredMaxFrameSize);
        List<Amqp1Frame> frames = [];
        var sent = 0;
        do
        {
            var room = maxFrameSize - Amqp1Frame.HeaderBytes - Amqp1Encoder.Encode((transfer with { More = true }).ToDescribed()).Length;
            var length = Math.Min(room, payload.Length - sent);
            var more = sent + length < payload.Length;
            frames.Add(Amqp1Frame.Carrying(session.OutgoingChannel, transfer with { More = more }, payload.AsMemory(sent, length)));
            sent += length;
            transfer = new Amqp1Transfer(link.ContainerHandle);
        }
        while (sent < payload.Length);

        return frames;
    }

    // A client receiver's outcome for the container's deliveries in the range (ADR-0107 decision 5).
    private async ValueTask<bool> AnswerDispositionAsync(Amqp1Session session, Amqp1Disposition disposition)
    {
        if (!disposition.IsReceiver)
        {
            // The container settles what it receives at once (rcv-settle-mode first); a sender's disposition changes nothing.
            return true;
        }

        var span = unchecked((disposition.Last ?? disposition.First) - disposition.First);
        var settled = session.Unsettled.Where(entry => unchecked(entry.Key - disposition.First) <= span).ToList();
        if (Amqp1DeliveryState.IsTransactional(disposition.State))
        {
            await RetireInTransactionAsync(session, disposition, settled);
            return true;
        }

        foreach (var (deliveryId, delivery) in settled)
        {
            await SettleSentAsync(session, deliveryId, delivery, disposition);
        }

        return true;
    }

    private async ValueTask SettleSentAsync(Amqp1Session session, uint deliveryId, Amqp1OutgoingDelivery delivery, Amqp1Disposition disposition)
    {
        delivery.ForgetHeldOutcome();
        delivery.OutcomeApplied = delivery.OutcomeApplied || ApplyOutcome(delivery, disposition.State);
        if (disposition.Settled)
        {
            session.Unsettled.Remove(deliveryId);
            delivery.Link.UnsettledCount--;
            if (!delivery.OutcomeApplied)
            {
                // Settled with no outcome: the message was not processed, so it goes back.
                ApplyOutcome(delivery, new Amqp1Described(Amqp1Descriptor.Released, Array.Empty<object?>()));
            }
        }
        else if (delivery.OutcomeApplied && delivery.Link.ClientSettlesSecond)
        {
            // rcv-settle-mode second: the container settles once the client has stated its outcome.
            session.Unsettled.Remove(deliveryId);
            delivery.Link.UnsettledCount--;
            await WriteAsync(session.OutgoingChannel, new Amqp1Disposition(IsReceiver: false, deliveryId) { Settled = true, State = disposition.State });
        }
    }

    // Accepted acknowledges a queue delivery, rejected drops it, released puts it back, modified puts it
    // back unless undeliverable-here, which keeps it from this link until the link ends. A delivery from
    // a kept or non-durable shared subscription's queue is a queue delivery, so its outcome acts on that
    // queue; only a live topic subscription's delivery has nothing to put back. False for a state that is
    // no outcome.
    private static bool ApplyOutcome(Amqp1OutgoingDelivery delivery, object? state)
    {
        var outcome = Amqp1DeliveryState.OutcomeOf(state);
        if (outcome is Amqp1Outcome.None or Amqp1Outcome.Received)
        {
            return false;
        }

        if (delivery.QueueDeliveryTag is { } tag && !Amqp1DeliveryState.IsUndeliverableHere(state))
        {
            var consumer = delivery.Link.Consumer!;
            _ = outcome == Amqp1Outcome.Accepted
                ? consumer.Acknowledge(tag)
                : consumer.Reject(tag, requeue: outcome != Amqp1Outcome.Rejected);
        }

        return true;
    }
}
