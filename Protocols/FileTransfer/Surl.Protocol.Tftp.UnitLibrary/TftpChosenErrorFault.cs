using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4) for TFTP: a well-formed read or write request is
/// asked about at a <see cref="FaultStage.Command"/> point, its command <c>RRQ</c> or <c>WRQ</c> and
/// its path the file name as the content store would map it, percent-decoded. TFTP has no login, so
/// the point names no account and an account-scoped rule never matches it. A matching
/// <see cref="FaultKind.Error"/> rule's reply, an error code 0 to 7 and its text, is sent as the
/// ERROR packet that answers the request, before the content store is touched.
/// </summary>
internal static class TftpChosenErrorFault
{
    /// <summary>
    /// Asks the exchange's fault plan about <paramref name="request"/> and, when a chosen-error rule
    /// matches, notes and publishes it and returns the ERROR packet to answer with.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream, log and listen URL.</param>
    /// <param name="request">The parsed request.</param>
    /// <returns>The ERROR packet, or <see langword="null"/> when no chosen-error rule matches.</returns>
    public static async ValueTask<byte[]?> FindErrorPacketAsync(ExchangeContext context, TftpRequest request)
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
        if (await context.Faults.FindAsync(point, context.CancellationToken) is not { Kind: FaultKind.Error, Reply: { } reply } rule)
        {
            return null;
        }

        var target = $"{command} {path}";
        context.Log.Note($"{target} answered by fault rule {rule.Id}: ERROR {reply}");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, rule.Kind, FaultStage.Command, context.ExchangeId, target));
        return ErrorPacket(reply);
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
}
