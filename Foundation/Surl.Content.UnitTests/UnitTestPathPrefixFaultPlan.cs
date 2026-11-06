using Surl.Protocol.Abstractions;

namespace Surl.Content;

/// <summary>
/// A fault plan that matches as ADR-0124 decision 4's matcher does for a path-scoped rule - the
/// point's path equal to the rule's or under it as a segment prefix - and spends
/// <see cref="FaultRule.Times"/> once per application, so a store test can show a rule for
/// <c>/uploads</c> and <c>times=1</c> without <c>Surl.Core</c>'s <c>FaultPlan</c>.
/// </summary>
internal sealed class UnitTestPathPrefixFaultPlan(FaultRule rule) : IFaultPlan
{
    private int applied;

    public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken)
    {
        bool isUnderThePath = point.Path is not null
            && (point.Path == rule.Path || point.Path.StartsWith(rule.Path + "/", StringComparison.Ordinal));
        bool isSpent = rule.Times is int times && applied >= times;
        if (point.Stage != FaultStage.StoreWrite || !isUnderThePath || isSpent)
        {
            return ValueTask.FromResult<FaultRule?>(null);
        }

        applied++;
        return ValueTask.FromResult<FaultRule?>(rule);
    }
}
