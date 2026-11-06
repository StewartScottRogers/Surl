using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// <c>Proxy-Authorization</c> (ADR-0088 decision 5 and Amendment 3): every request and every
/// <c>CONNECT</c> is judged by the connection's authentication session before it is forwarded or
/// tunnelled, and answered <c>407</c> with the session's challenges, or <c>403</c>, when the
/// session does not let it through.
/// </summary>
internal sealed partial class HttpProxyExchange
{
    /// <summary>
    /// The request target the login check is given, which is what upstream curl 8.21.0 puts in a
    /// Digest answer's <c>uri</c> (measured, Fixtures/README.md): a <c>CONNECT</c>'s authority as
    /// received, and an absolute-form request's path and query.
    /// </summary>
    private static string LoginTarget(HttpRequestHead head, bool isConnect) =>
        isConnect
            ? head.RequestTarget
            : ProxyTarget.FromAbsoluteForm(head.RequestTarget)?.OriginForm ?? head.RequestTarget;

    /// <summary>
    /// Judges the request's proxy login; <see langword="null"/> when it may go on, otherwise
    /// whether curl's connection stays open after the refusal written.
    /// </summary>
    private async Task<bool?> RefuseProxyLoginAsync(HttpRequestHead head, bool isConnect)
    {
        var request = new HttpAuthenticationRequest(
            head.Method,
            LoginTarget(head, isConnect),
            head.Method is not ("GET" or "HEAD"),
            ProxyFieldRules.ProxyLoginFields(head.Fields));
        var verdict = await authenticationSession.JudgeAsync(request, Cancellation);
        if (verdict.CheckedLogin is { } checkedLogin)
        {
            context.Log.Note(checkedLogin.Note);
        }

        return verdict.Outcome switch
        {
            HttpAuthenticationOutcome.Proceed => null,
            HttpAuthenticationOutcome.Forbidden => await AnswerAsync(HttpStatus.Forbidden, $"{head.Method} {head.RequestTarget}: the authentication policy forbade the request", closes: true),
            _ => await AnswerAsync(
                HttpProxyStatus.ProxyAuthenticationRequired,
                $"{head.Method} {head.RequestTarget}: a proxy login is needed",
                ClosesAfterRefusal(head),
                verdict.WwwAuthenticateValues),
        };
    }

    // A 407 keeps curl's connection, because NTLM and Negotiate are bound to it and curl answers
    // a 407 on the same connection; a request that asked to close, or announced a body the proxy
    // never reads, closes it.
    private static bool ClosesAfterRefusal(HttpRequestHead head) =>
        RequestEndsConnection(head) || HttpRequestBodyFraming.Of(head).Kind != HttpRequestBodyFramingKind.None;
}
