using Surl.Cli;
using Surl.Core;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The instance's one fault plan, as the command line's <c>--fault</c> rules ask for it, and the
/// warning a plan with rules writes on start (ADR-0124, decision 4).
/// </summary>
/// <remarks>
/// Public because more than one host calls it, but not a supported programming interface: it may
/// change in any release (ADR-0099, decision 1).
/// </remarks>
public static class FaultPlanComposition
{
    /// <summary>
    /// Builds a new fault plan holding <paramref name="faults"/>, in their order, as rules
    /// <c>f1</c>, <c>f2</c> ...; never shared between instances (ADR-0099, decision 8).
    /// </summary>
    /// <param name="faults">The <c>--fault</c> rules, as <see cref="SurlCommandLine.Faults"/> holds them.</param>
    /// <returns>The plan.</returns>
    public static FaultPlan Compose(IReadOnlyList<ConfiguredFault> faults)
    {
        ArgumentNullException.ThrowIfNull(faults);
        var plan = new FaultPlan();
        foreach (var fault in faults)
        {
            plan.Add(ToRule(fault));
        }

        return plan;
    }

    /// <summary>
    /// Turns one configured <c>--fault</c> rule into the rule a plan holds; the plan gives it its
    /// identifier when it is added.
    /// </summary>
    /// <param name="fault">The configured rule.</param>
    /// <returns>The rule, with an empty identifier.</returns>
    public static FaultRule ToRule(ConfiguredFault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        return new FaultRule(string.Empty, fault.Kind)
        {
            Listener = fault.Listener,
            Scheme = fault.Scheme,
            Account = fault.Account,
            Path = fault.Path,
            Command = fault.Command,
            Stage = fault.Stage,
            DelayMilliseconds = fault.DelayMilliseconds,
            AfterBytes = fault.AfterBytes,
            Reply = fault.Reply,
            Times = fault.Times,
        };
    }

    /// <summary>
    /// Writes ADR-0124 decision 4's warning once when any <c>--fault</c> rule is configured, from
    /// the info level up, as the other loosening warnings are (ADR-0032 section 9, ADR-0033
    /// section 7): <c>surl: warning: --fault: &lt;n&gt; fault rule(s) make this server misbehave on purpose</c>.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="log">The log stream.</param>
    public static void WriteWarning(SurlCommandLine commandLine, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(log);
        if (commandLine.Faults.Count > 0 && commandLine.LogLevel >= LogLevel.Info)
        {
            log.WriteLine($"surl: warning: --fault: {commandLine.Faults.Count} fault rule(s) make this server misbehave on purpose");
        }
    }
}
