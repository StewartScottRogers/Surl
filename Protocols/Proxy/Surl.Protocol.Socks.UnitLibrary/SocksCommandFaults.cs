using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// Asks the exchange's fault plan, at a SOCKS <c>CONNECT</c> that is about to be tried, whether to
/// hold it, drop the connection or answer it with a chosen error (ADR-0124 decision 4 and 9): the
/// point names the listener's scheme, the command <c>CONNECT</c> and the target <c>host:port</c> as
/// its path. A <see cref="FaultKind.Delay"/> or <see cref="FaultKind.Drop"/> rule is applied through
/// <see cref="CommandStageFaults"/>. A <see cref="FaultKind.Error"/> rule whose reply fits the
/// listener's version - a SOCKS4 reply code 91 to 93, a SOCKS5 <c>REP</c> 1 to 8, written alone - is
/// answered as the failed <c>CONNECT</c>'s reply and publishes one <see cref="FaultAppliedEvent"/>;
/// any other rule is noted as not applied and the request is served as without it.
/// </summary>
internal static class SocksCommandFaults
{
    /// <summary>The command a fault point names: the only one SOCKS connects.</summary>
    public const string ConnectCommand = "CONNECT";

    /// <summary>
    /// Finds the fault for a <c>CONNECT</c> to <paramref name="target"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, clocks, event stream, listen URL and log.</param>
    /// <param name="client">The connection a drop aborts.</param>
    /// <param name="target">The target, <c>host:port</c>.</param>
    /// <returns>
    /// The error rule, or the drop rule (the connection is already aborted), or <see langword="null"/>
    /// to serve as usual - after a delay too.
    /// </returns>
    public static async ValueTask<FaultRule?> FindAsync(ExchangeContext context, IConnection client, string target)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Path = target,
            Command = ConnectCommand,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, client, point, target);
        if (rule is null or { Kind: FaultKind.Delay })
        {
            return null;
        }

        if (rule.Kind == FaultKind.Drop)
        {
            return rule;
        }

        if (rule.Kind != FaultKind.Error || !TryReadReplyCode(context.Scheme, rule.Reply, out _))
        {
            var why = rule.Kind == FaultKind.Error ? $"its reply does not fit the {context.Scheme} error shape" : $"a {rule.Kind} rule is not applied at a command";
            context.Log.Note($"Fault rule {rule.Id} not applied to {ConnectCommand} {target}: {why}");

            return null;
        }

        context.Events.Publish(new FaultAppliedEvent(rule.Id, rule.Kind, FaultStage.Command, context.ExchangeId, target));

        return rule;
    }

    /// <summary>
    /// Reads a chosen-error reply for a listener of <paramref name="scheme"/>: the decimal reply
    /// code alone, 91 to 93 for <c>socks4</c> and <c>socks4a</c>, 1 to 8 for <c>socks5</c> and <c>socks5h</c>.
    /// </summary>
    /// <param name="scheme">The listener's scheme.</param>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <param name="code">The code: SOCKS4's <c>CD</c> byte, or SOCKS5's <c>REP</c>.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool TryReadReplyCode(string scheme, string? reply, out byte code)
    {
        var isSocks5 = scheme.StartsWith("socks5", StringComparison.OrdinalIgnoreCase);
        var (lowest, highest) = isSocks5 ? (1, 8) : (91, 93);
        code = 0;
        if (!byte.TryParse(reply, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < lowest || number > highest)
        {
            return false;
        }

        code = number;

        return true;
    }
}
