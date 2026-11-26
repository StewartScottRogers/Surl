using System.Collections.Frozen;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// How the answerer answers a request with a fault rule's chosen error (ADR-0124 decision 4,
/// ADR-0134 decision 1 and Amendment 1).
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
    // request announced a body that is never read, a refusal that closes it. A drop rule, or a
    // client that left during a delay, gives the dropped answer instead. Null when no rule
    // applies, or when the rule's reply does not fit the HTTP error shape. The WWW-Authenticate
    // fields are those WwwAuthenticateValuesForChosenError gives for the chosen status.
    private async Task<HttpAnswer?> AnswerChosenErrorAsync(HttpRequest request, string? accountName, IReadOnlyList<string> verdictValues)
    {
        var path = RequestPath(request.Target);
        var found = await HttpCommandFaults.FindAndApplyWithClientOutcomeAsync(context, request.Method, path, accountName, holdConnection);
        if (found.ClientLeft is PeerWatchOutcome.PeerClosed or PeerWatchOutcome.PeerReset)
        {
            return DroppedAnswer;
        }

        var rule = found.Rule;
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

        var sentValues = WwwAuthenticateValuesForChosenError(rule.Id, status.Code, verdictValues);

        var answer = request.Body.IsAnnounced
            ? Refuse(request, status, null, sentValues, $"{request.Method} {request.Target}: fault rule {rule.Id} chose the error, and the body announced was not read")
            : EmptyAnswer(request, status, sentValues);

        return rule.Body is null ? answer : WithChosenBody(request, answer, rule.Body);
    }

    // The WWW-Authenticate values a chosen error goes with. A let-in verdict's values are only login
    // success tokens (Negotiate's final token), never challenges, so a chosen 401 always replaces them
    // with those the session's own 401 would offer on this connection (RFC 9110 section 11.6.1). A
    // session offering none leaves the 401 without one, and this notes that in the exchange log. Every
    // other status keeps the verdict's values unchanged.
    private IReadOnlyList<string> WwwAuthenticateValuesForChosenError(string faultRuleId, int statusCode, IReadOnlyList<string> verdictValues)
    {
        if (statusCode != HttpStatus.Unauthorized.Code)
        {
            return verdictValues;
        }

        var offered = authenticationSession.CreateChallenges();
        if (offered.Count == 0)
        {
            context.Log.Note($"fault rule {faultRuleId} chose 401, and the server offers no challenge on this connection");
        }

        return offered;
    }

    // The rule's body= text replaces the status's fixed text: the same Content-Type, the text's UTF-8 length, and no
    // body after a HEAD's head (ADR-0186 decisions 3 and 5). A chosen status is always a 4xx or 5xx, so it always carries one.
    private static HttpAnswer WithChosenBody(HttpRequest request, HttpAnswer answer, string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var fields = answer.Fields
            .Where(field => !field.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) && !field.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .Append(new("Content-Type", HttpErrorText.ContentType))
            .Append(new("Content-Length", bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

        return answer with { Fields = fields, Body = request.Method == "HEAD" ? HttpAnswerBody.None : new HttpAnswerBody.Bytes(bytes) };
    }
}
