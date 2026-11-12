using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// The command-stage faults (ADR-0124, decision 4; ADR-0141) for TFTP: a well-formed read or write
/// request is asked about at a <see cref="FaultStage.Command"/> point, its command <c>RRQ</c> or
/// <c>WRQ</c> and its path the file name as the content store would map it, percent-decoded. TFTP has
/// no login, so the point names no account and an account-scoped rule never matches it. A matching
/// <see cref="FaultKind.Error"/> rule's reply, an error code 0 to 7 and its text, is sent as the
/// ERROR packet that answers the request, before the content store is touched; a
/// <see cref="FaultKind.Delay"/> rule holds the answer for the rule's <c>ms</c> on the exchange's
/// clock with its idle clock paused; a <see cref="FaultKind.Drop"/> rule sends nothing and ends the
/// exchange. TFTP has no <see cref="IConnection"/> for <see cref="CommandStageFaults"/> to abort, so
/// this applies the three kinds itself.
/// </summary>
internal static class TftpCommandStageFault
{
    /// <summary>
    /// Asks the exchange's fault plan about <paramref name="request"/> and applies a matching rule,
    /// noting and publishing it once.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream, log, clock and listen URL.</param>
    /// <param name="request">The parsed request.</param>
    /// <returns>
    /// The rule the server must act on: a <see cref="FaultKind.Drop"/> (send nothing, end) or a
    /// chosen <see cref="FaultKind.Error"/> (send <see cref="ErrorPacket"/> of its reply);
    /// <see langword="null"/> when none matched or after a delay, when the request is served as usual.
    /// </returns>
    public static async ValueTask<FaultRule?> FindAndApplyAsync(ExchangeContext context, TftpRequest request)
    {
        var path = Uri.UnescapeDataString(TftpFileName.ToRequestPath(request.FileName));
        var command = request.IsWrite ? "WRQ" : "RRQ";
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = FaultListenUrl.Text(context.ListenUrl),
            Scheme = context.Scheme,
            Path = path,
            Command = command,
        };
        var rule = await context.Faults.FindAsync(point, context.CancellationToken);
        var target = $"{command} {path}";
        switch (rule)
        {
            case { Kind: FaultKind.Delay }:
                await HoldAsync(context, rule, target);
                return null;
            case { Kind: FaultKind.Drop }:
                context.Log.Note($"{target} dropped, unanswered, by fault rule {rule.Id}.");
                Publish(context, rule, target);
                return rule;
            case { Kind: FaultKind.Error, Reply: { } reply }:
                context.Log.Note($"{target} answered by fault rule {rule.Id}: ERROR {reply}");
                Publish(context, rule, target);
                return rule;
            default:
                return null;
        }
    }

    /// <summary>
    /// The ERROR packet for a chosen-error reply: its leading error code, 0 to 7, and the text after
    /// the space that follows it. A reply that does not start that way - the configuration reader
    /// refuses one, but a plan built in code may hold it - is sent whole as the text of ERROR 0.
    /// </summary>
    /// <param name="reply">The rule's reply, e.g. <c>2 Quota frozen</c>.</param>
    /// <returns>The packet.</returns>
    public static byte[] ErrorPacket(string reply)
    {
        var parts = reply.Split(' ', 2);
        return int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var code) && code <= 7
            ? TftpPacket.ForError((TftpErrorCode)code, parts.Length == 2 ? parts[1] : "")
            : TftpPacket.ForError(TftpErrorCode.NotDefined, reply);
    }

    private static async Task HoldAsync(ExchangeContext context, FaultRule rule, string target)
    {
        var delay = TimeSpan.FromMilliseconds(rule.DelayMilliseconds ?? 0);
        context.Log.Note($"{target} held for {delay.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s by fault rule {rule.Id}.");
        Publish(context, rule, target);
        // The wait starts before the pause, as CommandStageFaults does, so its timer is counting already.
        var hold = Task.Delay(delay, context.TimeProvider, context.CancellationToken);
        context.IdleClock.Pause();
        try
        {
            await hold;
        }
        finally
        {
            context.IdleClock.Restart();
        }
    }

    private static void Publish(ExchangeContext context, FaultRule rule, string target) =>
        context.Events.Publish(new FaultAppliedEvent(rule.Id, rule.Kind, FaultStage.Command, context.ExchangeId, target));
}
