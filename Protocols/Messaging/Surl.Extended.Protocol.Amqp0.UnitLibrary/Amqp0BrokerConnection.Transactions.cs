namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The <c>tx</c> class (90) and RabbitMQ's publisher confirms (<c>confirm</c>, 85) (ADR-0112
/// decision 6): a transacted channel buffers its publishes and settlements until <c>commit</c>,
/// and a channel in confirm mode answers each publish with <c>basic.ack</c>, or <c>basic.nack</c>
/// when the bus refuses it.
/// </summary>
internal sealed partial class Amqp0BrokerConnection
{
    private static readonly Amqp0MethodPayload TxSelectOk = new(Amqp0TxMethodId.ClassId, Amqp0TxMethodId.SelectOk, ReadOnlyMemory<byte>.Empty);
    private static readonly Amqp0MethodPayload TxCommitOk = new(Amqp0TxMethodId.ClassId, Amqp0TxMethodId.CommitOk, ReadOnlyMemory<byte>.Empty);
    private static readonly Amqp0MethodPayload TxRollbackOk = new(Amqp0TxMethodId.ClassId, Amqp0TxMethodId.RollbackOk, ReadOnlyMemory<byte>.Empty);
    private static readonly Amqp0MethodPayload ConfirmSelectOk = new(Amqp0ConfirmMethodId.ClassId, Amqp0ConfirmMethodId.SelectOk, ReadOnlyMemory<byte>.Empty);

    // A channel is transacted or in confirm mode, never both: the other select is 406.
    private void SelectTransactions(Amqp0Channel channel)
    {
        if (channel.Confirming)
        {
            throw new Amqp0ChannelException(Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - channel in confirm mode");
        }

        channel.Transaction ??= new();
        SendMethod(channel.Number, TxSelectOk);
    }

    // The buffered work runs in order; nothing is atomic across queues (the tx class's rules).
    private void Commit(Amqp0Channel channel)
    {
        foreach (var work in TakeTransactionWork(channel))
        {
            if (work.Publish is { } publish)
            {
                Route(channel, publish);
            }
            else
            {
                Settle(work.Settled.Values, work.Acknowledge, work.Requeue);
            }
        }

        SendMethod(channel.Number, TxCommitOk);
    }

    private void RollBack(Amqp0Channel channel)
    {
        DiscardTransaction(channel);
        SendMethod(channel.Number, TxRollbackOk);
    }

    // The buffered publishes are dropped and the buffered settlements' deliveries are outstanding again.
    private void DiscardTransaction(Amqp0Channel channel)
    {
        var settled = TakeTransactionWork(channel).Where(work => work.Publish is null).SelectMany(work => work.Settled);
        lock (channel.Gate)
        {
            foreach (var (tag, delivery) in settled)
            {
                channel.Outstanding.Add(tag, delivery);
            }
        }
    }

    // Rules commit/transacted and rollback/transacted: a channel not transacted is 406.
    private List<Amqp0TransactedWork> TakeTransactionWork(Amqp0Channel channel)
    {
        var transaction = channel.Transaction
            ?? throw new Amqp0ChannelException(Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - channel not transacted");
        heldContentBytes -= transaction.PublishedBytes;
        return transaction.TakeWork();
    }

    private void SelectConfirms(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        var noWait = arguments.ReadBits(1)[0];
        if (channel.Transaction is not null)
        {
            throw new Amqp0ChannelException(Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - channel transacted");
        }

        channel.Confirming = true;
        if (!noWait)
        {
            SendMethod(channel.Number, ConfirmSelectOk);
        }
    }

    // Each publish is settled before the next frame is read, so each gets its own confirm, multiple
    // clear. A bound the bus refuses (506 outside confirm mode) is a nack; other faults stay exceptions.
    private void RouteConfirmed(Amqp0Channel channel, Amqp0PendingPublish publish)
    {
        var sequence = ++channel.LastPublishSequence;
        try
        {
            Route(channel, publish);
            SendMethod(channel.Number, MethodOf(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Ack, writer =>
            {
                writer.WriteLongLong(sequence);
                writer.WriteBits([false]);
            }));
        }
        catch (Amqp0ConnectionException exception) when (exception.ReplyCode == Amqp0ReplyCode.ResourceError)
        {
            context.Log.Note($"Publish {sequence} on channel {channel.Number} was nacked: {exception.Message}.");
            SendMethod(channel.Number, MethodOf(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Nack, writer =>
            {
                writer.WriteLongLong(sequence);
                writer.WriteBits([false, false]);
            }));
        }
    }
}
