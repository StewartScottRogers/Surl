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
    string Target) : SurlEvent
{
    /// <inheritdoc />
    public override long? ExchangeId { get; } = ExchangeId;

    /// <summary>
    /// How the connection was ended, set only on the serving engine's <see cref="FaultStage.Accept"/>-stage
    /// drop: <see cref="FaultClose.Reset"/> or <see cref="FaultClose.Graceful"/> (a datagram flow's drop is
    /// <see cref="FaultClose.Reset"/>); otherwise <see langword="null"/>.
    /// </summary>
    public FaultClose? Close { get; init; }

    /// <inheritdoc />
    public override void WriteMembers(ISurlEventMemberWriter writer)
    {
        writer.Text(nameof(RuleId), RuleId);
        writer.Choice(nameof(Kind), Kind);
        writer.Choice(nameof(Stage), Stage);
        writer.Number(nameof(ExchangeId), ExchangeId);
        writer.Text(nameof(Target), Target);

        if (Close is { } close)
        {
            writer.Choice(nameof(Close), close);
        }
    }
}
