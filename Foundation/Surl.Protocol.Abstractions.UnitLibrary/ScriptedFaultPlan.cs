namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IFaultPlan"/> that answers from the rules it is given, so a protocol, store or login
/// test can set <see cref="ExchangeContext.Faults"/> without the engine's matcher. A rule matches a
/// point when its <see cref="FaultRule.Stage"/> is unset or the point's stage, and each scope attribute
/// it sets (<see cref="FaultRule.Listener"/>, <see cref="FaultRule.Scheme"/>,
/// <see cref="FaultRule.Account"/>, <see cref="FaultRule.Path"/>, <see cref="FaultRule.Command"/>)
/// equals the point's, ordinally. The first matching rule in the order given is returned; the
/// script never spends a rule's <see cref="FaultRule.Times"/>. Every point asked is recorded.
/// </summary>
public sealed class ScriptedFaultPlan : IFaultPlan
{
    private readonly FaultRule[] rules;
    private readonly List<FaultPoint> points = [];
    private readonly Lock pointsLock = new();

    /// <summary>
    /// Creates a plan that answers from <paramref name="rules"/>, in order.
    /// </summary>
    /// <param name="rules">The rules, tried in this order.</param>
    public ScriptedFaultPlan(params FaultRule[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        this.rules = [.. rules];
    }

    /// <summary>
    /// A snapshot of every point the plan was asked about, in order.
    /// </summary>
    public IReadOnlyList<FaultPoint> Points
    {
        get
        {
            lock (pointsLock)
            {
                return [.. points];
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(point);

        lock (pointsLock)
        {
            points.Add(point);
        }

        return ValueTask.FromResult(rules.FirstOrDefault(rule => Matches(rule, point)));
    }

    private static bool Matches(FaultRule rule, FaultPoint point) =>
        (rule.Stage is null || rule.Stage == point.Stage)
        && Matches(rule.Listener, point.ListenUrl)
        && Matches(rule.Scheme, point.Scheme)
        && Matches(rule.Account, point.Account)
        && Matches(rule.Path, point.Path)
        && Matches(rule.Command, point.Command);

    private static bool Matches(string? ruleValue, string? pointValue) =>
        ruleValue is null || string.Equals(ruleValue, pointValue, StringComparison.Ordinal);
}
