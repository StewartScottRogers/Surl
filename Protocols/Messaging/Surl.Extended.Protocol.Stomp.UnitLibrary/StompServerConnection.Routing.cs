using System.Globalization;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// The frames a connected client sends (ADR-0118 decisions 4 to 6): SEND, SUBSCRIBE and
/// UNSUBSCRIBE on the shared bus, ACK and NACK, BEGIN, COMMIT and ABORT, DISCONNECT, and a RECEIPT
/// for each once it took effect.
/// </summary>
internal sealed partial class StompServerConnection
{
    private const string QueueDestinationPrefix = "/queue/";
    private const string QueueAccessPrefix = "$queue/";

    private static readonly BusQueueFlags DeclaredQueueFlags = new(Durable: true);

    // Guards the delivery state a bus callback and the reading share: the subscriptions' ends,
    // the outstanding messages and the message-id counter.
    private readonly Lock gate = new();
    private readonly Dictionary<string, StompSubscription> subscriptions = new(StringComparer.Ordinal);
    private readonly Dictionary<long, StompOutstandingMessage> outstanding = [];
    private readonly Dictionary<string, StompTransaction> transactions = new(StringComparer.Ordinal);
    private long lastMessageId;
    private bool disconnected;
    private BusQueueConsumer? consumerToStart;

    private void Answer(StompFrame frame)
    {
        if (frame.Command is "SEND")
        {
            Send(frame);
        }
        else
        {
            RequireNoBody(frame);
            AnswerBodiless(frame);
        }

        if (frame.Header("receipt") is { } receipt)
        {
            Enqueue(new StompFrame("RECEIPT", new KeyValuePair<string, string>("receipt-id", receipt)));
        }

        // A queue subscription's messages follow its RECEIPT.
        consumerToStart?.Start();
        consumerToStart = null;
    }

    private void AnswerBodiless(StompFrame frame)
    {
        switch (frame.Command)
        {
            case "SUBSCRIBE":
                Subscribe(frame);
                break;
            case "UNSUBSCRIBE":
                Unsubscribe(frame);
                break;
            case "ACK":
                Settle(frame);
                break;
            case "NACK" when version != StompVersion.V1_0:
                Settle(frame);
                break;
            case "DISCONNECT":
                context.Log.Note("DISCONNECT; closed.");
                disconnected = true;
                break;
            default:
                AnswerTransactionFrame(frame);
                break;
        }
    }

    private void AnswerTransactionFrame(StompFrame frame)
    {
        switch (frame.Command)
        {
            case "BEGIN":
                Begin(frame);
                break;
            case "COMMIT":
                Commit(frame);
                break;
            case "ABORT":
                TakeTransaction(frame);
                break;
            case "CONNECT" or "STOMP":
                // CONNECT and STOMP have no receipt (decision 6), so the ERROR names none.
                throw new StompProtocolViolationException(StompErrors.AlreadyConnected, $"A second {frame.Command} arrived");
            default:
                throw Violation(StompErrors.UnknownCommand, $"The command {frame.Command} is not served", frame);
        }
    }

    private void Send(StompFrame frame)
    {
        var destination = ResolveDestination(frame, isFilter: false);
        if (OpenTransactionOf(frame) is { } transaction)
        {
            Hold(transaction, frame);
            return;
        }

        Publish(destination, frame);
    }

    private void Publish(BusDestination destination, StompFrame frame)
    {
        var message = StompMessageFrames.ToBusMessage(destination.Name, frame);
        var outcome = destination.Kind == BusDestinationKind.Topic
            ? OnBus.Publish(message).Outcome
            : SendToQueue(destination.Name, message);
        ThrowIfRefused(outcome, frame);
    }

    private MessageBusOutcome SendToQueue(string queue, BusMessage message)
    {
        var declared = DeclareQueue(queue, TopicAccessRights.Publish);
        return declared is MessageBusOutcome.Succeeded ? bus.PublishToQueue(queue, message, account) : declared;
    }

    // ADR-0118 decision 4: access is judged before the queue is made, so a refused caller creates
    // nothing; a queue declared with other flags still exists, and the send or consume meets its
    // own refusal.
    private MessageBusOutcome DeclareQueue(string queue, TopicAccessRights right)
    {
        if (!bus.GetTopicAccess(account, QueueAccessPrefix + queue).HasFlag(right))
        {
            return MessageBusOutcome.AccessDenied;
        }

        return OnBus.DeclareQueue(queue, DeclaredQueueFlags).Outcome is MessageBusOutcome.TooManyQueues
            ? MessageBusOutcome.TooManyQueues
            : MessageBusOutcome.Succeeded;
    }

    private void Subscribe(StompFrame frame)
    {
        var id = frame.Header("id") ?? (version == StompVersion.V1_0 ? frame.Header("destination") : null)
            ?? throw MissingHeader(frame, "id");
        var ackMode = AckModeOf(frame);
        var destination = ResolveDestination(frame, isFilter: true);
        if (subscriptions.ContainsKey(id))
        {
            throw Violation(StompErrors.DuplicateSubscriptionId, "A SUBSCRIBE reused a live id", frame);
        }

        var subscription = new StompSubscription(id, ackMode);
        if (destination.Kind == BusDestinationKind.Topic)
        {
            SubscribeToTopic(subscription, destination.Name, frame);
        }
        else
        {
            SubscribeToQueue(subscription, destination.Name, frame);
        }

        subscriptions.Add(id, subscription);
    }

    private void SubscribeToTopic(StompSubscription subscription, string filter, StompFrame frame)
    {
        var result = OnBus.Subscribe(filter, message => DeliverFromTopic(subscription, message));
        ThrowIfRefused(result.Outcome, frame);
        subscription.Topic = result.Subscription;
    }

    private void SubscribeToQueue(StompSubscription subscription, string queue, StompFrame frame)
    {
        ThrowIfRefused(DeclareQueue(queue, TopicAccessRights.Subscribe), frame);
        var result = OnBus.Consume(queue, delivery => DeliverFromQueue(subscription, delivery));
        ThrowIfRefused(result.Outcome, frame);
        subscription.Queue = result.Consumer;
        consumerToStart = result.Consumer;
    }

    private StompAckMode AckModeOf(StompFrame frame) => frame.Header("ack") switch
    {
        null or "auto" => StompAckMode.Auto,
        "client" => StompAckMode.Client,
        "client-individual" when version != StompVersion.V1_0 => StompAckMode.ClientIndividual,
        _ => throw Violation(StompErrors.InvalidAckMode, "A SUBSCRIBE named an ack mode its version does not have", frame),
    };

    private void Unsubscribe(StompFrame frame)
    {
        var id = frame.Header("id") ?? (version == StompVersion.V1_0 ? frame.Header("destination") : null)
            ?? throw MissingHeader(frame, "id");

        // Ending what is already ended is harmless: an unknown id is ignored.
        if (subscriptions.Remove(id, out var subscription))
        {
            End(subscription);
        }
    }

    private void End(StompSubscription subscription)
    {
        lock (gate)
        {
            foreach (var message in outstanding.Values.Where(message => message.Subscription == subscription).ToList())
            {
                outstanding.Remove(message.MessageId);
            }
        }

        subscription.End();
    }

    // However the connection ends: every subscription ends, so its unacknowledged queue messages
    // go back to their queue, and every open transaction is aborted.
    private void EndSession()
    {
        foreach (var subscription in subscriptions.Values)
        {
            End(subscription);
        }

        subscriptions.Clear();
        transactions.Clear();
        OnBus.Dispose();
    }

    // Called by the bus and never blocks: the MESSAGE is queued in the outbox.
    private void DeliverFromTopic(StompSubscription subscription, BusMessage message)
    {
        var destination = bus.Names.FromCanonicalTopic(context.Scheme, message.Topic);
        if (destination.IsMapped)
        {
            Deliver(subscription, destination.Name, message, deliveryTag: null);
        }
    }

    private void DeliverFromQueue(StompSubscription subscription, BusQueueDelivery delivery)
    {
        Deliver(subscription, QueueDestinationPrefix + delivery.QueueName, delivery.Message, delivery.DeliveryTag);
        if (subscription.AckMode == StompAckMode.Auto)
        {
            subscription.Queue!.Acknowledge(delivery.DeliveryTag);
        }
    }

    // The bus hands nothing to a subscription once it has ended.
    private void Deliver(StompSubscription subscription, string destination, BusMessage message, long? deliveryTag)
    {
        lock (gate)
        {
            var messageId = ++lastMessageId;
            if (subscription.AckMode != StompAckMode.Auto)
            {
                outstanding.Add(messageId, new StompOutstandingMessage(messageId, subscription, deliveryTag));
            }

            Enqueue(StompMessageFrames.ToMessageFrame(version, destination, messageId, subscription, message));
        }
    }

    // ACK and NACK name the message by 1.2's id, the MESSAGE's ack header, or by earlier
    // versions' message-id.
    private void Settle(StompFrame frame)
    {
        var messageId = MessageIdOf(frame) ?? throw MissingHeader(frame, "id");
        if (OpenTransactionOf(frame) is { } transaction)
        {
            Hold(transaction, frame);
            return;
        }

        Settle(messageId, acknowledge: frame.Command == "ACK");
    }

    private string? MessageIdOf(StompFrame frame) => frame.Header(version == StompVersion.V1_2 ? "id" : "message-id");

    private void Settle(string messageIdText, bool acknowledge)
    {
        foreach (var message in TakeSettled(messageIdText))
        {
            if (message.DeliveryTag is { } deliveryTag)
            {
                var consumer = message.Subscription.Queue!;
                _ = acknowledge ? consumer.Acknowledge(deliveryTag) : consumer.Reject(deliveryTag, requeue: true);
            }
        }
    }

    // ADR-0118 decision 5: client mode settles the named message and every earlier one of its
    // subscription, client-individual the named one; a message not outstanding is ignored.
    private List<StompOutstandingMessage> TakeSettled(string messageIdText)
    {
        lock (gate)
        {
            if (!long.TryParse(messageIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var messageId)
                || !outstanding.TryGetValue(messageId, out var named))
            {
                return [];
            }

            var settled = named.Subscription.AckMode == StompAckMode.Client
                ? outstanding.Values.Where(message => message.Subscription == named.Subscription && message.MessageId <= messageId).OrderBy(message => message.MessageId).ToList()
                : [named];
            foreach (var message in settled)
            {
                outstanding.Remove(message.MessageId);
            }

            return settled;
        }
    }

    private void Begin(StompFrame frame)
    {
        var name = frame.Header("transaction") ?? throw MissingTransactionHeader(frame);
        if (transactions.ContainsKey(name))
        {
            throw Violation(StompErrors.DuplicateTransaction, "BEGIN named a transaction already open", frame);
        }

        if (transactions.Count >= StompServerOptions.MaxOpenTransactions)
        {
            throw Violation(StompErrors.ResourceLimitExceeded, $"BEGIN would open more than {StompServerOptions.MaxOpenTransactions} transactions", frame);
        }

        transactions.Add(name, new StompTransaction());
    }

    // Each held frame takes effect in order, as if sent then.
    private void Commit(StompFrame frame)
    {
        foreach (var held in TakeTransaction(frame).Held)
        {
            if (held.Command == "SEND")
            {
                Publish(ResolveDestination(held, isFilter: false), held);
            }
            else
            {
                Settle(MessageIdOf(held)!, acknowledge: held.Command == "ACK");
            }
        }
    }

    private StompTransaction TakeTransaction(StompFrame frame)
    {
        var name = frame.Header("transaction") ?? throw MissingTransactionHeader(frame);
        return transactions.Remove(name, out var transaction)
            ? transaction
            : throw UnknownTransaction(frame);
    }

    private StompTransaction? OpenTransactionOf(StompFrame frame) =>
        frame.Header("transaction") is not { } name ? null
        : transactions.TryGetValue(name, out var transaction) ? transaction
        : throw UnknownTransaction(frame);

    private void Hold(StompTransaction transaction, StompFrame frame)
    {
        var maxBytes = context.Limits.MaxUploadBytes;
        if (maxBytes > 0 && transaction.HeldBodyBytes + frame.Body.Length > maxBytes)
        {
            throw Violation(StompErrors.ResourceLimitExceeded, $"A transaction would hold more than {maxBytes} body bytes", frame);
        }

        transaction.Hold(frame);
    }

    // ADR-0118 decision 4: /queue/<q> is the bus queue <q>; anything else is a topic by the bus's
    // names for the scheme, aliases included, or an unknown destination.
    private BusDestination ResolveDestination(StompFrame frame, bool isFilter)
    {
        var destination = frame.Header("destination") ?? throw MissingHeader(frame, "destination");
        if (destination.StartsWith(QueueDestinationPrefix, StringComparison.Ordinal))
        {
            var queue = StompDestinationNames.ToPublishDestination(destination);
            return queue.IsMapped ? queue.Destination : throw UnknownDestination(frame);
        }

        var topic = isFilter
            ? bus.Names.ToCanonicalFilter(context.Scheme, destination)
            : bus.Names.ToCanonicalTopic(context.Scheme, destination);
        return topic.IsMapped ? new BusDestination(BusDestinationKind.Topic, topic.Name) : throw UnknownDestination(frame);
    }

    private static void ThrowIfRefused(MessageBusOutcome outcome, StompFrame frame)
    {
        if (outcome is not MessageBusOutcome.Succeeded)
        {
            throw Violation(RefusalMessage(outcome), $"The bus refused {frame.Command} ({outcome})", frame);
        }
    }

    // ADR-0118 decisions 4 and 8: every other bound of the bus is a resource limit.
    private static string RefusalMessage(MessageBusOutcome outcome) => outcome switch
    {
        MessageBusOutcome.AccessDenied => StompErrors.AccessRefused,
        MessageBusOutcome.TooManySubscriptions => StompErrors.TooManySubscriptions,
        MessageBusOutcome.MessageTooLarge => StompErrors.FrameTooLarge,
        _ => StompErrors.ResourceLimitExceeded,
    };

    private static StompProtocolViolationException MissingHeader(StompFrame frame, string header) => Violation(
        header == "destination" ? StompErrors.MissingDestinationHeader : StompErrors.MissingIdHeader,
        $"{frame.Command} carried no {header} header",
        frame);

    private static StompProtocolViolationException MissingTransactionHeader(StompFrame frame) =>
        Violation(StompErrors.MissingTransactionHeader, $"{frame.Command} carried no transaction header", frame);

    private static StompProtocolViolationException UnknownTransaction(StompFrame frame) =>
        Violation(StompErrors.UnknownTransaction, $"{frame.Command} named no open transaction", frame);

    private static StompProtocolViolationException UnknownDestination(StompFrame frame) =>
        Violation(StompErrors.UnknownDestination, $"{frame.Command} named a destination that is neither /topic/ nor /queue/", frame);

    private static StompProtocolViolationException Violation(string message, string reason, StompFrame frame) =>
        new(message, reason, frame.Header("receipt"));
}
