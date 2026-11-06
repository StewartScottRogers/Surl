namespace Surl.MessageBus;

/// <summary>
/// One named queue as a reset or a restore leaves it: its name and flags, the filters it is bound
/// to (<see langword="null"/> to keep the bindings it has), and its messages in publish order, each
/// with whether a consumer had been handed it.
/// </summary>
internal sealed record BusQueueSnapshot(
    string Name,
    BusQueueFlags Flags,
    IReadOnlyList<string>? Filters,
    IReadOnlyList<(BusMessage Message, bool Redelivered)> Messages);
