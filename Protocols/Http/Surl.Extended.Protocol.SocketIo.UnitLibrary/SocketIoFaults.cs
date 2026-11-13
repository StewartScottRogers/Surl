using System.Text;
using System.Text.Json;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Asks the exchange's fault plan, for each namespace connect and each event of a logged-in session,
/// whether to hold it, end the session or answer it with a chosen error (ADR-0124 decisions 4 and 9;
/// BL-1064): the point names the scheme (<c>socketio</c> or <c>socketios</c>), <c>connect</c> or the
/// event's name as <c>command</c>, the namespace as <c>path</c> and the login's user name.
/// </summary>
/// <remarks>
/// A <see cref="FaultKind.Delay"/> rule holds the packet's answer; a <see cref="FaultKind.Drop"/> rule
/// ends the session in place of the answer. A <see cref="FaultKind.Error"/> rule with a reply is
/// handed back, to be sent as a CONNECT_ERROR <c>message</c> or an error acknowledgement
/// (<see cref="ErrorArguments"/>), and publishes one <see cref="FaultAppliedEvent"/>. Any other rule
/// is noted as not applied and the packet is served as without it.
/// </remarks>
internal static class SocketIoFaults
{
    /// <summary>
    /// The <c>command</c> a namespace connect is matched by.
    /// </summary>
    public const string ConnectCommand = "connect";

    /// <summary>
    /// Finds the fault for one packet.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, clock, event stream, listen URL and log.</param>
    /// <param name="account">The login's user name, or <see langword="null"/>.</param>
    /// <param name="name">The packet's namespace.</param>
    /// <param name="command"><see cref="ConnectCommand"/> or the event's name.</param>
    /// <returns>The error or drop rule, or <see langword="null"/> to serve the packet as usual - after a delay too.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async ValueTask<FaultRule?> FindAsync(ExchangeContext context, string? account, string name, string command)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = account,
            Path = name,
            Command = command,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, point, name);
        if (rule is null or { Kind: FaultKind.Delay or FaultKind.Drop })
        {
            return rule is { Kind: FaultKind.Drop } ? rule : null;
        }

        if (rule.Kind != FaultKind.Error || string.IsNullOrEmpty(rule.Reply))
        {
            var why = rule.Kind == FaultKind.Error ? "it has no reply" : $"a {rule.Kind} rule is not applied at a packet";
            context.Log.Note($"Fault rule {rule.Id} not applied to {command} on {name}: {why}.");

            return null;
        }

        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, name));

        return rule;
    }

    /// <summary>
    /// The arguments of an error acknowledgement: a one-element array holding <c>{"error":"text"}</c>.
    /// </summary>
    /// <param name="text">The rule's reply.</param>
    public static byte[] ErrorArguments(string text) => Encoding.UTF8.GetBytes($"[{{\"error\":\"{JsonEncodedText.Encode(text)}\"}}]");
}
