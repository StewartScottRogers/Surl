using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smb;

/// <summary>
/// Asks the exchange's fault plan, at each SMB command after the session setup, whether to answer
/// it with a chosen NTSTATUS (ADR-0124, decision 4): the point names the scheme, the account logged
/// in, the command (<see cref="CommandNameOf"/>) and the path, <c>/&lt;share&gt;/&lt;name&gt;</c>
/// with the share and the name's backslash-separated parts joined by <c>/</c>.
/// </summary>
/// <remarks>
/// Only a <see cref="FaultKind.Error"/> rule whose reply fits (<see cref="TryReadNtStatus"/>) is
/// applied, and applying it publishes one <see cref="FaultAppliedEvent"/> naming the path, or the
/// command when the request names none. A rule of any other kind, or a reply that does not fit, is
/// noted as not applied and the request is answered as without it (ADR-0135).
/// </remarks>
internal static class SmbCommandFaults
{
    /// <summary>
    /// <c>SMB_FLAGS2_NT_STATUS</c>: the header's status is an NTSTATUS ([MS-CIFS] 2.2.3.1).
    /// </summary>
    public const ushort NtStatusFlag = 0x4000;

    /// <summary>
    /// Finds the chosen-error rule for a command, publishing its <see cref="FaultAppliedEvent"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream and listen URL.</param>
    /// <param name="accountName">The account logged in, or <see langword="null"/> for an anonymous caller.</param>
    /// <param name="command">The command's name (<c>CREATE</c>).</param>
    /// <param name="path">The path the command concerns, or <see langword="null"/> when it names none.</param>
    /// <returns>The rule to answer with, or <see langword="null"/> to answer as usual.</returns>
    public static async ValueTask<FaultRule?> FindErrorAsync(ExchangeContext context, string? accountName, string command, string? path)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = FormatListenUrl(context.ListenUrl),
            Scheme = context.Scheme,
            Account = accountName,
            Path = path,
            Command = command,
        };
        var rule = await context.Faults.FindAsync(point, context.CancellationToken);
        if (rule is null)
        {
            return null;
        }

        if (rule.Kind != FaultKind.Error || !TryReadNtStatus(rule.Reply, out _))
        {
            var why = rule.Kind == FaultKind.Error ? $"its reply does not fit the {context.Scheme} error shape" : $"a {rule.Kind} rule is not applied at a command";
            context.Log.Note($"Fault rule {rule.Id} not applied to SMB {command}: {why}");

            return null;
        }

        context.Events.Publish(new FaultAppliedEvent(rule.Id, rule.Kind, FaultStage.Command, context.ExchangeId, path ?? command));

        return rule;
    }

    /// <summary>
    /// The name a fault rule's <c>command</c> gives a request: <c>TREE_CONNECT</c>, <c>CREATE</c>,
    /// <c>READ</c>, <c>WRITE</c>, <c>CLOSE</c> or <c>TREE_DISCONNECT</c>.
    /// </summary>
    /// <param name="request">A request answered after the session setup.</param>
    /// <returns>The name.</returns>
    public static string CommandNameOf(SmbRequest request) => request switch
    {
        SmbTreeConnectRequest => "TREE_CONNECT",
        SmbNtCreateRequest => "CREATE",
        SmbReadRequest => "READ",
        SmbWriteRequest => "WRITE",
        SmbCloseRequest => "CLOSE",
        _ => "TREE_DISCONNECT",
    };

    /// <summary>
    /// The path a fault rule's <c>path</c> is matched against: <c>/</c>, then each non-empty
    /// backslash-separated part of a share and the name within it, joined by <c>/</c>.
    /// </summary>
    /// <param name="shareAndName">The share, alone or followed by <c>\</c> and a name within it.</param>
    /// <returns><c>/up/a.txt</c> for <c>up\a.txt</c> or <c>up\\a.txt</c>; <c>/up</c> for <c>up</c>.</returns>
    public static string PathOf(string shareAndName) =>
        "/" + string.Join('/', shareAndName.Split('\\', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Reads an <c>smb</c> reply: an NTSTATUS error as eight hex digits, 0xC0000000 or above, as
    /// <c>Surl.Cli</c> checks it.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <param name="status">The status; 0 when the reply does not fit.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool TryReadNtStatus(string? reply, out uint status)
    {
        status = 0;
        if (reply is not { Length: 8 }
            || !uint.TryParse(reply, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var parsed)
            || parsed < 0xC0000000)
        {
            return false;
        }

        status = parsed;

        return true;
    }

    /// <summary>
    /// The listen URL as the fault plan matches it, as the serving engine names it: the scheme, the
    /// host (an IPv6 address in brackets, its zone's <c>%</c> encoded) and the bound port.
    /// </summary>
    /// <param name="listenUrl">The exchange's listen URL.</param>
    /// <returns><c>smb://127.0.0.1:445/</c>.</returns>
    public static string FormatListenUrl(ListenUrl listenUrl)
    {
        var host = listenUrl.Host.Contains(':', StringComparison.Ordinal)
            ? "[" + listenUrl.Host.Replace("%", "%25", StringComparison.Ordinal) + "]"
            : listenUrl.Host;

        return $"{listenUrl.Scheme}://{host}:{listenUrl.BoundPort ?? listenUrl.Port}/";
    }
}
