using System.Collections.Frozen;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// How the responder answers a request with a fault rule's chosen error (ADR-0124 decision 4,
/// ADR-0134 decision 2 and Amendment 2). A chosen <c>401</c> sends the challenges the connection's
/// authentication session offers; every other status sends the let-in verdict's values.
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
    // the session is touched, its WWW-Authenticate fields those
    // WwwAuthenticateValuesForChosenError gives for the chosen status. Otherwise the method is
    // answered.
    private async Task<bool> AnswerLetInRequestAsync(HttpRequestHead head, string cseq, string? accountName)
    {
        var path = head.RequestTarget.StartsWith(AbsoluteUrlPrefix, StringComparison.OrdinalIgnoreCase)
            ? PresentationPath(head.RequestTarget)
            : null;
        var found = await HttpCommandFaults.FindAndApplyWithClientOutcomeAsync(context, head.Method, path, accountName, connection);
        if (found.ClientLeft is PeerWatchOutcome.PeerClosed or PeerWatchOutcome.PeerReset)
        {
            // ADR-0141 amendment 3: the client left during the hold, so nothing is answered.
            connection.Abort();

            return false;
        }

        var rule = found.Rule;
        if (rule is { Kind: FaultKind.Drop })
        {
            connection.Abort();

            return false;
        }

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
        wwwAuthenticateValues = WwwAuthenticateValuesForChosenError(rule.Id, status.Code);

        return await RefuseAsync(status, cseq, head.Method, $"fault rule {rule.Id} chose the error");
    }

    // The WWW-Authenticate values a chosen error goes with. A let-in verdict's values are only login
    // success tokens (Negotiate's final token), never challenges, so a chosen 401 always replaces them
    // with those the session's own 401 would offer on this connection (RFC 9110 section 11.6.1). A
    // session offering none leaves the 401 without one, and this notes that in the exchange log. Every
    // other status keeps the verdict's values unchanged.
    private IReadOnlyList<string> WwwAuthenticateValuesForChosenError(string faultRuleId, int statusCode)
    {
        if (statusCode != RtspStatus.Unauthorized.Code)
        {
            return wwwAuthenticateValues;
        }

        var offered = authenticationSession.CreateChallenges();
        if (offered.Count == 0)
        {
            context.Log.Note($"fault rule {faultRuleId} chose 401, and the server offers no challenge on this connection");
        }

        return offered;
    }
}
