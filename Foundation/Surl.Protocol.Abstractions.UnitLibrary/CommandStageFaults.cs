using System.Globalization;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// Asks an exchange's fault plan at a command (ADR-0124, decision 4; ADR-0141) and applies the two
/// kinds every server applies the same way: a <see cref="FaultKind.Delay"/> rule holds the command's
/// answer for the rule's <c>ms</c> on the exchange's <see cref="ExchangeContext.TimeProvider"/> with
/// its <see cref="ExchangeContext.IdleClock"/> paused, and a <see cref="FaultKind.Drop"/> rule aborts
/// the connection in place of the answer. Each publishes one <see cref="FaultAppliedEvent"/>. Any
/// other kind - a chosen <see cref="FaultKind.Error"/> - is handed back for the server to answer in
/// its own shape.
/// </summary>
public static class CommandStageFaults
{
    /// <summary>
    /// Finds the rule matching <paramref name="point"/> and applies it when it is a delay or a drop.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, clock, idle clock, log and events.</param>
    /// <param name="connection">The connection a drop aborts.</param>
    /// <param name="point">The command's fault point, at <see cref="FaultStage.Command"/>.</param>
    /// <param name="target">What a published <see cref="FaultAppliedEvent"/> names: the path, or else the verb.</param>
    /// <returns>
    /// The matching rule, or <see langword="null"/> when none matched. After a
    /// <see cref="FaultKind.Delay"/> the server answers the command as usual; after a
    /// <see cref="FaultKind.Drop"/> it sends nothing more and ends the session.
    /// </returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async ValueTask<FaultRule?> FindAndApplyAsync(
        ExchangeContext context, IConnection connection, FaultPoint point, string target)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var rule = await FindAndApplyAsync(context, point, target);
        if (rule is { Kind: FaultKind.Drop })
        {
            connection.Abort();
        }

        return rule;
    }

    /// <summary>
    /// <see cref="FindAndApplyAsync(ExchangeContext, IConnection, FaultPoint, string)"/> for a server
    /// that ends a dropped exchange itself - an HTTP/2 or HTTP/3 server resets the request's stream
    /// instead of aborting a connection other streams share (ADR-0141, amendment 1). A <see cref="FaultKind.Drop"/>
    /// rule is noted and published here and handed back; the caller sends nothing and ends the exchange.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, clock, idle clock, log and events.</param>
    /// <param name="point">The command's fault point, at <see cref="FaultStage.Command"/>.</param>
    /// <param name="target">What a published <see cref="FaultAppliedEvent"/> names: the path, or else the verb.</param>
    /// <returns>The matching rule, or <see langword="null"/> when none matched.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async ValueTask<FaultRule?> FindAndApplyAsync(ExchangeContext context, FaultPoint point, string target)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(point);

        var rule = await context.Faults.FindAsync(point, context.CancellationToken);
        if (rule is { Kind: FaultKind.Delay })
        {
            await HoldAsync(context, rule, point.Command, target);
        }
        else if (rule is { Kind: FaultKind.Drop })
        {
            context.Log.Note($"{point.Command} dropped the connection, unanswered, by fault rule {rule.Id}.");
            Publish(context, rule, target);
        }

        return rule;
    }

    private static async Task HoldAsync(ExchangeContext context, FaultRule rule, string? verb, string target)
    {
        var delay = TimeSpan.FromMilliseconds(rule.DelayMilliseconds ?? 0);
        context.Log.Note(
            $"{verb} held for {delay.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s by fault rule {rule.Id}.");
        Publish(context, rule, target);
        // The wait starts before the pause, so whoever sees the pause knows the wait's timer is
        // already counting on the clock.
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
