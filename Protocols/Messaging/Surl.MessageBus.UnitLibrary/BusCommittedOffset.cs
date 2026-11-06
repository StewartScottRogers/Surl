namespace Surl.MessageBus;

/// <summary>
/// One committed offset the bus keeps for a Kafka consumer group (ADR-0145, decision 4): what
/// the group committed for one canonical topic, as given, and when. Replaced by the group's next
/// commit for the topic; never expired by time.
/// </summary>
/// <param name="Group">The consumer group's id, whatever it holds.</param>
/// <param name="Topic">The canonical topic name.</param>
/// <param name="Offset">The committed offset, as the client gave it.</param>
/// <param name="LeaderEpoch">The committed leader epoch, -1 when none was given.</param>
/// <param name="Metadata">The committed metadata, <see langword="null"/> kept null.</param>
/// <param name="CommittedAt">When it was committed, by the bus's <see cref="TimeProvider"/>.</param>
public sealed record BusCommittedOffset(
    string Group,
    string Topic,
    long Offset,
    int LeaderEpoch,
    string? Metadata,
    DateTimeOffset CommittedAt);
