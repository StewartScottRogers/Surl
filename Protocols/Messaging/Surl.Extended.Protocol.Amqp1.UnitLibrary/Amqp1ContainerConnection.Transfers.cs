using System.Buffers;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The container as receiver: credit to client senders, their transfers joined into messages, stored on
/// the bus and settled (Part 2 sections 2.6.7, 2.6.12, 2.6.14 and 2.7.5; ADR-0107 decisions 5 and 6).
/// </summary>
public sealed partial class Amqp1ContainerConnection
{
    /// <summary>The link credit the container gives a client sender (ADR-0107 decision 5).</summary>
    public const uint LinkCredit = 100;

    /// <summary>The credit left at which the container raises a client sender's credit back to <see cref="LinkCredit"/>.</summary>
    public const uint LinkCreditRefillAt = 50;

    private const uint StandardMessageFormat = 0;

    private long partialBytes;

    private async ValueTask<bool> AnswerTransferAsync(Amqp1Session session, Amqp1Transfer transfer, ReadOnlyMemory<byte> payload)
    {
        session.NextIncomingId++;
        if (await AttachedLinkAsync(session, transfer.Handle) is not { } link || link.IsDetachedByContainer)
        {
            return true;
        }

        if (!link.ContainerReceives)
        {
            return await DetachWithErrorAsync(session, link, Error(Amqp1ErrorCondition.IllegalState, "a transfer on a link the client receives on"));
        }

        if (link.Partial is null)
        {
            return await StartDeliveryAsync(session, link, transfer, payload);
        }

        return link.Partial.IsContinuedBy(transfer)
            ? await ContinueDeliveryAsync(session, link, transfer, payload)
            : await DetachWithErrorAsync(session, link, Error(Amqp1ErrorCondition.InvalidField, "a continuation of another delivery"));
    }

    private async ValueTask<bool> StartDeliveryAsync(Amqp1Session session, Amqp1Link link, Amqp1Transfer transfer, ReadOnlyMemory<byte> payload)
    {
        if (transfer.DeliveryId is not { } deliveryId)
        {
            return await DetachWithErrorAsync(session, link, Error(Amqp1ErrorCondition.InvalidField, "a continuation with no delivery open"));
        }

        // Part 2 section 2.6.7: no credit left when the sender's delivery-count has reached the limit given.
        if (CreditLeft(link) == 0)
        {
            return await DetachWithErrorAsync(session, link, Error(Amqp1ErrorCondition.TransferLimitExceeded, "a transfer beyond link credit"));
        }

        link.DeliveryCount++;
        link.Partial = new Amqp1IncomingDelivery(deliveryId, transfer.DeliveryTag, transfer.MessageFormat ?? StandardMessageFormat)
        {
            Settled = link.SenderSettles,
        };
        if (link.DeliveryLimit - link.DeliveryCount <= LinkCreditRefillAt)
        {
            link.DeliveryLimit = link.DeliveryCount + LinkCredit;
            await WriteLinkFlowAsync(session, link);
        }

        return await ContinueDeliveryAsync(session, link, transfer, payload);
    }

    private async ValueTask<bool> ContinueDeliveryAsync(Amqp1Session session, Amqp1Link link, Amqp1Transfer transfer, ReadOnlyMemory<byte> payload)
    {
        var delivery = link.Partial!;
        delivery.Settled |= transfer.Settled == true;
        delivery.State = transfer.State ?? delivery.State;
        if (transfer.Aborted)
        {
            // Part 2 section 2.7.5: nothing of an aborted delivery is stored or settled.
            DiscardPartial(link);
            return true;
        }

        delivery.Bytes.Write(payload.Span);
        partialBytes += payload.Length;
        if (delivery.Bytes.WrittenCount > bus.MaxMessageBytes)
        {
            return await DetachWithErrorAsync(session, link, Error(Amqp1ErrorCondition.MessageSizeExceeded, "message size exceeded"));
        }

        if (partialBytes > bus.MaxMessageBytes)
        {
            return await CloseWithErrorAsync(Error(Amqp1ErrorCondition.ResourceLimitExceeded, "partial deliveries exceed the message size"));
        }

        if (!transfer.More)
        {
            DiscardPartial(link);
            await CompleteDeliveryAsync(session, link, delivery);
        }

        return true;
    }

    // A whole message: a declare or discharge on a coordinator link, held in a transaction when its
    // state names one (ADR-0146 decision 1.4), otherwise stored at once.
    private async ValueTask CompleteDeliveryAsync(Amqp1Session session, Amqp1Link link, Amqp1IncomingDelivery delivery)
    {
        if (link.IsCoordinator)
        {
            await AnswerUnlessSettledAsync(session, delivery, await ControlAsync(link, Amqp1TransactionControl.Read(delivery.Bytes.WrittenMemory)));
        }
        else if (Amqp1DeliveryState.IsTransactional(delivery.State))
        {
            await AnswerUnlessSettledAsync(session, delivery, PostInTransaction(link, delivery));
        }
        else
        {
            await SettleReceivedAsync(session, delivery, await StoreAsync(link, delivery));
        }
    }

    // The credit a client sender has left; none once its delivery-count reaches, or a flow of its passes, the limit given.
    private static uint CreditLeft(Amqp1Link link) => (uint)Math.Max(unchecked((int)(link.DeliveryLimit - link.DeliveryCount)), 0);

    private void DiscardPartial(Amqp1Link link)
    {
        partialBytes -= link.Partial?.Bytes.WrittenCount ?? 0;
        link.Partial = null;
    }

    // ADR-0107 decision 5: rcv-settle-mode first; a delivery the client settled gets no disposition.
    private async ValueTask SettleReceivedAsync(Amqp1Session session, Amqp1IncomingDelivery delivery, Amqp1Error? rejection)
    {
        if (delivery.Settled)
        {
            if (rejection is not null)
            {
                context.Log.Note($"A pre-settled message was dropped: {rejection.Condition.Value}: {rejection.Description}.");
            }

            return;
        }

        await AnswerUnlessSettledAsync(session, delivery, rejection is null ? Amqp1DeliveryState.Accepted : Amqp1DeliveryState.Rejected(rejection));
    }

    // Part 2 section 2.6.12: a delivery the client settled is answered by nothing.
    private async ValueTask AnswerUnlessSettledAsync(Amqp1Session session, Amqp1IncomingDelivery delivery, Amqp1Described state)
    {
        if (!delivery.Settled)
        {
            await WriteAsync(session.OutgoingChannel, new Amqp1Disposition(IsReceiver: true, delivery.DeliveryId) { Settled = true, State = state });
        }
    }

    // Stores the message on its node; null when the bus holds it, or the error that rejects it.
    private async ValueTask<Amqp1Error?> StoreAsync(Amqp1Link link, Amqp1IncomingDelivery delivery)
    {
        var (entry, refusal) = ReadMessage(link, delivery);
        if (entry is null)
        {
            return refusal;
        }

        var outcome = entry.QueueName is { } queueName ? bus.PublishToQueue(queueName, entry.Message, accountName) : busConnection.Publish(entry.Message).Outcome;
        await SaveBusAsync();
        return StoreRejection(outcome);
    }

    // The message as the bus entry that stores it on the link's node, or the error that rejects it.
    private (BusBatchEntry? Entry, Amqp1Error? Refusal) ReadMessage(Amqp1Link link, Amqp1IncomingDelivery delivery)
    {
        if (delivery.MessageFormat != StandardMessageFormat)
        {
            return (null, Error(Amqp1ErrorCondition.NotImplemented, "message format not supported"));
        }

        var isTopic = Amqp1NodeAddress.IsTopic(link.NodeAddress);
        var name = link.NodeAddress[(isTopic ? Amqp1NodeAddress.TopicPrefix : Amqp1NodeAddress.QueuePrefix).Length..];
        try
        {
            var message = Amqp1BusMessages.ToBusMessage(delivery.Bytes.WrittenMemory, name, delivery.Settled, context.TimeProvider.GetUtcNow());
            return (isTopic ? BusBatchEntry.ToTopic(message) : BusBatchEntry.ToQueue(name, message), null);
        }
        catch (Amqp1DecodeException)
        {
            return (null, Error(Amqp1ErrorCondition.DecodeError, "a message that does not decode"));
        }
    }

    // ADR-0100 decision 3.7: a failed persistence write is noted, never answered as an error.
    private async ValueTask SaveBusAsync()
    {
        var saved = await bus.SaveChangesAsync(CancellationToken);
        if (saved.Outcome != MessageBusOutcome.Succeeded)
        {
            context.Log.Note("The message bus could not save its files; the message is held in memory.");
        }
    }

    private static Amqp1Error? StoreRejection(MessageBusOutcome outcome) => outcome switch
    {
        MessageBusOutcome.Succeeded => null,
        MessageBusOutcome.AccessDenied => Error(Amqp1ErrorCondition.UnauthorizedAccess, "not authorized"),
        MessageBusOutcome.StoreFull or MessageBusOutcome.TooManyTopics or MessageBusOutcome.QueueFull
            or MessageBusOutcome.MessageTooLarge => Error(Amqp1ErrorCondition.ResourceLimitExceeded, "resource limit exceeded"),
        _ => Error(Amqp1ErrorCondition.NotFound, "no such node"),
    };
}
