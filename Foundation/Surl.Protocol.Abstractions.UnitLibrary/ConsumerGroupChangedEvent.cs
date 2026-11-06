namespace Surl.Protocol.Abstractions;

/// <summary>
/// A consumer group's membership or generation changed (ADR-0145 decision 10, amending ADR-0124 decision 1).
/// </summary>
/// <param name="ExchangeId">The exchange whose request changed it, or <see langword="null"/> when none did (a member removed by the broker).</param>
/// <param name="Scheme">The scheme the group is served over (<c>kafka</c>).</param>
/// <param name="Group">The group id.</param>
/// <param name="Change">What happened.</param>
/// <param name="MemberId">The member the change is about, or <see langword="null"/> when it names none.</param>
/// <param name="GenerationId">The group's generation once the change was made.</param>
/// <param name="Account">The account the exchange logged in as, or <see langword="null"/> when none is known.</param>
public sealed record ConsumerGroupChangedEvent(
    long? ExchangeId,
    string Scheme,
    string Group,
    ConsumerGroupChange Change,
    string? MemberId,
    int GenerationId,
    string? Account) : SurlEvent;
