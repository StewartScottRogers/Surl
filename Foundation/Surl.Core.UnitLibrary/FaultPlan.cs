using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// The instance's fault plan (ADR-0124, decision 4): the rules that make its servers, stores and
/// login check misbehave on purpose, matched in the order they were added. Thread-safe: rules may
/// be added, removed and cleared while exchanges consult it.
/// </summary>
/// <remarks>
/// A rule matches a point when its stage - <see cref="FaultRule.Stage"/>, or its kind's default
/// stage when unset - is the point's, its count is not spent, and every scope attribute it sets
/// matches the point's: <see cref="FaultRule.Listener"/> the listen URL by scheme and host
/// (case-insensitively) and port, a port of 0 matching any bound port; <see cref="FaultRule.Scheme"/>
/// and <see cref="FaultRule.Command"/> case-insensitively; <see cref="FaultRule.Account"/>
/// ordinally, as the account book compares names; <see cref="FaultRule.Path"/> equal, or as a
/// segment prefix. The first match wins, and finding it spends one of its
/// <see cref="FaultRule.Times"/> atomically, so a rule with <c>times=1</c> is found exactly once
/// however many exchanges ask at once.
/// </remarks>
public sealed class FaultPlan : IFaultPlan
{
    /// <summary>
    /// The most rules one plan holds.
    /// </summary>
    public const int MaxRules = 256;

    /// <summary>
    /// The longest a <see cref="FaultKind.Delay"/> rule may wait, in milliseconds.
    /// </summary>
    public const int MaxDelayMilliseconds = 600_000;

    private readonly Lock rulesLock = new();
    private readonly List<PlannedRule> rules = [];
    private int lastRuleNumber;

    /// <summary>
    /// A snapshot of the rules, in the order they are tried, each with its
    /// <see cref="FaultRule.Times"/> as what is left of its count.
    /// </summary>
    public IReadOnlyList<FaultRule> Rules
    {
        get
        {
            lock (rulesLock)
            {
                return [.. rules.Select(planned => planned.Rule with { Times = planned.RemainingTimes })];
            }
        }
    }

    /// <summary>
    /// Adds a rule after every rule already in the plan, under the next identifier: <c>f1</c>,
    /// <c>f2</c> ..., never reused, whatever <see cref="FaultRule.Id"/> the rule was given.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The rule as added, with its identifier.</returns>
    /// <exception cref="ArgumentException">
    /// The rule's stage is not one its kind acts at, or a <see cref="FaultKind.Delay"/> rule has no
    /// <see cref="FaultRule.DelayMilliseconds"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="FaultRule.Times"/> is less than 1, <see cref="FaultRule.DelayMilliseconds"/> is
    /// outside 0 to <see cref="MaxDelayMilliseconds"/>, or <see cref="FaultRule.AfterBytes"/> is negative.
    /// </exception>
    /// <exception cref="InvalidOperationException">The plan already holds <see cref="MaxRules"/> rules.</exception>
    public FaultRule Add(FaultRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var stage = StageOf(rule);
        RefuseParameters(rule);

        lock (rulesLock)
        {
            if (rules.Count == MaxRules)
            {
                throw new InvalidOperationException($"A fault plan holds at most {MaxRules} rules.");
            }

            var added = rule with { Id = "f" + (++lastRuleNumber).ToString(CultureInfo.InvariantCulture) };
            rules.Add(new PlannedRule(added, stage, rule.Times));

            return added;
        }
    }

    /// <summary>
    /// Removes the rule with an identifier.
    /// </summary>
    /// <param name="ruleId">The rule's identifier.</param>
    /// <returns><see langword="true"/> when the plan held the rule.</returns>
    public bool Remove(string ruleId)
    {
        ArgumentNullException.ThrowIfNull(ruleId);

        lock (rulesLock)
        {
            return rules.RemoveAll(planned => planned.Rule.Id == ruleId) > 0;
        }
    }

    /// <summary>
    /// Removes every rule. Identifiers already given are still never reused.
    /// </summary>
    public void Clear()
    {
        lock (rulesLock)
        {
            rules.Clear();
        }
    }

    /// <inheritdoc/>
    /// <remarks>Completes synchronously. A rule found with a count has one of its times spent.</remarks>
    public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(point);

        lock (rulesLock)
        {
            var found = rules.Find(planned => planned.RemainingTimes != 0 && Matches(planned, point));

            if (found?.RemainingTimes is { } remaining)
            {
                found.RemainingTimes = remaining - 1;
            }

            return ValueTask.FromResult(found?.Rule);
        }
    }

    // The stage a rule acts at: the one it names, which its kind must allow, or its kind's default.
    private static FaultStage StageOf(FaultRule rule)
    {
        var defaultStage = DefaultStageOf(rule.Kind);
        var stage = rule.Stage ?? defaultStage;
        var allowed = stage == defaultStage || (stage == FaultStage.Command && rule.Kind is FaultKind.Delay or FaultKind.Drop);

        return allowed
            ? stage
            : throw new ArgumentException($"A {rule.Kind} fault rule cannot act at the {stage} stage.", nameof(rule));
    }

    private static FaultStage DefaultStageOf(FaultKind kind) =>
        kind switch
        {
            FaultKind.Delay or FaultKind.Drop => FaultStage.Accept,
            FaultKind.RefuseLogin => FaultStage.Login,
            FaultKind.Error => FaultStage.Command,
            _ => FaultStage.StoreWrite,
        };

    private static void RefuseParameters(FaultRule rule)
    {
        if (rule.Times is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rule), rule.Times, "A fault rule's times must be 1 or more.");
        }

        if (rule.AfterBytes is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rule), rule.AfterBytes, "A fault rule's after-bytes must be 0 or more.");
        }

        RefuseDelay(rule);
    }

    private static void RefuseDelay(FaultRule rule)
    {
        if (rule.Kind == FaultKind.Delay && rule.DelayMilliseconds is null)
        {
            throw new ArgumentException("A Delay fault rule needs its delay in milliseconds.", nameof(rule));
        }

        if (rule.DelayMilliseconds is { } milliseconds && (uint)milliseconds > MaxDelayMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rule), rule.DelayMilliseconds, $"A fault rule's delay is 0 to {MaxDelayMilliseconds} milliseconds.");
        }
    }

    private static bool Matches(PlannedRule planned, FaultPoint point) =>
        planned.Stage == point.Stage
        && MatchesListener(planned.Rule.Listener, point.ListenUrl)
        && MatchesIgnoringCase(planned.Rule.Scheme, point.Scheme)
        && MatchesOrdinally(planned.Rule.Account, point.Account)
        && MatchesPath(planned.Rule.Path, point.Path)
        && MatchesIgnoringCase(planned.Rule.Command, point.Command);

    private static bool MatchesOrdinally(string? ruleValue, string? pointValue) =>
        ruleValue is null || string.Equals(ruleValue, pointValue, StringComparison.Ordinal);

    private static bool MatchesIgnoringCase(string? ruleValue, string? pointValue) =>
        ruleValue is null || string.Equals(ruleValue, pointValue, StringComparison.OrdinalIgnoreCase);

    // Equal, or a segment prefix: the point's path continues past the rule's with a '/'.
    private static bool MatchesPath(string? rulePath, string? pointPath) =>
        rulePath is null
        || (pointPath is not null
            && (string.Equals(rulePath, pointPath, StringComparison.Ordinal)
                || pointPath.StartsWith(rulePath.EndsWith('/') ? rulePath : rulePath + "/", StringComparison.Ordinal)));

    // The listen URL as given or as bound: scheme, host and port, a given port of 0 matching any.
    // A rule's listener that is not an absolute URL matches only the same text.
    private static bool MatchesListener(string? ruleListener, string? pointListenUrl) =>
        ruleListener is null
        || (pointListenUrl is not null
            && (string.Equals(ruleListener, pointListenUrl, StringComparison.Ordinal)
                || MatchesListenerParts(ruleListener, pointListenUrl)));

    private static bool MatchesListenerParts(string ruleListener, string pointListenUrl) =>
        Uri.TryCreate(ruleListener, UriKind.Absolute, out var rule)
        && Uri.TryCreate(pointListenUrl, UriKind.Absolute, out var point)
        && string.Equals(rule.Scheme, point.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(rule.Host, point.Host, StringComparison.OrdinalIgnoreCase)
        && (rule.Port == 0 || rule.Port == point.Port);

    // A rule, the stage it acts at and what is left of its count (null: always).
    private sealed class PlannedRule(FaultRule rule, FaultStage stage, int? remainingTimes)
    {
        public FaultRule Rule { get; } = rule;

        public FaultStage Stage { get; } = stage;

        public int? RemainingTimes { get; set; } = remainingTimes;
    }
}
