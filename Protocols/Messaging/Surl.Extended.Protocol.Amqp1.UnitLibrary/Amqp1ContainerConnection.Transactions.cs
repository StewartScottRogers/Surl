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
    // txn-id is answered rejected, and the deliveries stay as they were.
    private async ValueTask RetireInTransactionAsync(Amqp1Session session, Amqp1Disposition disposition, List<KeyValuePair<uint, Amqp1OutgoingDelivery>> deliveries)
    {
        if (OpenTransaction(Amqp1DeliveryState.TransactionIdOf(disposition.State)) is not { } transaction)
        {
            await WriteAsync(session.OutgoingChannel, new Amqp1Disposition(IsReceiver: false, disposition.First)
            {
                Last = disposition.Last,
                State = Amqp1DeliveryState.Rejected(UnknownTransaction()),
            });
            return;
        }

        var outcome = Amqp1DeliveryState.TransactionalOutcomeOf(disposition.State);
        foreach (var (deliveryId, delivery) in deliveries)
        {
            transaction.HoldOutcome(delivery, outcome, disposition.Settled);
            if (disposition.Settled && session.Unsettled.Remove(deliveryId))
            {
                delivery.Link.UnsettledCount--;
            }
        }
    }

    // The held outcomes are applied in the order stated; under rcv-settle-mode second the container then
    // settles the deliveries the client left unsettled.
    private async ValueTask CommitOutcomesAsync(Amqp1Transaction transaction)
    {
        transaction.Acquired.Clear();
        foreach (var held in transaction.HeldOutcomes)
        {
            var delivery = held.Delivery;
            delivery.Held = null;
            delivery.OutcomeApplied = ApplyOutcome(delivery, held.Outcome) || delivery.OutcomeApplied;
            if (!held.Settled && delivery.Link.ClientSettlesSecond && delivery.Session.Unsettled.Remove(delivery.DeliveryId))
            {
                delivery.Link.UnsettledCount--;
                await WriteAsync(delivery.Session.OutgoingChannel, new Amqp1Disposition(IsReceiver: false, delivery.DeliveryId) { Settled = true, State = held.Outcome });
            }
        }
    }

    // ADR-0146 decisions 1.5 and 1.6: held outcomes are forgotten, their settled deliveries unsettled again;
    // deliveries acquired in the transaction and not retired outside it go back to their queue, redelivered.
    private static void RollBack(Amqp1Transaction transaction)
    {
        foreach (var held in transaction.HeldOutcomes)
        {
            var delivery = held.Delivery;
            delivery.Held = null;
            if (held.Settled && !delivery.Link.IsEnded)
            {
                delivery.Session.Unsettled.Add(delivery.DeliveryId, delivery);
                delivery.Link.UnsettledCount++;
            }
        }

        foreach (var delivery in transaction.Acquired.Where(acquired => acquired.Session.Unsettled.Remove(acquired.DeliveryId)))
        {
            delivery.Link.UnsettledCount--;
            if (delivery.QueueDeliveryTag is { } tag)
            {
                delivery.Link.Consumer!.Reject(tag, requeue: true);
            }
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
        // Held messages count with the partial deliveries against --max-filesize (ADR-0146 decision 1.4).
        var bytes = delivery.Bytes.WrittenCount;
        if (transactions.Values.Sum(open => open.HeldBytes) + partialBytes + bytes > context.Limits.MaxUploadBytes)
        {
            return StoreRejection(MessageBusOutcome.StoreFull);
        }

        transaction.Hold(entry, bytes);
        return null;
    }

    private Amqp1Transaction? OpenTransaction(byte[]? transactionId) =>
        Amqp1Transaction.IdOf(transactionId) is { } id && transactions.TryGetValue(id, out var transaction) ? transaction : null;

    private static Amqp1Error UnknownTransaction() => Error(Amqp1ErrorCondition.TransactionUnknownId, "unknown transaction");

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
