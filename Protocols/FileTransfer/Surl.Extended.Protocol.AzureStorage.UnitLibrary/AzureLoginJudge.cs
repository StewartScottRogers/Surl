using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Judges each request's login on one connection, for both endpoints (ADR-0109, decision 3): an
/// <c>Authorization</c> that is no bearer token is <c>400 InvalidAuthenticationInfo</c>; a bearer
/// request naming a version before 2017-11-09 <c>403 AuthenticationFailed</c>; then the
/// connection's authentication session judges, and a refusal - or an anonymous write - is
/// <c>401</c> with the Bearer challenge.
/// </summary>
/// <param name="authentication">The connection's authentication session.</param>
/// <param name="scheme">The listen scheme every store call names as the caller's.</param>
internal sealed class AzureLoginJudge(IHttpAuthenticationSession authentication, string scheme)
{
    /// <summary>
    /// The challenge every <c>401</c> carries ("Authorize with Microsoft Entra ID", "Bearer Challenge").
    /// </summary>
    public const string BearerChallenge = "Bearer authorization_uri=https://login.microsoftonline.com/common/oauth2/authorize resource_id=https://storage.azure.com";

    /// <summary>
    /// Judges the request's login.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="reply">The request's answers, which a refusal is made from.</param>
    /// <param name="cancellationToken">Cancels the session's judgement.</param>
    /// <returns>The caller, or the refusal.</returns>
    public async Task<AzureLogin> LogInAsync(HttpRequest request, AzureReply reply, CancellationToken cancellationToken)
    {
        var authorization = AzureRequestFields.Value(request.Fields, "Authorization");
        if (AuthorizationRefusal(authorization, request.Fields) is { } refusal)
        {
            return AzureLogin.Refused(reply.Error(refusal));
        }

        var isWrite = request.Method is not ("GET" or "HEAD");
        var verdict = await authentication.JudgeAsync(new HttpAuthenticationRequest(request.Method, request.Target, isWrite, request.Fields), cancellationToken);

        return IsServed(verdict, isWrite)
            ? AzureLogin.As(CallerOf(verdict.AccountName))
            : AzureLogin.Refused(ChallengeAnswer(reply, authorization));
    }

    // 401 with the Bearer challenge: NoAuthenticationInformation without credentials, else
    // InvalidAuthenticationInfo.
    private static HttpAnswer ChallengeAnswer(AzureReply reply, string? authorization) =>
        reply.Error(
            AzureRefusal.Of(authorization is null ? AzureError.NoAuthenticationInformation : AzureError.InvalidBearerToken),
            [new("WWW-Authenticate", BearerChallenge)]);

    // Before the session: an Authorization that is no bearer token, or a bearer request naming a
    // version before 2017-11-09.
    private static AzureRefusal? AuthorizationRefusal(string? authorization, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        if (authorization is null)
        {
            return null;
        }

        if (!IsBearer(authorization))
        {
            return AzureRefusal.Of(AzureError.MalformedAuthorization);
        }

        return AzureRequestChecks.PrecedesBearer(AzureRequestFields.Value(fields, "x-ms-version")!)
            ? AzureRefusal.AuthenticationFailed("Bearer authorization needs x-ms-version 2017-11-09 or later.")
            : null;
    }

    // A write needs a login; the bearer method logs in as the empty name, anonymous as none.
    private static bool IsServed(HttpAuthenticationVerdict verdict, bool isWrite) =>
        verdict.Outcome == HttpAuthenticationOutcome.Proceed && !(isWrite && verdict.AccountName is null);

    private ContentCaller CallerOf(string? accountName) =>
        string.IsNullOrEmpty(accountName)
            ? ContentCaller.Anonymous(scheme)
            : ContentCaller.ForAccount(accountName, scheme);

    private static bool IsBearer(string authorization) =>
        authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && authorization[7..].Trim().Length > 0;
}
