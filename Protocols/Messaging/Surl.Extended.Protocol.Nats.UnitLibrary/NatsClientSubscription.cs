using Surl.MessageBus;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// One SUB of one connection (ADR-0113 decision 4): its <c>sid</c>, its subject as the client sent
/// it, its bus subscription, and the count of messages it has delivered, which UNSUB's
/// <c>max_msgs</c> ends it at.
/// </summary>
/// <param name="sid">The subscription's identifier on the connection.</param>
/// <param name="subject">The subject, as the client sent it.</param>
internal sealed class NatsClientSubscription(string sid, string subject)
{
    private readonly Lock gate = new();
    private BusSubscription? busSubscription;
    private long delivered;
    private long? maxMessages;
    private bool ended;

    /// <summary>The subscription's identifier on the connection.</summary>
    public string Sid { get; } = sid;

    /// <summary>The subject, as the client sent it.</summary>
    public string Subject { get; } = subject;

    /// <summary>Whether it has ended, by UNSUB or by reaching its <c>max_msgs</c>.</summary>
    public bool HasEnded
    {
        get
        {
            lock (gate)
            {
                return ended;
            }
        }
    }

    /// <summary>Gives it the bus subscription made for it, which ending it disposes.</summary>
    public void Attach(BusSubscription subscription)
    {
        lock (gate)
        {
            busSubscription = subscription;
        }
    }

    /// <summary>
    /// Counts one delivery: false, and nothing counted, once it has ended; it ends with the
    /// delivery that reaches its <c>max_msgs</c>.
    /// </summary>
    public bool TryCountDelivery()
    {
        lock (gate)
        {
            if (ended)
            {
                return false;
            }

            delivered++;
            ended = delivered >= maxMessages;
        }

        EndOnBusIfEnded();
        return true;
    }

    /// <summary>
    /// UNSUB: ends it at once without <paramref name="max"/>, or once it has delivered
    /// <paramref name="max"/> messages in all - at once when it already has.
    /// </summary>
    /// <returns>Whether it has ended.</returns>
    public bool EndAfter(long? max)
    {
        lock (gate)
        {
            maxMessages = max;
            ended = max is null || delivered >= max;
        }

        EndOnBusIfEnded();
        return HasEnded;
    }

    private void EndOnBusIfEnded()
    {
        BusSubscription? ending;
        lock (gate)
        {
            ending = ended ? busSubscription : null;
        }

        ending?.Dispose();
    }
}
