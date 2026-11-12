using System.Collections.Frozen;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// How the answerer answers a request with a fault rule's chosen error (ADR-0124 decision 4,
/// ADR-0134 decision 1).
/// </summary>
internal sealed partial class HttpRequestAnswerer
{
    // RFC 9110 section 15's reason phrases for the client and server errors, with RFC 6585's,
    // sent when a chosen error's reply names a status and no reason phrase. Any other status
    // goes with an empty reason phrase, which RFC 9112 section 4 allows.
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
        [408] = "Request Timeout",
        [409] = "Conflict",
        [410] = "Gone",
        [411] = "Length Required",
        [412] = "Precondition Failed",
        [413] = "Content Too Large",
        [414] = "URI Too Long",
        [415] = "Unsupported Media Type",
        [416] = "Range Not Satisfiable",
        [417] = "Expectation Failed",
        [421] = "Misdirected Request",
        [422] = "Unprocessable Content",
        [426] = "Upgrade Required",
        [428] = "Precondition Required",
        [429] = "Too Many Requests",
        [431] = "Request Header Fields Too Large",
        [500] = "Internal Server Error",
        [501] = "Not Implemented",
        [502] = "Bad Gateway",
        [503] = "Service Unavailable",
        [504] = "Gateway Timeout",
        [505] = "HTTP Version Not Supported",
        [511] = "Network Authentication Required",
    }.ToFrozenDictionary();

    // What a drop rule answers with: nothing is written, and each HTTP version ends the exchange its own way.
    private static readonly HttpAnswer DroppedAnswer = new(
        new HttpStatus(500, string.Empty), [], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: false, DropsExchange: true);

    // A request let in is answered with a matching chosen-error rule's status before the content
    // store is touched: an empty answer that keeps the connection as a 404 does, or, when the
    // request announced a body that is never read, a refusal that closes it. Null when no rule
    // applies, or when the rule's reply does not fit the HTTP error shape.
    private async Task<HttpAnswer?> AnswerChosenErrorAsync(HttpRequest request, string? accountName, IReadOnlyList<string> challenges)
    {
        var path = RequestPath(request.Target);
        var rule = await HttpCommandFaults.FindAndApplyAsync(context, request.Method, path, accountName);
        if (rule is null)
        {
            return null;
        }

        if (rule.Kind == FaultKind.Drop)
        {
            return DroppedAnswer;
        }

        if (!HttpCommandFaults.TryReadChosenStatus(rule.Reply, out var code, out var reasonPhrase))
        {
            context.Log.Note($"{request.Method} {request.Target}: fault rule {rule.Id}'s reply does not fit the HTTP error shape; answered as without it.");

            return null;
        }

        var status = new HttpStatus(code, reasonPhrase ?? ErrorReasonPhrases.GetValueOrDefault(code, string.Empty));
        HttpCommandFaults.PublishApplied(context, rule, request.Method, path, $"{status.Code} {status.ReasonPhrase}");

        return request.Body.IsAnnounced
            ? Refuse(status, null, challenges, $"{request.Method} {request.Target}: fault rule {rule.Id} chose the error, and the body announced was not read")
            : EmptyAnswer(request, status, challenges);
    }
}
