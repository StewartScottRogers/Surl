using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// An <see cref="IFaultPlan"/> that spends each rule's <see cref="FaultRule.Times"/> as the engine's
/// matcher does (ADR-0124, decision 4), so a test can show <c>times=1</c> fires once. A rule matches a
/// point when its scheme and account are unset or equal the point's, ordinally.
/// </summary>
internal sealed class SpendingFaultPlan(params FaultRule[] rules) : IFaultPlan
{
    private readonly int[] remaining = [.. rules.Select(rule => rule.Times ?? int.MaxValue)];

    public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken)
    {
        for (var index = 0; index < rules.Length; index++)
        {
            if (remaining[index] > 0 && Matches(rules[index], point))
            {
                remaining[index]--;
                return ValueTask.FromResult<FaultRule?>(rules[index]);
            }
        }

        return ValueTask.FromResult<FaultRule?>(null);
    }

    private static bool Matches(FaultRule rule, FaultPoint point) =>
        (rule.Scheme is null || rule.Scheme == point.Scheme) && (rule.Account is null || rule.Account == point.Account);
}
