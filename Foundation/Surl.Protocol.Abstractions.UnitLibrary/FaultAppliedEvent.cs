namespace Surl.Protocol.Abstractions;

/// <summary>
/// A fault rule was applied: the server misbehaved on purpose (ADR-0124, decision 4).
/// </summary>
/// <param name="RuleId">The applied rule's <see cref="FaultRule.Id"/>.</param>
/// <param name="Kind">The rule's kind.</param>
/// <param name="Stage">The stage it was applied at.</param>
/// <param name="ExchangeId">The exchange it was applied to, or <see langword="null"/> when none is known.</param>
/// <param name="Target">What it was applied to: the listen URL, account, path or command the point named.</param>
public sealed record FaultAppliedEvent(
    string RuleId,
    FaultKind Kind,
    FaultStage Stage,
    long? ExchangeId,
    string Target) : SurlEvent;
