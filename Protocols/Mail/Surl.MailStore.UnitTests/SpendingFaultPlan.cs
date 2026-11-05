using Surl.Protocol.Abstractions;

namespace Surl.MailStore;

/// <summary>
/// A fault plan that answers as <see cref="ScriptedFaultPlan"/> does but spends each rule's
/// <see cref="FaultRule.Times"/> as it answers with it, as ADR-0124 decision 4's matcher does, so a
/// store's test can show a <c>times=1</c> rule fires once.
/// </summary>
internal sealed class SpendingFaultPlan(params FaultRule[] rules) : IFaultPlan
{
    private readonly Dictionary<string, int> spent = [];

    public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken)
    {
        var rule = rules.FirstOrDefault(candidate => !IsSpent(candidate) && Matches(candidate, point));
        if (rule is not null)
        {
            spent[rule.Id] = spent.GetValueOrDefault(rule.Id) + 1;
        }

        return ValueTask.FromResult(rule);
    }

    private bool IsSpent(FaultRule rule) => rule.Times is { } times && spent.GetValueOrDefault(rule.Id) >= times;

    private static bool Matches(FaultRule rule, FaultPoint point) =>
        (rule.Stage is null || rule.Stage == point.Stage)
        && (rule.Account is null || rule.Account == point.Account)
        && (rule.Path is null || rule.Path == point.Path);
}
