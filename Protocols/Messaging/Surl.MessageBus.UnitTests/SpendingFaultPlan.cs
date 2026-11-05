using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// A fault plan that matches as ADR-0124 decision 4's matcher does for what the bus asks - the
/// stage when the rule names one, the account, and the path equal or as a segment prefix (the
/// point's path continues with <c>/</c>), so <c>path=a</c> covers what the topic filter <c>a/#</c>
/// covers - and spends each rule's <see cref="FaultRule.Times"/> as it answers with it, so a
/// test can show a <c>times=1</c> rule fires once. Every point asked is recorded.
/// </summary>
internal sealed class SpendingFaultPlan(params FaultRule[] rules) : IFaultPlan
{
    private readonly Dictionary<string, int> spent = [];

    public List<FaultPoint> Points { get; } = [];

    public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken)
    {
        Points.Add(point);
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
        && (rule.Path is null || PathMatches(rule.Path, point.Path));

    private static bool PathMatches(string rulePath, string? pointPath) =>
        pointPath is not null
        && (pointPath == rulePath || pointPath.StartsWith(rulePath + "/", StringComparison.Ordinal));
}
