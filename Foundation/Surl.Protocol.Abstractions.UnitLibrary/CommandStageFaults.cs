using System.Globalization;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// Asks an exchange's fault plan at a command (ADR-0124, decision 4; ADR-0141) and applies the two
/// kinds every server applies the same way: a <see cref="FaultKind.Delay"/> rule holds the command's
/// answer for the rule's <c>ms</c> on the exchange's <see cref="ExchangeContext.TimeProvider"/> with
/// its <see cref="ExchangeContext.IdleClock"/> paused, and a <see cref="FaultKind.Drop"/> rule takes
/// the place of the answer. Each publishes one <see cref="FaultAppliedEvent"/>. Any other kind - a
/// chosen <see cref="FaultKind.Error"/> - is handed back for the server to answer in its own shape.
/// <para>
/// The overloads that take an <see cref="IConnection"/> watch it during a hold and end the hold early
/// when it reports that the client closed or reset the connection (otherwise the hold lasts the whole
/// delay), and they abort it on a drop. The overload without one,
/// <see cref="FindAndApplyAsync(ExchangeContext, FaultPoint, string)"/>, watches no connection, so its
/// hold lasts the whole delay, and it hands a drop rule back for the caller to end the exchange.
/// Cancelling the exchange ends a hold in every overload.
/// </para>
/// </summary>
public static class CommandStageFaults
{
    // Read-ahead bound of a hold's watch for the client leaving (ADR-0141, amendment 2, decision 2).
    private const int PeerWatchBufferBytes = 64 * 1024;

    /// <summary>
    /// Finds the rule matching <paramref name="point"/> and applies it when it is a delay or a drop.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, clock, idle clock, log and events.</param>
    /// <param name="connection">The connection a drop aborts.</param>
    /// <param name="point">The command's fault point, at <see cref="FaultStage.Command"/>.</param>
    /// <param name="target">
    /// What a published <see cref="FaultAppliedEvent"/> names: the path, or else the verb. It is also the subject
    /// of the log notes when <paramref name="point"/> has no <see cref="FaultPoint.Command"/>.
    /// </param>
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

        return (await FindAndApplyWithClientOutcomeAsync(context, connection, point, target)).Rule;
    }

    /// <summary>
    /// <see cref="FindAndApplyAsync(ExchangeContext, IConnection, FaultPoint, string)"/> that also
    /// reports whether a <see cref="FaultKind.Delay"/> hold ended because the client left (ADR-0141, amendment 3).
    /// </summary>
    /// <param name="context">The exchange: its fault plan, clock, idle clock, log and events.</param>
    /// <param name="connection">The connection a drop aborts and a hold watches.</param>
    /// <param name="point">The command's fault point, at <see cref="FaultStage.Command"/>.</param>
    /// <param name="target">
    /// What a published <see cref="FaultAppliedEvent"/> names: the path, or else the verb. It is also the subject
    /// of the log notes when <paramref name="point"/> has no <see cref="FaultPoint.Command"/>.
    /// </param>
    /// <returns>The matching rule and, when the client left during a hold, how.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async ValueTask<CommandStageFaultResult> FindAndApplyWithClientOutcomeAsync(
        ExchangeContext context, IConnection connection, FaultPoint point, string target)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var result = await ApplyAsync(context, connection, point, target);
        if (result.Rule is { Kind: FaultKind.Drop })
        {
            connection.Abort();
        }

        return result;
    }

    /// <summary>
    /// <see cref="FindAndApplyAsync(ExchangeContext, IConnection, FaultPoint, string)"/> for a server
    /// that ends a dropped exchange itself - an HTTP/2 or HTTP/3 server resets the request's stream
    /// instead of aborting a connection other streams share (ADR-0141, amendment 1). A <see cref="FaultKind.Drop"/>
    /// rule is noted and published here and handed back; the caller sends nothing and ends the exchange.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, clock, idle clock, log and events.</param>
    /// <param name="point">The command's fault point, at <see cref="FaultStage.Command"/>.</param>
    /// <param name="target">
    /// What a published <see cref="FaultAppliedEvent"/> names: the path, or else the verb. It is also the subject
    /// of the log notes when <paramref name="point"/> has no <see cref="FaultPoint.Command"/>.
    /// </param>
    /// <returns>The matching rule, or <see langword="null"/> when none matched.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async ValueTask<FaultRule?> FindAndApplyAsync(ExchangeContext context, FaultPoint point, string target) =>
        (await ApplyAsync(context, null, point, target)).Rule;

    private static async ValueTask<CommandStageFaultResult> ApplyAsync(
        ExchangeContext context, IConnection? connection, FaultPoint point, string target)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(point);

        var clientLeft = PeerWatchOutcome.NotWatched;
        var rule = await context.Faults.FindAsync(point, context.CancellationToken);
        var noteSubject = point.Command ?? target;
        if (rule is { Kind: FaultKind.Delay })
        {
            clientLeft = await HoldAsync(context, connection, rule, noteSubject, target);
        }
        else if (rule is { Kind: FaultKind.Drop })
        {
            context.Log.Note($"{noteSubject} dropped the connection, unanswered, by fault rule {rule.Id}.");
            Publish(context, rule, target);
        }

        return new CommandStageFaultResult(rule, clientLeft);
    }

    private static async Task<PeerWatchOutcome> HoldAsync(
        ExchangeContext context, IConnection? connection, FaultRule rule, string noteSubject, string target)
    {
        var delay = TimeSpan.FromMilliseconds(rule.DelayMilliseconds ?? 0);
        context.Log.Note(
            $"{noteSubject} held for {delay.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s by fault rule {rule.Id}.");
        Publish(context, rule, target);
        using var holdEnded = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        // The wait starts before the pause, so whoever sees the pause knows the wait's timer is
        // already counting on the clock.
        var hold = Task.Delay(delay, context.TimeProvider, holdEnded.Token);
        var watch = connection is null ? null : WatchAsync(connection, holdEnded.Token);
        context.IdleClock.Pause();
        try
        {
            return await WaitForDelayOrPeerAsync(context, rule, hold, watch, holdEnded);
        }
        finally
        {
            context.IdleClock.Restart();
        }
    }

    private static async Task<PeerWatchOutcome> WaitForDelayOrPeerAsync(
        ExchangeContext context, FaultRule rule, Task hold, Task<PeerWatchOutcome>? watch, CancellationTokenSource holdEnded)
    {
        if (watch is not null && await Task.WhenAny(hold, watch) == watch)
        {
            var outcome = await watch;
            var left = outcome switch
            {
                PeerWatchOutcome.PeerClosed => "closed",
                PeerWatchOutcome.PeerReset => "reset",
                _ => null,
            };

            if (left is not null)
            {
                await holdEnded.CancelAsync();
                context.Log.Note($"Exchange {context.ExchangeId}: the client {left} the connection during the hold by fault rule {rule.Id}.");
                return outcome;
            }
        }

        try
        {
            await hold;
        }
        finally
        {
            holdEnded.Cancel();
        }

        if (watch is not null)
        {
            await watch;
        }

        return PeerWatchOutcome.NotWatched;
    }

    private static async Task<PeerWatchOutcome> WatchAsync(IConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.WatchForPeerCloseAsync(PeerWatchBufferBytes, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PeerWatchOutcome.NotWatched;
        }
        catch (IOException)
        {
            return PeerWatchOutcome.PeerReset;
        }
    }
    private static void Publish(ExchangeContext context, FaultRule rule, string target) =>
        context.Events.Publish(new FaultAppliedEvent(rule.Id, rule.Kind, FaultStage.Command, context.ExchangeId, target));
}
