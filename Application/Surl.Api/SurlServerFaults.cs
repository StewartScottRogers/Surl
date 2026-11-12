using Surl.Cli;
using Surl.Composition;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A running <see cref="SurlServer"/>'s fault plan (ADR-0124, decision 4): the rules given at start in
/// <see cref="SurlServerOptions.Faults"/> as <c>f1</c>, <c>f2</c> ..., and the rules added, removed and cleared
/// while it serves. A change takes effect on the next point an exchange asks the plan at; each rule applied is
/// published as a <see cref="Protocol.Abstractions.FaultAppliedEvent"/>, and a rule added here writes nothing to
/// <see cref="SurlServerOptions.Log"/>. Thread-safe.
/// </summary>
public sealed class SurlServerFaults
{
    // The one listen URL the runtime rule is parsed beside: the reader checks the rule alone, never the listen URLs.
    private const string ParsedBesideListenUrl = "http://127.0.0.1:0";

    private readonly FaultPlan plan;
    private readonly ISurlEventPublisher events;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan hookTimeLimit;
    private readonly CustomProtocolServers custom;

    internal SurlServerFaults(FaultPlan plan, ISurlEventPublisher events, TimeProvider timeProvider, TimeSpan hookTimeLimit, CustomProtocolServers custom)
    {
        this.plan = plan;
        this.events = events;
        this.timeProvider = timeProvider;
        this.hookTimeLimit = hookTimeLimit;
        this.custom = custom;
    }

    /// <summary>
    /// Adds <paramref name="fault"/> after every rule already in the plan, read by <c>surl</c>'s own
    /// <c>--fault</c> reader, so a rule <c>surl</c> would refuse at start is refused here with the same text.
    /// </summary>
    /// <param name="fault">The rule.</param>
    /// <returns>The rule's identifier, <c>f&lt;n&gt;</c>, never reused by this server.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fault"/> is null.</exception>
    /// <exception cref="ArgumentException"><c>surl</c> would refuse the rule; the message is <c>surl</c>'s, e.g. <c>option --fault: kind error needs key scheme or key listener</c>, or an <c>error</c> rule is scoped to a custom protocol server's scheme (<see cref="SurlServerOptions.ProtocolServers"/>).</exception>
    /// <exception cref="InvalidOperationException">The plan already holds 256 rules.</exception>
    public string Add(SurlFault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        return plan.Add(custom.Read(fault, nameof(fault))).Id;
    }

    /// <summary>
    /// Adds a response override after every rule already in the plan (ADR-0124, decision 5): at each point
    /// <paramref name="scope"/> matches, <paramref name="decide"/> is asked which fault applies there, or
    /// <see langword="null"/> for none, and the plan goes on to its next rule. The answer is read by <c>surl</c>'s own
    /// <c>--fault</c> reader and applies to that point only - its scope and <see cref="SurlFault.Times"/> are
    /// ignored, and an error's reply shape is the point's scheme's - so an override can choose only what a fault rule
    /// can: it cannot accept a refused login, turn TLS off, reach outside the served root or widen an option.
    /// </summary>
    /// <remarks>
    /// The decision runs off the serving thread with <see cref="SurlServerOptions.HookTimeLimit"/> on the server's
    /// <see cref="SurlServerOptions.TimeProvider"/>. Past it, or when the decision throws or answers a fault
    /// <c>surl</c> refuses or that cannot act at the point's stage, a <see cref="HookFailedEvent"/> naming the
    /// override is published and the point is served as if the override were not there; the override stays added.
    /// </remarks>
    /// <param name="scope">The points the override is asked at.</param>
    /// <param name="decide">The decision; its token is cancelled when the time limit passes.</param>
    /// <returns>The override's identifier, <c>f&lt;n&gt;</c>, which <see cref="Remove"/> takes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> or <paramref name="decide"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="scope"/> gives both a listener and a scheme.</exception>
    /// <exception cref="InvalidOperationException">The plan already holds 256 rules.</exception>
    public string AddOverride(SurlFaultScope scope, Func<FaultPoint, CancellationToken, ValueTask<SurlFault?>> decide)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(decide);
        if (scope.Listener is not null && scope.Scheme is not null)
        {
            throw new ArgumentException("A response override's scope gives a listener or a scheme, not both.", nameof(scope));
        }

        var hook = new ResponseOverrideHook(decide, events, timeProvider, hookTimeLimit);
        var planScope = new FaultOverrideScope
        {
            Listener = scope.Listener,
            Scheme = scope.Scheme,
            Account = scope.Account,
            Path = scope.Path,
            Command = scope.Command,
            Stage = scope.Stage,
        };
        return plan.AddOverride(planScope, hook.DecideAsync);
    }

    /// <summary>The rule <paramref name="fault"/> spells, read by <c>surl</c>'s own <c>--fault</c> reader.</summary>
    /// <param name="fault">The rule.</param>
    /// <param name="parameterName">The parameter a refusal names.</param>
    /// <returns>The rule, its identifier not yet given.</returns>
    /// <exception cref="ArgumentException"><c>surl</c> would refuse the rule; the message is <c>surl</c>'s.</exception>
    internal static FaultRule Read(SurlFault fault, string parameterName)
    {
        var parsed = CommandLineParser.Parse([ParsedBesideListenUrl, "--fault", fault.ToItem()]);
        if (parsed.Outcome != CommandLineOutcome.Serve)
        {
            throw new ArgumentException(parsed.Failure!.Message, parameterName);
        }

        return FaultPlanComposition.ToRule(parsed.CommandLine!.Faults[0]);
    }

    /// <summary>Removes the rule or response override with identifier <paramref name="ruleId"/>, a start rule included.</summary>
    /// <param name="ruleId">The identifier <see cref="Add"/> or <see cref="AddOverride"/> returned, or <c>f&lt;n&gt;</c> for the n-th start rule.</param>
    /// <returns><see langword="true"/> when the plan held the rule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ruleId"/> is null.</exception>
    public bool Remove(string ruleId)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        return plan.Remove(ruleId);
    }

    /// <summary>
    /// The plan's rules as they stand, in the order they are tried (ADR-0124, decision 8's <c>list_faults</c>): each
    /// one's identifier, its <c>--fault</c> item without <c>times</c>, and how many more times it applies. A removed
    /// rule is not listed, a spent one is with 0 left; response overrides are not listed, having no kind until asked.
    /// </summary>
    /// <returns>A snapshot of the rules.</returns>
    public IReadOnlyList<SurlFaultRule> List() =>
        [.. plan.Rules.Select(rule => new SurlFaultRule(rule.Id, SurlFault.WithoutCountFrom(rule).ToItem(), rule.Times))];

    /// <summary>Removes every rule and response override, start rules included; identifiers already given are still never reused.</summary>
    public void Clear() => plan.Clear();
}
