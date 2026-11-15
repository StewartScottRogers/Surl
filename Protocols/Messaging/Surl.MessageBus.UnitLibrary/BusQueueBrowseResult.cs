namespace Surl.MessageBus;

/// <summary>
/// What <see cref="MessageBus.BrowseQueue"/> found: a queue's ready messages in delivery order,
/// read without consuming, acknowledging or reordering any of them (ADR-0164, decision 1).
/// </summary>
/// <param name="Outcome"><see cref="MessageBusOutcome.Succeeded"/>, or
/// <see cref="MessageBusOutcome.UnknownQueue"/> when no queue has the name.</param>
/// <param name="Messages">The ready messages from the asked position on, at most the limit and at
/// most <see cref="MessageBus.MaxBrowseBytes"/> of payload and native form together (the first is
/// listed whatever it counts); empty when refused.</param>
/// <param name="ReadyCount">How many messages wait for a consumer, as
/// <see cref="BusQueueInspectResult.ReadyMessageCount"/> says; zero when refused.</param>
/// <param name="InFlightCount">How many messages a consumer holds unacknowledged; zero when refused.</param>
/// <param name="HasMore">Whether ready messages after the last one listed were left out by the limit or
/// the byte bound.</param>
public sealed record BusQueueBrowseResult(
    MessageBusOutcome Outcome,
    IReadOnlyList<BusBrowsedMessage> Messages,
    int ReadyCount,
    int InFlightCount,
    bool HasMore);
