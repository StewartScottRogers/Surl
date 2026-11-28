namespace Surl.MessageBus;

/// <summary>
/// One message held in a named queue, with its place in the queue and whether it has been handed
/// out to a consumer before (<see cref="HandedOut"/>), which a delivery only held until claimed
/// does not make it.
/// </summary>
internal sealed class QueuedMessage(long sequence, BusMessage message)
{
    public long Sequence { get; } = sequence;

    public BusMessage Message { get; } = message;

    public bool HandedOut { get; set; }

    /// <summary>The number of the file under <c>.surl/bus/queues/messages</c> that holds it;
    /// <see langword="null"/> until the bus saves it there (ADR-0100, decision 3.7).</summary>
    public ulong? FileNumber { get; set; }
}
