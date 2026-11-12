using System.Text;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using Surl.WebSocket;

namespace Surl.Protocol.Ws;

/// <summary>
/// How the responder answers an upgrade with a fault rule's chosen close (ADR-0124 decision 4,
/// ADR-0134 decision 3): the <c>101</c>, then a <c>CLOSE</c> carrying the rule's close code and
/// reason as the server's first and only frame.
/// </summary>
internal sealed partial class WebSocketUpgradeResponder
{
    // The chosen close a fault rule names for an upgrade let in, or null when none applies or its
    // reply is not a close code RFC 6455 section 7.4 allows on the wire with a reason that fits a
    // CLOSE frame.
    private ChosenClose? ChosenCloseOf(FaultRule? rule, string path)
    {
        if (rule is not { Kind: FaultKind.Error })
        {
            return null;
        }

        if (!TryReadChosenClose(rule.Reply, out var code, out var reason))
        {
            context.Log.Note($"WebSocket upgrade for {path}: fault rule {rule.Id}'s reply does not fit the WebSocket close shape; answered as without it.");

            return null;
        }

        return new ChosenClose(rule, path, code, reason);
    }

    // A close code allowed on the wire, then, after one space, an optional non-empty reason that
    // fits a CLOSE frame as UTF-8.
    private static bool TryReadChosenClose(string? reply, out ushort code, out string reason)
    {
        var fits = HttpCommandFaults.TrySplitCode(reply, out var number, out var text) && WebSocketCloseCodes.IsAllowedOnTheWire(number);
        code = (ushort)number;
        reason = text ?? string.Empty;

        return fits && (text is null || IsCloseReason(text));
    }

    private static bool IsCloseReason(string text) =>
        text.Length > 0 && Encoding.UTF8.GetByteCount(text) <= WebSocketFrameEncoder.MaxCloseReasonBytes;

    // The 101 as for any upgrade, then the chosen CLOSE, the half-close and the lingering close;
    // the content store is never touched and the connection never stays open.
    private async Task<bool> AcceptAndCloseAsync(HttpRequestHead head, ChosenClose chosenClose)
    {
        var accepted = WebSocketUpgradeAcceptance.Accept(head, [], context.TimeProvider.GetUtcNow()).ResponseHead
            .AddChallengeFields(wwwAuthenticateValues);
        HttpCommandFaults.PublishApplied(context, chosenClose.Rule, head.Method, chosenClose.Path, $"101 then CLOSE {chosenClose.Code}");
        await connection.WriteAsync(accepted.ToBytes(), context.CancellationToken);
        await connection.WriteAsync(WebSocketFrameEncoder.EncodeClose(chosenClose.Code, chosenClose.Reason), context.CancellationToken);
        await connection.CompleteWritesAsync(context.CancellationToken);
        await lingeringClose.LingerAsync(context.CancellationToken);

        return false;
    }

    // A chosen close: the rule, the path it was applied to, and the CLOSE's code and reason.
    private sealed record ChosenClose(FaultRule Rule, string Path, ushort Code, string Reason);
}
