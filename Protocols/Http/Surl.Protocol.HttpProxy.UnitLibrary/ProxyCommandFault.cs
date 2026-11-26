using System.Collections.Frozen;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// What a command-stage fault rule makes the proxy do with one request (ADR-0160 decisions 3 and 4):
/// nothing, abort the exchange without a byte, or answer a chosen status in place of the request's
/// own answer, with the proxy's challenges when it is <c>407</c> (ADR-0160 Amendment 2). A delay has
/// already been served by the time this is returned, so it is "nothing".
/// </summary>
/// <param name="Drops">Whether the exchange ends with nothing sent.</param>
/// <param name="ChosenStatus">The status to answer, or <see langword="null"/>.</param>
/// <param name="ChosenByRuleId">The ID of the rule that chose the status, or <see langword="null"/> when none was chosen.</param>
internal readonly record struct ProxyCommandFault(bool Drops, HttpStatus? ChosenStatus, string? ChosenByRuleId)
{
    private const string HttpProxyScheme = "http-proxy";

    private const string HttpsProxyScheme = "https-proxy";

    private const string HttpsScheme = "https";

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

    /// <summary>
    /// Asks the exchange's fault plan about a request the proxy let in, at the
    /// <see cref="FaultStage.Command"/> stage with the proxy's own scheme (ADR-0160 decisions 1 and 2),
    /// and applies a delay or a drop through <see cref="CommandStageFaults"/>; a chosen error is read
    /// and published here and handed back.
    /// </summary>
    /// <param name="context">The exchange.</param>
    /// <param name="method">The request's method, as sent.</param>
    /// <param name="path">The request target without query or fragment.</param>
    /// <param name="accountName">The account the proxy login judged; <see langword="null"/> when anonymous.</param>
    /// <returns>What the proxy does with the request.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async ValueTask<ProxyCommandFault> FindAndApplyAsync(ExchangeContext context, string method, string path, string? accountName)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme == HttpsScheme ? HttpsProxyScheme : HttpProxyScheme,
            Account = string.IsNullOrEmpty(accountName) ? null : accountName,
            Path = path,
            Command = method,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, point, path);

        return rule?.Kind switch
        {
            FaultKind.Drop => new ProxyCommandFault(true, null, null),
            FaultKind.Error => ReadChosenError(context, rule, method, path),
            _ => default,
        };
    }

    /// <summary>
    /// The <c>Proxy-Authenticate</c> values the chosen status goes with: for a <c>407</c>, whatever its
    /// reason phrase, those the connection's proxy login session would offer in its own <c>407</c>
    /// (RFC 9110 sections 11.7.1 and 15.5.8; ADR-0160 Amendment 2), noting in the exchange log when it
    /// offers none; for every other status, none, and the session is not asked. Called only when
    /// <see cref="ChosenStatus"/> is not <see langword="null"/>.
    /// </summary>
    /// <param name="context">The exchange.</param>
    /// <param name="authenticationSession">The connection's proxy login session.</param>
    /// <returns>The values in the order to send them.</returns>
    public IReadOnlyList<string> CreateChosenStatusChallenges(ExchangeContext context, IHttpAuthenticationSession authenticationSession)
    {
        if (ChosenStatus!.Code != HttpProxyStatus.ProxyAuthenticationRequired.Code)
        {
            return [];
        }

        var offered = authenticationSession.CreateChallenges();
        if (offered.Count == 0)
        {
            context.Log.Note($"fault rule {ChosenByRuleId} chose 407, and the proxy offers no challenge on this connection");
        }

        return offered;
    }

    private static ProxyCommandFault ReadChosenError(ExchangeContext context, FaultRule rule, string method, string path)
    {
        if (!HttpCommandFaults.TryReadChosenStatus(rule.Reply, out var code, out var reasonPhrase))
        {
            context.Log.Note($"{method} {path}: fault rule {rule.Id}'s reply does not fit the HTTP error shape; answered as without it.");

            return default;
        }

        var status = new HttpStatus(code, reasonPhrase ?? ErrorReasonPhrases.GetValueOrDefault(code, string.Empty));
        HttpCommandFaults.PublishApplied(context, rule, method, path, $"{status.Code} {status.ReasonPhrase}");

        return new ProxyCommandFault(false, status, rule.Id);
    }
}
