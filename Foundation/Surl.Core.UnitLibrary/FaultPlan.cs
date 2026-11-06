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
/// <para>
/// An override (<see cref="AddOverride"/>, ADR-0124 decision 5) sits in the same order: at a point its
/// scope matches it is asked which rule applies, outside the plan's lock, and an answer of
/// <see langword="null"/> goes on to the next rule. While the plan holds no override,
/// <see cref="FindAsync"/> completes synchronously.
/// </para>
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
    private readonly List<PlannedEntry> rules = [];
    private int lastRuleNumber;
    private int overrideCount;

    /// <summary>
    /// A snapshot of the rules, in the order they are tried, each with its
    /// <see cref="FaultRule.Times"/> as what is left of its count. Overrides are not listed: they have
    /// no kind until asked.
    /// </summary>
    public IReadOnlyList<FaultRule> Rules
    {
        get
        {
            lock (rulesLock)
            {
                return [.. rules.OfType<PlannedRule>().Select(planned => planned.Rule with { Times = planned.RemainingTimes })];
            }
        }
    }

    /// <summary>
    /// Adds an override after every rule already in the plan, under the next identifier (ADR-0124,
    /// decision 5): at each point <paramref name="scope"/> matches, <paramref name="decide"/> is asked
    /// which rule applies there. The rule it answers is applied under the override's identifier.
    /// </summary>
    /// <param name="scope">The points the override is asked at.</param>
    /// <param name="decide">The decision; its caller bounds its time and catches what it throws.</param>
    /// <returns>The override's identifier, <c>f&lt;n&gt;</c>, which <see cref="Remove"/> takes.</returns>
    /// <exception cref="InvalidOperationException">The plan already holds <see cref="MaxRules"/> rules.</exception>
    public string AddOverride(FaultOverrideScope scope, FaultOverride decide)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(decide);

        lock (rulesLock)
        {
            var id = NextIdUnderLock();
            rules.Add(new PlannedOverride(id, scope, decide));
            overrideCount++;

            return id;
        }
    }

    /// <summary>
    /// The stage a rule acts at: the one it names, or its kind's default when it names none.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The stage.</returns>
    /// <exception cref="ArgumentException">The rule names a stage its kind cannot act at.</exception>
    public static FaultStage StageOf(FaultRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var defaultStage = DefaultStageOf(rule.Kind);
        var stage = rule.Stage ?? defaultStage;
        var allowed = stage == defaultStage || (stage == FaultStage.Command && rule.Kind is FaultKind.Delay or FaultKind.Drop);

        return allowed
            ? stage
            : throw new ArgumentException($"A {rule.Kind} fault rule cannot act at the {stage} stage.", nameof(rule));
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
            var added = rule with { Id = NextIdUnderLock() };
            rules.Add(new PlannedRule(added, stage, rule.Times));

            return added;
        }
    }

    /// <summary>
    /// Removes the rule or override with an identifier.
    /// </summary>
    /// <param name="ruleId">The rule's identifier.</param>
    /// <returns><see langword="true"/> when the plan held the rule.</returns>
    public bool Remove(string ruleId)
    {
        ArgumentNullException.ThrowIfNull(ruleId);

        lock (rulesLock)
        {
            overrideCount -= rules.Count(planned => planned.Id == ruleId && planned is PlannedOverride);
            return rules.RemoveAll(planned => planned.Id == ruleId) > 0;
        }
    }

    /// <summary>
    /// Removes every rule and override. Identifiers already given are still never reused.
    /// </summary>
    public void Clear()
    {
        lock (rulesLock)
        {
            rules.Clear();
            overrideCount = 0;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Completes synchronously while the plan holds no override. A rule found with a count has one of its
    /// times spent; a rule an override answers is returned under the override's identifier.
    /// </remarks>
    public ValueTask<FaultRule?> FindAsync(FaultPoint point, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(point);
        PlannedEntry[] entries;

        lock (rulesLock)
        {
            if (overrideCount == 0)
            {
                var found = rules.Find(planned => TrySpendUnderLock((PlannedRule)planned, point));
                return ValueTask.FromResult(((PlannedRule?)found)?.Rule);
            }

            entries = [.. rules];
        }

        return FindAskingOverridesAsync(entries, point, cancellationToken);
    }

    // Each entry in the order it stood when the search began; one removed meanwhile is skipped.
    private async ValueTask<FaultRule?> FindAskingOverridesAsync(
        PlannedEntry[] entries, FaultPoint point, CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            var found = entry is PlannedOverride planned
                ? await AskAsync(planned, point, cancellationToken)
                : TrySpend((PlannedRule)entry, point);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private async ValueTask<FaultRule?> AskAsync(PlannedOverride planned, FaultPoint point, CancellationToken cancellationToken)
    {
        if (!IsHeld(planned) || !planned.Matches(point))
        {
            return null;
        }

        var answer = await planned.Decide(planned.Id, point, cancellationToken);
        return answer is null ? null : answer with { Id = planned.Id };
    }

    private bool IsHeld(PlannedEntry entry)
    {
        lock (rulesLock)
        {
            return rules.Contains(entry);
        }
    }

    private FaultRule? TrySpend(PlannedRule planned, FaultPoint point)
    {
        lock (rulesLock)
        {
            return rules.Contains(planned) && TrySpendUnderLock(planned, point) ? planned.Rule : null;
        }
    }

    // Whether the rule matches the point with its count not spent; if so, spends one.
    private static bool TrySpendUnderLock(PlannedRule planned, FaultPoint point)
    {
        if (planned.RemainingTimes == 0 || !Matches(planned, point))
        {
            return false;
        }

        if (planned.RemainingTimes is { } remaining)
        {
            planned.RemainingTimes = remaining - 1;
        }

        return true;
    }

    // The next identifier, f1, f2 ...; refuses past MaxRules. Called under the lock.
    private string NextIdUnderLock()
    {
        if (rules.Count == MaxRules)
        {
            throw new InvalidOperationException($"A fault plan holds at most {MaxRules} rules.");
        }

        return "f" + (++lastRuleNumber).ToString(CultureInfo.InvariantCulture);
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
        && MatchesScope(planned.Rule.Listener, planned.Rule.Scheme, planned.Rule.Account, point)
        && MatchesPath(planned.Rule.Path, point.Path)
        && MatchesIgnoringCase(planned.Rule.Command, point.Command);

    private static bool MatchesScope(string? listener, string? scheme, string? account, FaultPoint point) =>
        MatchesListener(listener, point.ListenUrl)
        && MatchesIgnoringCase(scheme, point.Scheme)
        && MatchesOrdinally(account, point.Account);

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

    // One place in the plan's order: a rule or an override, under its identifier.
    private abstract class PlannedEntry(string id)
    {
        public string Id { get; } = id;
    }

    // An override, the points it is asked at and its decision.
    private sealed class PlannedOverride(string id, FaultOverrideScope scope, FaultOverride decide) : PlannedEntry(id)
    {
        public FaultOverride Decide { get; } = decide;

        public bool Matches(FaultPoint point) =>
            (scope.Stage is null || scope.Stage == point.Stage)
            && MatchesScope(scope.Listener, scope.Scheme, scope.Account, point)
            && MatchesPath(scope.Path, point.Path)
            && MatchesIgnoringCase(scope.Command, point.Command);
    }

    // A rule, the stage it acts at and what is left of its count (null: always).
    private sealed class PlannedRule(FaultRule rule, FaultStage stage, int? remainingTimes) : PlannedEntry(rule.Id)
    {
        public FaultRule Rule { get; } = rule;

        public FaultStage Stage { get; } = stage;

        public int? RemainingTimes { get; set; } = remainingTimes;
    }
}
