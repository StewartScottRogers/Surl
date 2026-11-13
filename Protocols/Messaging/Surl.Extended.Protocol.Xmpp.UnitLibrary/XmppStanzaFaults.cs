using System.Collections.Frozen;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// Asks the exchange's fault plan, for each stanza a bound client sends, whether to hold it, drop
/// the connection or answer it with a chosen stanza error (ADR-0124 decisions 4 and 9; BL-1063): the
/// point names the scheme (<c>xmpp</c> or <c>xmpps</c>), the stanza's kind (<c>message</c>,
/// <c>presence</c> or <c>iq</c>) as <c>command</c>, its <c>to</c> JID as <c>path</c> and the
/// logged-in account.
/// </summary>
/// <remarks>
/// A <see cref="FaultKind.Delay"/> rule holds the stanza and a <see cref="FaultKind.Drop"/> rule
/// aborts the connection, both applied by <see cref="CommandStageFaults"/>. An
/// <see cref="FaultKind.Error"/> rule whose reply is a stanza error condition of RFC 6120 section
/// 8.3.3 (<see cref="TypeOf"/>) answers the stanza as <c>&lt;error type=...&gt;</c> with that
/// condition and publishes one <see cref="FaultAppliedEvent"/>. Any other rule is noted as not
/// applied and the stanza is served as without it.
/// </remarks>
internal static class XmppStanzaFaults
{
    // RFC 6120 8.3.3: each defined condition and the error type 8.3.2 gives it.
    private static readonly FrozenDictionary<string, string> TypeByCondition = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bad-request"] = "modify",
        ["conflict"] = "cancel",
        ["feature-not-implemented"] = "cancel",
        ["forbidden"] = "auth",
        ["gone"] = "cancel",
        ["internal-server-error"] = "wait",
        ["item-not-found"] = "cancel",
        ["jid-malformed"] = "modify",
        ["not-acceptable"] = "modify",
        ["not-allowed"] = "cancel",
        ["not-authorized"] = "auth",
        ["policy-violation"] = "modify",
        ["recipient-unavailable"] = "wait",
        ["redirect"] = "modify",
        ["registration-required"] = "auth",
        ["remote-server-not-found"] = "cancel",
        ["remote-server-timeout"] = "wait",
        ["resource-constraint"] = "wait",
        ["service-unavailable"] = "cancel",
        ["subscription-required"] = "auth",
        ["undefined-condition"] = "cancel",
        ["unexpected-request"] = "wait",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Finds the fault for <paramref name="stanza"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, clock, event stream, listen URL and log.</param>
    /// <param name="connection">The connection a drop rule aborts.</param>
    /// <param name="account">The logged-in account.</param>
    /// <param name="stanza">The stanza's kind (<c>message</c>, <c>presence</c> or <c>iq</c>).</param>
    /// <param name="to">The stanza's <c>to</c> attribute, or <see langword="null"/> for none.</param>
    /// <returns>The error or drop rule, or <see langword="null"/> to serve the stanza as usual - after a delay too.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async ValueTask<FaultRule?> FindAsync(ExchangeContext context, IConnection connection, string account, string stanza, string? to)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = account,
            Path = to,
            Command = stanza,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, connection, point, to ?? stanza);
        if (rule is null or { Kind: FaultKind.Delay or FaultKind.Drop })
        {
            return rule is { Kind: FaultKind.Drop } ? rule : null;
        }

        if (rule.Kind != FaultKind.Error || TypeOf(rule.Reply) is null)
        {
            var why = rule.Kind == FaultKind.Error ? "its reply does not fit the xmpp error shape" : $"a {rule.Kind} rule is not applied at a stanza";
            context.Log.Note($"Fault rule {rule.Id} not applied to {stanza}: {why}.");

            return null;
        }

        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, to ?? stanza));

        return rule;
    }

    /// <summary>
    /// The error type RFC 6120 section 8.3.2 gives a stanza error condition.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <returns>The type (<c>auth</c>, <c>cancel</c>, <c>modify</c> or <c>wait</c>), or <see langword="null"/> when the reply is no defined condition.</returns>
    public static string? TypeOf(string? reply) => reply is not null && TypeByCondition.TryGetValue(reply, out var type) ? type : null;
}
