using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Telnet;

/// <summary>
/// Asks the exchange's fault plan, at each line the client completes, whether to answer it with a
/// chosen error (ADR-0124, decision 4; ADR-0138): the point names the scheme only - TELNET has no
/// login, no command and no path, so a rule scoped by any of those never matches it.
/// </summary>
/// <remarks>
/// Only a <see cref="FaultKind.Error"/> rule whose reply fits (<see cref="Fits"/>) is applied, and
/// applying it publishes one <see cref="FaultAppliedEvent"/> whose target is <c>line &lt;n&gt;</c>,
/// the line's number in the session counted from 1 - never the line's text, which may be anything
/// a person typed. A rule of any other kind, or a reply that does not fit, is noted as not applied
/// and the line is answered as without it.
/// </remarks>
internal static class TelnetLineFaults
{
    /// <summary>
    /// Finds and applies the fault for a line: a delay holds it, a drop aborts the connection, and
    /// each publishes its <see cref="FaultAppliedEvent"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream and listen URL.</param>
    /// <param name="connection">The connection a drop aborts.</param>
    /// <param name="lineNumber">The line's number in the session, counted from 1.</param>
    /// <returns>
    /// The <see cref="FaultKind.Drop"/> rule (write nothing, end the session) or the fitting
    /// <see cref="FaultKind.Error"/> rule (send its reply as one line before closing), or
    /// <see langword="null"/> to answer as usual.
    /// </returns>
    public static async ValueTask<FaultRule?> FindAndApplyAsync(ExchangeContext context, IConnection connection, int lineNumber)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
        };
        var target = string.Create(CultureInfo.InvariantCulture, $"line {lineNumber}");
        var rule = await CommandStageFaults.FindAndApplyAsync(context, connection, point, target);
        if (rule is null or { Kind: FaultKind.Delay })
        {
            return null;
        }

        if (rule.Kind == FaultKind.Drop)
        {
            return rule;
        }

        if (rule.Kind != FaultKind.Error || !Fits(rule.Reply))
        {
            var why = rule.Kind == FaultKind.Error ? "its reply does not fit the telnet error shape" : $"a {rule.Kind} rule is not applied at a line";
            context.Log.Note($"Fault rule {rule.Id} not applied to {target}: {why}.");

            return null;
        }

        context.Log.Note($"{target} was answered with the chosen error of fault rule {rule.Id}; the connection was closed.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, target));

        return rule;
    }

    /// <summary>
    /// Whether a reply fits the <c>telnet</c> error shape: one line of non-empty text, with no CR
    /// or LF.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool Fits(string? reply) =>
        reply is { Length: > 0 } && reply.AsSpan().IndexOfAny('\r', '\n') < 0;
}
