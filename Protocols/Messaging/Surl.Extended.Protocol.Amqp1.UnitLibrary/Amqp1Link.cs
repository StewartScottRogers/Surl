using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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

    // The delivery credit the consumer has left: what it was last given, less each delivery claimed since.
    private uint consumerCredit;

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

    /// <summary>Whether the link has ended, so nothing it held is the bus's to settle any more.</summary>
    public bool IsEnded { get; set; }

    /// <summary>When the container sends: the <c>txn-id</c> the client's last <c>flow</c> carried, whose transaction the deliveries sent now are acquired in; <see langword="null"/> when it carried none.</summary>
    public byte[]? AcquiringTransactionId { get; set; }

    /// <summary>The node's canonical address, <c>/topic/&lt;t&gt;</c> or <c>/queue/&lt;q&gt;</c>, once found.</summary>
    public string NodeAddress { get; set; } = string.Empty;

    /// <summary>The address of the dynamic node this link's attach made, which goes when the link ends or the attach is refused; <see langword="null"/> when it made none.</summary>
    public string? MadeDynamicNodeAddress { get; set; }

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

    /// <summary>When the container sends from a topic subscription held in a bus queue, a kept or a non-durable shared one: that queue's plan (ADR-0146 decision 3 and Amendment 4); <see langword="null"/> otherwise.</summary>
    public Amqp1SubscriptionQueue? SubscriptionQueue { get; set; }

    /// <summary>Whether the link only looked a kept subscription up (an attach with no source), so it consumes nothing.</summary>
    public bool IsSubscriptionLookup { get; set; }

    /// <summary>When the container sends from a live topic subscription: that subscription; <see langword="null"/> otherwise. A kept or non-durable shared subscription's link has none and uses <see cref="Consumer"/>.</summary>
    public BusSubscription? Subscription { get; set; }

    /// <summary>When the container sends from a bus queue, a queue node's or a kept or non-durable shared subscription's: the consumer of it; <see langword="null"/> otherwise, as for a live topic subscription or a kept subscription lookup.</summary>
    public BusQueueConsumer? Consumer { get; set; }

    /// <summary>When the container sends from a live topic subscription: the messages handed to the link that wait until it can send them, in order. A queue's messages wait in the queue instead (ADR-0146 Amendment 7).</summary>
    public ConcurrentQueue<Amqp1PendingMessage> Waiting { get; } = new();

    /// <summary>How many messages wait.</summary>
    public int WaitingCount => Volatile.Read(ref waitingCount);

    /// <summary>How many of a live topic subscription's messages were dropped because too many waited. No others are dropped or counted here: a link with a <see cref="Consumer"/>, a kept or non-durable shared subscription's included, has none wait on it.</summary>
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

    /// <summary>Takes the first waiting message; <see langword="null"/> when none waits.</summary>
    public Amqp1PendingMessage? TakeWaiting()
    {
        if (!Waiting.TryDequeue(out var message))
        {
            return null;
        }

        Interlocked.Decrement(ref waitingCount);
        return message;
    }

    /// <summary>Starts the bus consumer, if the link has one, with delivery credit 0, so the bus holds nothing for it until <see cref="GiveConsumerCredit"/> says otherwise (ADR-0146 Amendment 7 item 3).</summary>
    public void StartConsumer()
    {
        Consumer?.SetDeliveryCredit(0);
        Consumer?.Start();
    }

    /// <summary>
    /// Gives the bus consumer, if the link has one, this delivery credit: the most messages the bus holds
    /// for it unclaimed. Held ones past it go back to the queue. The bus is told only of a change.
    /// </summary>
    public void GiveConsumerCredit(uint credit)
    {
        if (Consumer is not null && credit != consumerCredit)
        {
            consumerCredit = credit;
            Consumer.SetDeliveryCredit(credit);
        }
    }

    /// <summary>Claims the delivery the bus has held longest for the link's consumer, using one of its credit; the link must have a consumer.</summary>
    /// <returns>Whether one was held.</returns>
    public bool TryClaimDelivery([NotNullWhen(true)] out BusQueueDelivery? delivery)
    {
        if (!Consumer!.TryClaimDelivery(out delivery))
        {
            return false;
        }

        consumerCredit--;
        return true;
    }

    /// <summary>Ends the link's subscription or consumer; a consumer's unsettled deliveries go back to the queue.</summary>
    public void EndSource()
    {
        Subscription?.Dispose();
        Consumer?.Dispose();
    }
}
