using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Local transactions (Part 4; ADR-0146 decisions 1.1 to 1.4 and 1.6 to 1.8): the coordinator link,
/// <c>declare</c> and <c>discharge</c>, and messages posted in a transaction, held until a commit
/// publishes them all or none.
/// </summary>
public sealed partial class Amqp1ContainerConnection
{
    /// <summary>The most transactions open at once on a connection (ADR-0146 decision 1.3).</summary>
    public const int MaxOpenTransactions = 100;

    /// <summary>
    /// The most posted messages a connection's open transactions hold between them, whatever
    /// <c>--max-filesize</c> is (ADR-0146, note to decision 1.4): the bus's default bound on one queue's
    /// messages.
    /// </summary>
    public const int MaxHeldMessages = MessageBus.MessageBus.DefaultMaxMessagesPerQueue;

    /// <summary>
    /// The most characters of node names the messages posted in a connection's open transactions keep
    /// between them, each link's name counted once per transaction, whatever <c>--max-filesize</c> is
    /// (ADR-0146, BL-2125 note to decision 1.4).
    /// </summary>
    public const int MaxHeldNodeNameChars = 16 * 1024 * 1024;

    /// <summary>
    /// The most deliveries a connection's open transactions keep as acquired between them (ADR-0146,
    /// BL-2125 note to decision 1.5); at it, a link acquiring in one of them sends nothing more.
    /// </summary>
    public const int MaxAcquiredDeliveries = MessageBus.MessageBus.DefaultMaxMessagesPerQueue;

    /// <summary>
    /// The most outcomes a connection's open transactions hold between them (ADR-0146, BL-2125 note to
    /// decision 1.5); a transactional <c>disposition</c> that would pass it is refused.
    /// </summary>
    public const int MaxHeldOutcomes = MessageBus.MessageBus.DefaultMaxMessagesPerQueue;

    // The connection's open transactions by number (ADR-0146 decision 1.2: a transaction belongs to the connection).
    private readonly Dictionary<uint, Amqp1Transaction> transactions = [];

    private uint lastTransactionId;

    // Answers a declare or discharge with the delivery state its disposition carries.
    private async ValueTask<Amqp1Described> ControlAsync(Amqp1Link coordinator, Amqp1TransactionControl? control)
    {
        if (control is null)
        {
            return Amqp1DeliveryState.Rejected(Error(Amqp1ErrorCondition.DecodeError, "not a declare or discharge"));
        }

        return control.IsDeclare ? Declare(coordinator, control) : await DischargeAsync(control);
    }

    // ADR-0146 decision 1.3: ids count up from 00 00 00 01 and are never reused on the connection.
    private Amqp1Described Declare(Amqp1Link coordinator, Amqp1TransactionControl declare)
    {
        if (declare.GlobalId is not null)
        {
            return Amqp1DeliveryState.Rejected(Error(Amqp1ErrorCondition.NotImplemented, "distributed transactions are not supported"));
        }

        if (transactions.Count >= MaxOpenTransactions)
        {
            return Amqp1DeliveryState.Rejected(Error(Amqp1ErrorCondition.ResourceLimitExceeded, "too many open transactions"));
        }

        var transaction = new Amqp1Transaction(++lastTransactionId, coordinator);
        transactions.Add(transaction.Id, transaction);
        return Amqp1DeliveryState.Declared(transaction.TransactionId);
    }

    // ADR-0146 decision 1.6: fail rolls back; otherwise the held messages are published all or none.
    private async ValueTask<Amqp1Described> DischargeAsync(Amqp1TransactionControl discharge)
    {
        if (OpenTransaction(discharge.TransactionId) is not { } transaction)
        {
            return Amqp1DeliveryState.Rejected(UnknownTransaction());
        }

        transactions.Remove(transaction.Id);
        var published = discharge.Fail ? null : busConnection.PublishBatch(transaction.Held);
        if (published is not null && published.Outcome != MessageBusOutcome.Succeeded)
        {
            context.Log.Note($"A transaction was rolled back: the bus refused its message {published.RefusedIndex} with {published.Outcome}.");
            RollBack(transaction);
            return Amqp1DeliveryState.Rejected(Error(Amqp1ErrorCondition.TransactionRollback, "the transaction was rolled back"));
        }

        if (published is null)
        {
            RollBack(transaction);
            return Amqp1DeliveryState.Accepted;
        }

        await CommitOutcomesAsync(transaction);
        await SaveBusAsync();
        return Amqp1DeliveryState.Accepted;
    }

    // ADR-0146 decision 1.5: a transactional disposition holds its outcome until the commit; an unknown
    // txn-id, or one that would pass MaxHeldOutcomes, is answered rejected, and the deliveries stay as
    // they were. The outcome is held bare, so a peer cannot size what is kept.
    private async ValueTask RetireInTransactionAsync(Amqp1Session session, Amqp1Disposition disposition, List<KeyValuePair<uint, Amqp1OutgoingDelivery>> deliveries)
    {
        if (OpenTransaction(Amqp1DeliveryState.TransactionIdOf(disposition.State)) is not { } transaction)
        {
            await RefuseRetirementAsync(session, disposition, UnknownTransaction());
            return;
        }

        // A delivery an open transaction already holds an outcome for adds none.
        var added = deliveries.Count(entry => entry.Value.Held is null);
        if (transactions.Values.Sum(open => open.HeldOutcomes.Count) + added > MaxHeldOutcomes)
        {
            await RefuseRetirementAsync(session, disposition, Error(Amqp1ErrorCondition.ResourceLimitExceeded, "too many outcomes held in open transactions"));
            return;
        }

        var outcome = Amqp1DeliveryState.BareOutcomeOf(Amqp1DeliveryState.TransactionalOutcomeOf(disposition.State));
        foreach (var (deliveryId, delivery) in deliveries)
        {
            transaction.HoldOutcome(delivery, outcome, disposition.Settled);
            if (disposition.Settled && session.Unsettled.Remove(deliveryId))
            {
                delivery.Link.UnsettledCount--;
            }
        }
    }

    // The container's rejected disposition over the refused one's range, settling nothing (ADR-0146 Amendment 1).
    private ValueTask RefuseRetirementAsync(Amqp1Session session, Amqp1Disposition disposition, Amqp1Error error) =>
        WriteAsync(session.OutgoingChannel, new Amqp1Disposition(IsReceiver: false, disposition.First)
        {
            Last = disposition.Last,
            State = Amqp1DeliveryState.Rejected(error),
        });

    // ADR-0146, BL-2125 note to decision 1.5: whether what the link sends next would be acquired in a
    // transaction and the open transactions already keep as many acquired deliveries as they may. A
    // pre-settled delivery is kept by none.
    private bool IsAcquisitionFull(Amqp1Link link) =>
        !link.SenderSettles
        && OpenTransaction(link.AcquiringTransactionId) is not null
        && transactions.Values.Sum(open => open.Acquired.Count) >= MaxAcquiredDeliveries;

    // The held outcomes are applied in the order stated; under rcv-settle-mode second the container then
    // settles the deliveries the client left unsettled.
    private async ValueTask CommitOutcomesAsync(Amqp1Transaction transaction)
    {
        transaction.Acquired.Clear();
        foreach (var held in transaction.HeldOutcomes)
        {
            var delivery = held.Delivery;
            delivery.Held = null;
            // A delivery's first outcome is the one applied, as outside a transaction: a later one must not
            // requeue a delivery modified undeliverable-here while its link lasts (ADR-0146 Amendment 10).
            delivery.OutcomeApplied = delivery.OutcomeApplied || ApplyOutcome(delivery, held.Outcome);
            if (!held.Settled && delivery.Link.ClientSettlesSecond && delivery.Session.Unsettled.Remove(delivery.DeliveryId))
            {
                delivery.Link.UnsettledCount--;
                await WriteAsync(delivery.Session.OutgoingChannel, new Amqp1Disposition(IsReceiver: false, delivery.DeliveryId) { Settled = true, State = held.Outcome });
            }
        }
    }

    // ADR-0146 decisions 1.5 and 1.6 and Amendments 1, 10, 11 and 12: held outcomes are forgotten, their
    // settled deliveries unsettled again (those of an ended link were forgotten as it ended), as many as
    // their link's bound takes. Deliveries acquired in the transaction, still unsettled, with no outcome
    // applied and none held by another open transaction leave the unsettled set and go back to their queue.
    // One whose outcome stated outside the transaction was applied stays as it is, so one modified
    // undeliverable-here stays with its link until the link ends. One whose outcome another transaction
    // holds stays with its link too, for that transaction's discharge to decide.
    private static void RollBack(Amqp1Transaction transaction)
    {
        foreach (var held in transaction.HeldOutcomes)
        {
            var delivery = held.Delivery;
            delivery.Held = null;
            if (held.Settled)
            {
                delivery.Session.Unsettled.Add(delivery.DeliveryId, delivery);
                delivery.Link.UnsettledCount++;
            }
        }

        // The loop above cleared this transaction's held outcomes, so one still held is another's.
        foreach (var delivery in transaction.Acquired.Where(acquired => !acquired.OutcomeApplied && acquired.Held is null && acquired.Session.Unsettled.Remove(acquired.DeliveryId)))
        {
            delivery.Link.UnsettledCount--;
            ReturnToQueue(delivery);
        }

        ForgetSettledPastLinkBound(transaction.HeldOutcomes.Where(held => held.Settled).Select(held => held.Delivery));
    }

    // ADR-0146 Amendment 12: a rollback leaves no link holding more than MaxWaitingDeliveries unsettled.
    // Of the client-settled deliveries it put back, those past the bound, the earliest stated first, are
    // forgotten; one with no outcome applied goes back to its queue.
    private static void ForgetSettledPastLinkBound(IEnumerable<Amqp1OutgoingDelivery> putBack)
    {
        // Lazy: the link's count is read again for each delivery, after the last one forgotten.
        foreach (var delivery in putBack.Where(delivery => delivery.Link.UnsettledCount > MaxWaitingDeliveries && delivery.Session.Unsettled.Remove(delivery.DeliveryId)))
        {
            delivery.Link.UnsettledCount--;
            if (!delivery.OutcomeApplied)
            {
                ReturnToQueue(delivery);
            }
        }
    }

    // Puts a delivery with a queue delivery tag (a queue's, or a kept or non-durable shared subscription's)
    // back on its queue, redelivered; a live topic subscription's has none and nothing to return.
    private static void ReturnToQueue(Amqp1OutgoingDelivery delivery)
    {
        if (delivery.QueueDeliveryTag is { } tag)
        {
            delivery.Link.Consumer!.Reject(tag, requeue: true);
        }
    }

    // ADR-0146 decision 1.4: judged at once, then held and answered transactional-state with the
    // provisional outcome; a txn-id naming no open transaction is rejected outside any transaction.
    private Amqp1Described PostInTransaction(Amqp1Link link, Amqp1IncomingDelivery delivery)
    {
        if (OpenTransaction(Amqp1DeliveryState.TransactionIdOf(delivery.State)) is not { } transaction)
        {
            return Amqp1DeliveryState.Rejected(UnknownTransaction());
        }

        var refusal = Hold(link, delivery, transaction);
        return Amqp1DeliveryState.TransactionalState(
            transaction.TransactionId,
            refusal is null ? Amqp1DeliveryState.Accepted : Amqp1DeliveryState.Rejected(refusal));
    }

    // Holds the message in the transaction; null when held, or the error that refuses it.
    private Amqp1Error? Hold(Amqp1Link link, Amqp1IncomingDelivery delivery, Amqp1Transaction transaction)
    {
        var (entry, refusal) = ReadMessage(link, delivery);
        if (entry is null)
        {
            return refusal;
        }

        // Access was judged when the link attached, and the commit's batch publish judges it again.

        // The count is bounded on its own because an empty message adds no bytes (ADR-0146, note to
        // decision 1.4).
        if (transactions.Values.Sum(open => open.Held.Count) >= MaxHeldMessages)
        {
            return Error(Amqp1ErrorCondition.ResourceLimitExceeded, "too many messages held in open transactions");
        }

        // Each link attached again has its own node name string, which no byte bound counts (ADR-0146,
        // BL-2125 note to decision 1.4).
        if (transactions.Values.Sum(open => open.HeldNodeNameChars) + transaction.NodeNameCharsAddedBy(entry.Message.Topic) > MaxHeldNodeNameChars)
        {
            return Error(Amqp1ErrorCondition.ResourceLimitExceeded, "too many node names held in open transactions");
        }

        // Held messages count with the partial deliveries against --max-filesize (ADR-0146 decision 1.4);
        // --max-filesize 0 is no limit (ADR-0006).
        var bytes = delivery.Bytes.WrittenCount;
        var maxBytes = context.Limits.MaxUploadBytes;
        if (maxBytes > 0 && transactions.Values.Sum(open => open.HeldBytes) + partialBytes + bytes > maxBytes)
        {
            return StoreRejection(MessageBusOutcome.StoreFull);
        }

        transaction.Hold(entry, bytes);
        return null;
    }

    private Amqp1Transaction? OpenTransaction(byte[]? transactionId) =>
        Amqp1Transaction.IdOf(transactionId) is { } id && transactions.TryGetValue(id, out var transaction) ? transaction : null;

    private static Amqp1Error UnknownTransaction() => Error(Amqp1ErrorCondition.TransactionUnknownId, "unknown transaction");

    // An ended link's deliveries leave every open transaction, freeing their room.
    private void ForgetTransactionalDeliveriesOf(Amqp1Link link)
    {
        foreach (var transaction in transactions.Values)
        {
            transaction.ForgetDeliveriesOf(link);
        }
    }

    // ADR-0146 decision 1.7: the transactions a coordinator link declared roll back, with no frame of
    // their own, when it detaches, its session ends or the connection ends.
    private void RollBackTransactionsDeclaredBy(Amqp1Link coordinator)
    {
        foreach (var transaction in transactions.Values.Where(open => open.Coordinator == coordinator).ToList())
        {
            transactions.Remove(transaction.Id);
            RollBack(transaction);
        }
    }
}
