namespace Surl.MessageBus;

/// <summary>
/// One named queue as a reset or a restore leaves it: its name and flags, the filters it is bound
/// to (<see langword="null"/> to keep the bindings it has), and its messages in publish order, each
/// with whether a consumer had been handed it, and its subscription description and excluded
/// publisher name. A queue made anew takes the flags. An existing queue takes the flags, losing any
/// owning connection, the description and the excluded publisher name only with the filters, and
/// keeps its own when they are <see langword="null"/>.
/// </summary>
internal sealed record BusQueueSnapshot(
    string Name,
    BusQueueFlags Flags,
    IReadOnlyList<string>? Filters,
    IReadOnlyList<(BusMessage Message, bool Redelivered)> Messages,
    string? SubscriptionDescription = null,
    string? ExcludedPublisherName = null);
