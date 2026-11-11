using System.Collections.Concurrent;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// One link attached in a session (Part 2 section 2.6): its handle, its node, and its credit and
/// deliveries (Part 2 sections 2.6.7 and 2.6.12; ADR-0107 decisions 5 and 6).
/// </summary>
/// <param name="containerHandle">The handle the container's frames for this link carry.</param>
internal sealed class Amqp1Link(uint containerHandle)
{
    private int waitingCount;

    private int droppedCount;

    /// <summary>The handle the container's frames for this link carry.</summary>
    public uint ContainerHandle => containerHandle;

    /// <summary>
    /// Whether the container has already sent its <c>detach</c> (a refused attach, or a link ended with an
    /// error) and waits only for the client's, which it then does not answer; the link's other frames are
    /// ignored meanwhile.
    /// </summary>
    public bool IsDetachedByContainer { get; set; }

    /// <summary>Whether the client sends on the link and the container receives.</summary>
    public bool ContainerReceives { get; set; }

    /// <summary>Whether the link's target is the transaction coordinator, so what the client sends on it declares and discharges transactions (Part 4 section 4.5.1).</summary>
    public bool IsCoordinator { get; set; }

    /// <summary>The node's canonical address, <c>/topic/&lt;t&gt;</c> or <c>/queue/&lt;q&gt;</c>, once found.</summary>
    public string NodeAddress { get; set; } = string.Empty;

    /// <summary>Whether the link's sender sends every delivery settled (<c>snd-settle-mode</c> <c>settled</c>).</summary>
    public bool SenderSettles { get; set; }

    /// <summary>Whether the client receiver settles second (<c>rcv-settle-mode</c> <c>second</c>).</summary>
    public bool ClientSettlesSecond { get; set; }

    /// <summary>The link's delivery-count: the sender's, as the container knows it.</summary>
    public uint DeliveryCount { get; set; }

    /// <summary>When the container receives: the delivery-count its credit runs to.</summary>
    public uint DeliveryLimit { get; set; }

    /// <summary>When the container sends: the client's link credit left.</summary>
    public uint Credit { get; set; }

    /// <summary>When the container receives: the delivery whose transfers are still arriving.</summary>
    public Amqp1IncomingDelivery? Partial { get; set; }

    /// <summary>When the container sends: the source filters it applies (ADR-0146 decision 2).</summary>
    public Amqp1SourceFilters Filters { get; set; } = Amqp1SourceFilters.None;

    /// <summary>When the container sends from a topic: the live subscription.</summary>
    public BusSubscription? Subscription { get; set; }

    /// <summary>When the container sends from a queue: the bus consumer.</summary>
    public BusQueueConsumer? Consumer { get; set; }

    /// <summary>When the container sends: the messages handed to the link that wait for credit, in order.</summary>
    public ConcurrentQueue<Amqp1PendingMessage> Waiting { get; } = new();

    /// <summary>How many messages wait.</summary>
    public int WaitingCount => Volatile.Read(ref waitingCount);

    /// <summary>How many topic messages were dropped because too many waited.</summary>
    public int DroppedCount => Volatile.Read(ref droppedCount);

    /// <summary>When the container sends: how many of its deliveries on the link wait for the client to settle them.</summary>
    public int UnsettledCount { get; set; }

    /// <summary>When the container sends: the delivery whose frames are still being written, the client's session window having closed.</summary>
    public Amqp1OutgoingTransfer? Sending { get; set; }

    /// <summary>The tag of the container's next delivery on the link.</summary>
    public ulong NextDeliveryTag { get; set; }

    /// <summary>
    /// Adds a message to <see cref="Waiting"/>; called on the bus's thread. A message past
    /// <paramref name="maxWaiting"/> is dropped and counted instead.
    /// </summary>
    /// <returns>Whether the message was added.</returns>
    public bool Offer(Amqp1PendingMessage message, int maxWaiting)
    {
        if (Interlocked.Increment(ref waitingCount) > maxWaiting)
        {
            Interlocked.Decrement(ref waitingCount);
            Interlocked.Increment(ref droppedCount);
            return false;
        }

        Waiting.Enqueue(message);
        return true;
    }

    /// <summary>Takes the first waiting message.</summary>
    public Amqp1PendingMessage Take()
    {
        Waiting.TryDequeue(out var message);
        Interlocked.Decrement(ref waitingCount);
        return message!;
    }

    /// <summary>Ends the link's subscription or consumer; a consumer's unsettled deliveries go back to the queue.</summary>
    public void EndSource()
    {
        Subscription?.Dispose();
        Consumer?.Dispose();
    }
}
