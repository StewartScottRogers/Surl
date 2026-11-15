namespace Surl.MessageBus;

/// <summary>
/// One ready message of a queue as <see cref="MessageBus.BrowseQueue"/> lists it.
/// </summary>
/// <param name="Position">Its 0-based place among the queue's ready messages in delivery order
/// (high priority first, each level in publish order); a caller pages on from the last one seen.</param>
/// <param name="Message">The message as the bus holds it.</param>
public sealed record BusBrowsedMessage(int Position, BusMessage Message);
