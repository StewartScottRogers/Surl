using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Asks the exchange's fault plan, for each Invocation or StreamInvocation that has an
/// <c>invocationId</c>, whether to delay it, drop the connection, or complete it with a chosen error
/// instead of calling the hub method (ADR-0124, decisions 4 and 9; ADR-0144; ADR-0141): the point
/// names the scheme (<c>signalr</c> or <c>signalrs</c>), the account the connection was negotiated
/// by, the hub's endpoint base as <c>path</c> (<c>/chat</c>) and the hub method as <c>command</c>, as sent.
/// </summary>
/// <remarks>
/// A non-blocking Invocation, which has no <c>invocationId</c>, gets no Completion to carry an error,
/// so the plan is not asked for one. A <see cref="FaultKind.Delay"/> or <see cref="FaultKind.Drop"/>
/// rule is applied, and so is an <see cref="FaultKind.Error"/> rule whose reply fits
/// (<see cref="Fits"/>); each publishes one <see cref="FaultAppliedEvent"/> naming the path. A rule
/// of any other kind, or a reply that does not fit, is noted as not applied and the method is
/// called as without it.
/// </remarks>
/// <param name="context">The exchange: its fault plan, event stream and listen URL.</param>
/// <param name="hubPath">The hub's endpoint base (<c>/chat</c>).</param>
/// <param name="accountName">The account that negotiated the connection, or <see langword="null"/> for the anonymous caller.</param>
internal sealed class SignalRInvocationFaults(ExchangeContext context, string hubPath, string? accountName)
{
    /// <summary>
    /// Offers an invocation to the fault plan and applies what it finds, publishing the
    /// <see cref="FaultAppliedEvent"/>: a <see cref="FaultKind.Delay"/> rule holds the call for its
    /// <c>ms</c> on the exchange's clock with the idle clock paused, a <see cref="FaultKind.Drop"/>
    /// rule ends the connection with nothing written, and an <see cref="FaultKind.Error"/> rule gives
    /// the Completion's <c>error</c> text.
    /// </summary>
    /// <param name="invocation">An invocation that has an <c>invocationId</c>.</param>
    /// <returns>What the invocation is answered with.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public async Task<SignalRInvocationFaultOutcome> ApplyAsync(HubInvocation invocation)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = string.IsNullOrEmpty(accountName) ? null : accountName,
            Path = hubPath,
            Command = invocation.Target,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, point, hubPath);

        return rule?.Kind switch
        {
            null or FaultKind.Delay => SignalRInvocationFaultOutcome.CalledAsUsual,
            FaultKind.Drop => SignalRInvocationFaultOutcome.Dropped,
            _ => ChooseError(rule),
        };
    }

    /// <summary>
    /// Whether a reply fits the <c>signalr</c> error shape: non-empty text, sent as the Completion's
    /// <c>error</c> string.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool Fits(string? reply) => !string.IsNullOrEmpty(reply);

    private SignalRInvocationFaultOutcome ChooseError(FaultRule rule)
    {
        if (rule.Kind != FaultKind.Error || !Fits(rule.Reply))
        {
            var why = rule.Kind == FaultKind.Error ? "its reply does not fit the signalr error shape" : $"a {rule.Kind} rule is not applied at an invocation";
            context.Log.Note($"Fault rule {rule.Id} not applied to the invocation: {why}.");

            return SignalRInvocationFaultOutcome.CalledAsUsual;
        }

        context.Log.Note($"The invocation was completed with the chosen error of fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, hubPath));

        return new SignalRInvocationFaultOutcome(rule.Reply, false);
    }
}
