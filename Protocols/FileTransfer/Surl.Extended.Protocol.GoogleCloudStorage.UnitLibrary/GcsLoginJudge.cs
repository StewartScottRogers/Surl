using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Judges each request's login on one connection (ADR-0110, decisions 3.1, 3.2 and 3.4): an
/// <c>Authorization</c> that is no Bearer token is <c>400 InvalidAuthentication</c>; otherwise the
/// connection's authentication session judges. No credential refused, or an anonymous write, is
/// <c>401 AuthenticationRequired</c> with the Bearer challenge; a Bearer token refused is
/// <c>401 InvalidAuthentication</c> with the <c>invalid_token</c> challenge.
/// </summary>
/// <remarks>
/// The <c>GOOG4-HMAC-SHA256</c> signature (decision 3.3) is wired in by BL-432; until then it is
/// answered as another scheme.
/// </remarks>
/// <param name="authentication">The connection's authentication session.</param>
/// <param name="log">Where each checked login's note is written.</param>
internal sealed class GcsLoginJudge(IHttpAuthenticationSession authentication, IExchangeLog log)
{
    /// <summary>
    /// The challenge a missing credential is answered with (RFC 6750, section 3).
    /// </summary>
    public const string BearerChallenge = "Bearer realm=\"storage\"";

    /// <summary>
    /// The challenge a refused Bearer token is answered with (RFC 6750, section 3.1).
    /// </summary>
    public const string InvalidTokenChallenge = "Bearer realm=\"storage\", error=\"invalid_token\"";

    /// <summary>
    /// Judges the request's login.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="reply">The request's answers, which a refusal is made from.</param>
    /// <param name="cancellationToken">Cancels the session's judgement.</param>
    /// <returns>The caller, or the refusal.</returns>
    public async Task<GcsLogin> LogInAsync(HttpRequest request, GcsReply reply, CancellationToken cancellationToken)
    {
        var authorization = GcsRequestFields.Value(request.Fields, "Authorization");
        if (authorization is not null && !IsBearer(authorization))
        {
            return GcsLogin.Refused(reply.Error(GcsError.UnsupportedAuthentication));
        }

        var isWrite = request.Method is not ("GET" or "HEAD");
        var verdict = await authentication.JudgeAsync(new HttpAuthenticationRequest(request.Method, request.Target, isWrite, request.Fields), cancellationToken);
        if (verdict.CheckedLogin is { } checkedLogin)
        {
            log.Note(checkedLogin.Note);
        }

        if (verdict.Outcome != HttpAuthenticationOutcome.Proceed)
        {
            return GcsLogin.Refused(authorization is null ? MissingCredential(reply) : reply.Error(GcsError.InvalidBearerToken, new KeyValuePair<string, string>("WWW-Authenticate", InvalidTokenChallenge)));
        }

        return isWrite && verdict.AccountName is null
            ? GcsLogin.Refused(MissingCredential(reply))
            : GcsLogin.As(CallerOf(verdict.AccountName));
    }

    private static HttpAnswer MissingCredential(GcsReply reply) =>
        reply.Error(GcsError.AuthenticationRequired, new KeyValuePair<string, string>("WWW-Authenticate", BearerChallenge));

    // The bearer method logs in as the empty name, which stands for no named account.
    private static ContentCaller CallerOf(string? accountName) =>
        string.IsNullOrEmpty(accountName)
            ? ContentCaller.Anonymous(GoogleCloudStorageProtocolServer.Scheme)
            : ContentCaller.ForAccount(accountName, GoogleCloudStorageProtocolServer.Scheme);

    private static bool IsBearer(string authorization) =>
        authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && authorization[7..].Trim().Length > 0;
}
