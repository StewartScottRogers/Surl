namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The basic class's <c>qos</c> windows (ADR-0112 decision 6): <c>prefetch-count</c> and
/// <c>prefetch-size</c> bound the outstanding deliveries of a channel's consumers together, or
/// with <c>global</c> of the whole connection. A delivery past a window waits here, still
/// unacknowledged on the bus, and is written once a settlement makes room; <c>basic.recover</c>
/// without <c>requeue</c> writes the channel's outstanding deliveries again ahead of them.
/// </summary>
internal sealed partial class Amqp0BrokerConnection
{
    // Guards every channel's Window, connectionWindow and waitingDeliveries. Always taken before a
    // channel's Gate, never while holding one.
    private readonly Lock windowGate = new();
    private readonly Amqp0DeliveryWindow connectionWindow = new();
    private readonly List<Amqp0WindowedDelivery> waitingDeliveries = [];

    private void AnswerQos(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        var prefetchSize = arguments.ReadLong();
        var prefetchCount = arguments.ReadShort();
        var global = arguments.ReadBits(1)[0];
        lock (windowGate)
        {
            var window = global ? connectionWindow : channel.Window;
            window.PrefetchSize = prefetchSize;
            window.PrefetchCount = prefetchCount;
        }

        SendMethod(channel.Number, QosOk);
        lock (windowGate)
        {
            WriteWaitingDeliveriesThatFit();
        }
    }

    // Called on the bus's thread. A delivery waits behind any of its channel's already waiting.
    private void DeliverWithinWindows(Amqp0WindowedDelivery delivery)
    {
        lock (windowGate)
        {
            if (waitingDeliveries.Exists(waiting => waiting.Channel == delivery.Channel) || !HasRoomFor(delivery))
            {
                waitingDeliveries.Add(delivery);
                return;
            }

            WriteWithinWindows(delivery);
        }
    }

    // Under the window gate.
    private bool HasRoomFor(Amqp0WindowedDelivery delivery) =>
        delivery.Channel.Window.HasRoomFor(delivery.BodyBytes) && connectionWindow.HasRoomFor(delivery.BodyBytes);

    // Under the window gate.
    private void WriteWithinWindows(Amqp0WindowedDelivery delivery)
    {
        delivery.Channel.Window.Add(delivery.BodyBytes);
        connectionWindow.Add(delivery.BodyBytes);
        WriteDelivery(delivery.Channel, delivery.Consumer, delivery.Delivery, delivery);
    }

    // Under the window gate: in the order they wait, each channel's until one of them still does not fit.
    private void WriteWaitingDeliveriesThatFit()
    {
        var stillWaiting = new HashSet<Amqp0Channel>();
        foreach (var delivery in waitingDeliveries.ToList())
        {
            if (stillWaiting.Contains(delivery.Channel) || !HasRoomFor(delivery))
            {
                stillWaiting.Add(delivery.Channel);
                continue;
            }

            waitingDeliveries.Remove(delivery);
            WriteWithinWindows(delivery);
        }
    }

    // Settled or taken back consumer deliveries leave their windows, and the waiting ones that now fit are written.
    private void LeaveWindows(IEnumerable<Amqp0OutstandingDelivery> deliveries)
    {
        lock (windowGate)
        {
            foreach (var windowed in deliveries.Select(delivery => delivery.Windowed).OfType<Amqp0WindowedDelivery>())
            {
                LeaveWindows(windowed);
            }

            WriteWaitingDeliveriesThatFit();
        }
    }

    // Under the window gate.
    private void LeaveWindows(Amqp0WindowedDelivery delivery)
    {
        delivery.Channel.Window.Remove(delivery.BodyBytes);
        connectionWindow.Remove(delivery.BodyBytes);
    }

    // recover without requeue: each delivery of a consumer still on the channel is written again to
    // it, first of all that wait, under a new tag marked redelivered; gets and cancelled consumers'
    // deliveries have no recipient left, so they go back to their queues.
    private void RedeliverToOriginalConsumers(Amqp0Channel channel, IEnumerable<Amqp0OutstandingDelivery> taken)
    {
        var byRecipient = taken.ToLookup(delivery => delivery.Windowed is { } windowed && channel.Consumers.ContainsValue(windowed.Consumer));
        var again = byRecipient[true].Select(delivery => delivery.Windowed!).ToList();
        lock (windowGate)
        {
            again.ForEach(LeaveWindows);
            waitingDeliveries.InsertRange(0, again.Select(windowed => windowed with { Delivery = windowed.Delivery with { Redelivered = true } }));
            WriteWaitingDeliveriesThatFit();
        }

        Settle(byRecipient[false], acknowledge: false, requeue: true);
    }

    // basic.cancel: the cancelled consumer's waiting deliveries go back to their queue for the others.
    private void RequeueWaitingDeliveries(Amqp0Consumer consumer)
    {
        List<Amqp0WindowedDelivery> requeued;
        lock (windowGate)
        {
            requeued = waitingDeliveries.FindAll(waiting => waiting.Consumer == consumer);
            waitingDeliveries.RemoveAll(waiting => waiting.Consumer == consumer);
        }

        requeued.ForEach(waiting => consumer.Bus.Reject(waiting.Delivery.DeliveryTag, requeue: true));
    }

    // The channel's close: its consumers, already disposed, put its waiting deliveries back on the bus.
    private void DropWaitingDeliveries(Amqp0Channel channel)
    {
        lock (windowGate)
        {
            waitingDeliveries.RemoveAll(waiting => waiting.Channel == channel);
        }
    }
}
