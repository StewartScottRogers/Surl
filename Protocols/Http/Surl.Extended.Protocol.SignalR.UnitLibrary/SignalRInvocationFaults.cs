using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Asks the exchange's fault plan, for each Invocation or StreamInvocation that has an
/// <c>invocationId</c>, whether to complete it with a chosen error instead of calling the hub method
/// (ADR-0124, decisions 4 and 9; ADR-0144): the point names the scheme (<c>signalr</c> or
/// <c>signalrs</c>), the account the connection was negotiated by, the hub's endpoint base as
/// <c>path</c> (<c>/chat</c>) and the hub method as <c>command</c>, as sent.
/// </summary>
/// <remarks>
/// A non-blocking Invocation, which has no <c>invocationId</c>, gets no Completion to carry an error,
/// so the plan is not asked for one. Only a <see cref="FaultKind.Error"/> rule whose reply fits
/// (<see cref="Fits"/>) is applied, and applying it publishes one <see cref="FaultAppliedEvent"/>
/// naming the path. A rule of any other kind, or a reply that does not fit, is noted as not applied
/// and the method is called as without it.
/// </remarks>
/// <param name="context">The exchange: its fault plan, event stream and listen URL.</param>
/// <param name="hubPath">The hub's endpoint base (<c>/chat</c>).</param>
/// <param name="accountName">The account that negotiated the connection, or <see langword="null"/> for the anonymous caller.</param>
internal sealed class SignalRInvocationFaults(ExchangeContext context, string hubPath, string? accountName)
{
    /// <summary>
    /// Finds the chosen Completion <c>error</c> for an invocation, publishing its <see cref="FaultAppliedEvent"/>.
    /// </summary>
    /// <param name="invocation">An invocation that has an <c>invocationId</c>.</param>
    /// <returns>The error text, or <see langword="null"/> to call the method as usual.</returns>
    public async Task<string?> FindErrorAsync(HubInvocation invocation)
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
        if (await context.Faults.FindAsync(point, context.CancellationToken) is not { } rule)
        {
            return null;
        }

        if (rule.Kind != FaultKind.Error || !Fits(rule.Reply))
        {
            var why = rule.Kind == FaultKind.Error ? "its reply does not fit the signalr error shape" : $"a {rule.Kind} rule is not applied at an invocation";
            context.Log.Note($"Fault rule {rule.Id} not applied to the invocation: {why}.");

            return null;
        }

        context.Log.Note($"The invocation was completed with the chosen error of fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, hubPath));

        return rule.Reply;
    }

    /// <summary>
    /// Whether a reply fits the <c>signalr</c> error shape: non-empty text, sent as the Completion's
    /// <c>error</c> string.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool Fits(string? reply) => !string.IsNullOrEmpty(reply);
}
