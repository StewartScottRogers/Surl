using Surl.Cli;
using Surl.Composition;
using Surl.Core;

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

    internal SurlServerFaults(FaultPlan plan) => this.plan = plan;

    /// <summary>
    /// Adds <paramref name="fault"/> after every rule already in the plan, read by <c>surl</c>'s own
    /// <c>--fault</c> reader, so a rule <c>surl</c> would refuse at start is refused here with the same text.
    /// </summary>
    /// <param name="fault">The rule.</param>
    /// <returns>The rule's identifier, <c>f&lt;n&gt;</c>, never reused by this server.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fault"/> is null.</exception>
    /// <exception cref="ArgumentException"><c>surl</c> would refuse the rule; the message is <c>surl</c>'s, e.g. <c>option --fault: kind error needs key scheme or key listener</c>.</exception>
    /// <exception cref="InvalidOperationException">The plan already holds 256 rules.</exception>
    public string Add(SurlFault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        var parsed = CommandLineParser.Parse([ParsedBesideListenUrl, "--fault", fault.ToItem()]);
        if (parsed.Outcome != CommandLineOutcome.Serve)
        {
            throw new ArgumentException(parsed.Failure!.Message, nameof(fault));
        }

        return plan.Add(FaultPlanComposition.ToRule(parsed.CommandLine!.Faults[0])).Id;
    }

    /// <summary>Removes the rule with identifier <paramref name="ruleId"/>, a start rule included.</summary>
    /// <param name="ruleId">The identifier <see cref="Add"/> returned, or <c>f&lt;n&gt;</c> for the n-th start rule.</param>
    /// <returns><see langword="true"/> when the plan held the rule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ruleId"/> is null.</exception>
    public bool Remove(string ruleId)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        return plan.Remove(ruleId);
    }

    /// <summary>Removes every rule, start rules included; identifiers already given are still never reused.</summary>
    public void Clear() => plan.Clear();
}
