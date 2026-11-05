namespace Surl.MessageBus;

/// <summary>
/// One message held in a named queue, with its place in the queue and whether a consumer has
/// been handed it before.
/// </summary>
internal sealed class QueuedMessage(long sequence, BusMessage message)
{
    public long Sequence { get; } = sequence;

    public BusMessage Message { get; } = message;

    public bool HandedOut { get; set; }
}
