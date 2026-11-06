using System.Collections.Frozen;
using Surl.HttpMessage;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// How the responder answers a request with a fault rule's chosen error (ADR-0124 decision 4,
/// ADR-0134 decision 2).
/// </summary>
internal sealed partial class RtspRequestResponder
{
    // RFC 2326 section 7.1.1's reason phrases for the client and server errors, sent when a chosen
    // error's reply names a status and no reason phrase. Any other status goes with an empty reason
    // phrase, which pinned upstream curl accepts.
    private static readonly FrozenDictionary<int, string> ErrorReasonPhrases = new Dictionary<int, string>
    {
        [400] = "Bad Request",
        [401] = "Unauthorized",
        [402] = "Payment Required",
        [403] = "Forbidden",
        [404] = "Not Found",
        [405] = "Method Not Allowed",
        [406] = "Not Acceptable",
        [407] = "Proxy Authentication Required",
        [408] = "Request Time-out",
        [410] = "Gone",
        [411] = "Length Required",
        [412] = "Precondition Failed",
        [413] = "Request Entity Too Large",
        [414] = "Request-URI Too Large",
        [415] = "Unsupported Media Type",
        [451] = "Parameter Not Understood",
        [452] = "Conference Not Found",
        [453] = "Not Enough Bandwidth",
        [454] = "Session Not Found",
        [455] = "Method Not Valid in This State",
        [456] = "Header Field Not Valid for Resource",
        [457] = "Invalid Range",
        [458] = "Parameter Is Read-Only",
        [459] = "Aggregate operation not allowed",
        [460] = "Only aggregate operation allowed",
        [461] = "Unsupported transport",
        [462] = "Destination unreachable",
        [500] = "Internal Server Error",
        [501] = "Not Implemented",
        [502] = "Bad Gateway",
        [503] = "Service Unavailable",
        [504] = "Gateway Time-out",
        [505] = "RTSP Version not supported",
        [551] = "Option not supported",
    }.ToFrozenDictionary();

    // A request let in is first offered to the fault plan; a chosen error is answered as every
    // refusal that keeps the connection is, its body already read, before the content store or
    // the session is touched. Otherwise the method is answered.
    private async Task<bool> AnswerLetInRequestAsync(HttpRequestHead head, string cseq, string? accountName)
    {
        var path = head.RequestTarget.StartsWith(AbsoluteUrlPrefix, StringComparison.OrdinalIgnoreCase)
            ? PresentationPath(head.RequestTarget)
            : null;
        var rule = await HttpCommandFaults.FindChosenErrorAsync(context, head.Method, path, accountName);
        if (rule is null)
        {
            return await AnswerMethodAsync(head, cseq);
        }

        if (!HttpCommandFaults.TryReadChosenStatus(rule.Reply, out var code, out var reasonPhrase))
        {
            context.Log.Note($"RTSP {head.Method}: fault rule {rule.Id}'s reply does not fit the RTSP error shape; answered as without it.");

            return await AnswerMethodAsync(head, cseq);
        }

        var status = new HttpStatus(code, reasonPhrase ?? ErrorReasonPhrases.GetValueOrDefault(code, string.Empty));
        HttpCommandFaults.PublishApplied(context, rule, head.Method, path, $"{status.Code} {status.ReasonPhrase}");

        return await RefuseAsync(status, cseq, head.Method, $"fault rule {rule.Id} chose the error");
    }
}
