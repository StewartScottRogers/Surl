using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// Asks the exchange's fault plan, for each frame a logged-in client sends, whether to hold it, drop
/// the connection or answer it with a chosen error (ADR-0124 decisions 4 and 9; BL-1062): the point
/// names the scheme (<c>stomp</c> or <c>stomps</c>), the frame's command as <c>command</c>, its
/// <c>destination</c> header as <c>path</c>, and the logged-in account when there is one.
/// </summary>
/// <remarks>
/// A <see cref="FaultKind.Delay"/> rule holds the frame, a <see cref="FaultKind.Drop"/> rule is handed
/// back for the connection to abort, and an <see cref="FaultKind.Error"/> rule whose reply fits
/// (<see cref="IsErrorText"/>) is answered as an ERROR frame whose <c>message</c> is the reply, after
/// which the connection closes as STOMP 1.2 says. Each publishes one <see cref="FaultAppliedEvent"/>.
/// Any other rule is noted as not applied and the frame is served as without it.
/// </remarks>
internal static class StompFrameFaults
{
    /// <summary>
    /// Finds the fault for <paramref name="frame"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, clock, event stream, listen URL and log.</param>
    /// <param name="account">The logged-in account, or <see langword="null"/> for none.</param>
    /// <param name="frame">The frame, read but not yet answered.</param>
    /// <returns>
    /// The error or drop rule, or <see langword="null"/> to serve the frame as usual - after a delay too.
    /// </returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async ValueTask<FaultRule?> FindAsync(ExchangeContext context, string? account, StompFrame frame)
    {
        var path = frame.Header("destination");
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = account,
            Path = path,
            Command = frame.Command,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, point, path ?? frame.Command);
        if (rule is null or { Kind: FaultKind.Delay or FaultKind.Drop })
        {
            return rule is { Kind: FaultKind.Drop } ? rule : null;
        }

        if (rule.Kind != FaultKind.Error || !IsErrorText(rule.Reply))
        {
            var why = rule.Kind == FaultKind.Error ? "its reply does not fit the stomp error shape" : $"a {rule.Kind} rule is not applied at a frame";
            context.Log.Note($"Fault rule {rule.Id} not applied to {frame.Command}: {why}.");

            return null;
        }

        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, path ?? frame.Command));

        return rule;
    }

    /// <summary>
    /// Whether <paramref name="reply"/> fits the <c>stomp</c> error shape: text of at least one
    /// character with no carriage return, line feed or NUL, which no version's header could carry.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool IsErrorText(string? reply) => !string.IsNullOrEmpty(reply) && reply.IndexOfAny(['\r', '\n', '\0']) < 0;
}
