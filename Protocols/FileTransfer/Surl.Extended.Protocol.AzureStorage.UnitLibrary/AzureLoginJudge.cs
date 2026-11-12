using System.Net;
using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Judges each request's login on one connection, for both endpoints (ADR-0109, decision 3): a
/// bearer request naming a version before 2017-11-09 is <c>403 AuthenticationFailed</c>; then the
/// connection's authentication session judges the bearer token, Shared Key signature or shared
/// access signature, told the request's <see cref="AzureStorageScope"/>, and each refusal is
/// answered with decision 3.4's code: no credentials, an anonymous write or a bearer token that
/// is no account <c>401</c> with the Bearer challenge; another scheme or credentials that do not
/// read <c>400 InvalidAuthenticationInfo</c>; a permission a signature does not grant <c>403
/// AuthorizationPermissionMismatch</c>; every other <c>403 AuthenticationFailed</c> with a fixed
/// <c>AuthenticationErrorDetail</c>, never the string to sign.
/// </summary>
/// <param name="authentication">The connection's authentication session.</param>
/// <param name="scheme">The listen scheme every store call names as the caller's.</param>
/// <param name="peerAddress">The address the connection came from, which a signature's <c>sip</c> must admit.</param>
/// <param name="refusesBearerToken">Whether a bearer token is refused unchecked, because the connection is plain text and plain-text logins are not allowed (ADR-0109, decision 6).</param>
internal sealed class AzureLoginJudge(IHttpAuthenticationSession authentication, string scheme, IPAddress peerAddress, bool refusesBearerToken = false)
{
    /// <summary>
    /// The challenge every <c>401</c> carries ("Authorize with Microsoft Entra ID", "Bearer Challenge").
    /// </summary>
    public const string BearerChallenge = "Bearer authorization_uri=https://login.microsoftonline.com/common/oauth2/authorize resource_id=https://storage.azure.com";

    private const string SignatureMismatchDetail = "Signature did not match.";

    /// <summary>
    /// Judges the request's login.
    /// </summary>
    /// <param name="request">The request, its target as sent, which a Shared Key signature covers.</param>
    /// <param name="target">The target the container and blob are read from, the path-style account segment removed.</param>
    /// <param name="pathStyleAccount">
    /// The account the path-style segment named, which a shared access signature is then signed
    /// by; <see langword="null"/> in the account-host form, where the <c>Host</c>'s first label is.
    /// </param>
    /// <param name="reply">The request's answers, which a refusal is made from.</param>
    /// <param name="cancellationToken">Cancels the session's judgement.</param>
    /// <returns>The caller, or the refusal.</returns>
    public async Task<AzureLogin> LogInAsync(HttpRequest request, AzureBlobTarget target, string? pathStyleAccount, AzureReply reply, CancellationToken cancellationToken)
    {
        var authorization = AzureRequestFields.Value(request.Fields, "Authorization");
        if (IsBearerBeforeItsVersion(request, authorization))
        {
            return AzureLogin.Refused(reply.Error(AzureRefusal.AuthenticationFailed("Bearer authorization needs x-ms-version 2017-11-09 or later.")));
        }

        if (refusesBearerToken && IsBearer(authorization))
        {
            return AzureLogin.Refused(ChallengeAnswer(reply, AzureError.InvalidBearerToken));
        }

        var isWrite = request.Method is not ("GET" or "HEAD");
        var verdict = await authentication.JudgeAsync(AuthenticationRequestOf(request, target, pathStyleAccount, isWrite), cancellationToken);

        return LoginOf(verdict, isWrite, reply, authorization);
    }

    private static bool IsBearerBeforeItsVersion(HttpRequest request, string? authorization) =>
        IsBearer(authorization) && AzureRequestChecks.PrecedesBearer(AzureRequestFields.Value(request.Fields, "x-ms-version")!);

    private HttpAuthenticationRequest AuthenticationRequestOf(HttpRequest request, AzureBlobTarget target, string? pathStyleAccount, bool isWrite)
    {
        var scope = new AzureStorageScope(pathStyleAccount ?? AccountNameOf(request.Fields), ResourcePathOf(target), GrantingPermissionsOf(request.Method, target), peerAddress);

        return new HttpAuthenticationRequest(request.Method, request.Target, isWrite, request.Fields, AzureStorage: scope);
    }

    private AzureLogin LoginOf(HttpAuthenticationVerdict verdict, bool isWrite, AzureReply reply, string? authorization)
    {
        if (verdict.Outcome != HttpAuthenticationOutcome.Proceed)
        {
            return AzureLogin.Refused(RefusalAnswer(reply, verdict.Refusal, authorization));
        }

        // A write needs a login; the bearer method logs in as the empty name, anonymous as none.
        return isWrite && verdict.AccountName is null
            ? AzureLogin.Refused(ChallengeAnswer(reply, AzureError.NoAuthenticationInformation))
            : AzureLogin.As(CallerOf(verdict.AccountName));
    }

    /// <summary>
    /// The permission letters a shared access signature must grant one of for the request: a
    /// listing <c>l</c>, any other read <c>r</c>, a delete <c>d</c>, a Data Lake append <c>a</c>
    /// or <c>w</c>, any other write <c>w</c>. <c>c</c> alone never grants a write, because the
    /// login is judged before the server knows whether the name exists.
    /// </summary>
    /// <param name="method">The request method.</param>
    /// <param name="target">The request target.</param>
    /// <returns>The letters.</returns>
    public static string GrantingPermissionsOf(string method, AzureBlobTarget target)
    {
        if (method == "GET" && IsListing(target))
        {
            return "l";
        }

        if (method == "PATCH" && QueryValue(target, "action") == "append")
        {
            return "aw";
        }

        return method switch
        {
            "GET" or "HEAD" => "r",
            "DELETE" => "d",
            _ => "w",
        };
    }

    // 401 with the Bearer challenge.
    private static HttpAnswer ChallengeAnswer(AzureReply reply, AzureError error) =>
        reply.Error(AzureRefusal.Of(error), [new("WWW-Authenticate", BearerChallenge)]);

    // Decision 3.4's table. A session that gives no reason (no Azure session does) is answered
    // 401 NoAuthenticationInformation without credentials and InvalidAuthenticationInfo with them.
    private static HttpAnswer RefusalAnswer(AzureReply reply, HttpAuthenticationRefusal? refusal, string? authorization)
    {
        if (refusal is not { } reason)
        {
            return ChallengeAnswer(reply, authorization is null ? AzureError.NoAuthenticationInformation : AzureError.InvalidBearerToken);
        }

        if (reason == HttpAuthenticationRefusal.UnknownAccount && IsBearer(authorization))
        {
            return ChallengeAnswer(reply, AzureError.InvalidBearerToken);
        }

        return RefusalOf(reply, reason);
    }

    private static HttpAnswer RefusalOf(AzureReply reply, HttpAuthenticationRefusal reason) =>
        reason switch
        {
            HttpAuthenticationRefusal.MissingCredentials => ChallengeAnswer(reply, AzureError.NoAuthenticationInformation),
            HttpAuthenticationRefusal.UnsupportedScheme or HttpAuthenticationRefusal.MalformedCredentials => reply.Error(AzureRefusal.Of(AzureError.MalformedAuthorization)),
            HttpAuthenticationRefusal.PermissionNotGranted => reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch)),
            _ => reply.Error(AzureRefusal.AuthenticationFailed(DetailOf(reason))),
        };

    // A fixed sentence per cause; an unknown account reads as a bad signature, so a peer cannot
    // probe which accounts exist.
    private static string DetailOf(HttpAuthenticationRefusal refusal) =>
        refusal switch
        {
            HttpAuthenticationRefusal.MissingRequestDate => "Request date header not specified.",
            HttpAuthenticationRefusal.ClockSkew => "Request date header too old.",
            HttpAuthenticationRefusal.SignatureExpired => "Signed expiry time has passed.",
            HttpAuthenticationRefusal.SignatureNotYetValid => "Signed start time is still ahead.",
            HttpAuthenticationRefusal.AddressNotAllowed => "The request address is not in the signed IP range.",
            HttpAuthenticationRefusal.ProtocolNotAllowed => "The signature allows HTTPS only.",
            HttpAuthenticationRefusal.UnservedSignatureKind => "This kind of shared access signature is not served.",
            HttpAuthenticationRefusal.UnreadableSignatureFields => "The signed fields of the shared access signature do not read.",
            _ => SignatureMismatchDetail,
        };

    // The Host's first label, the account a shared access signature is signed by in the
    // account-host form (decision 3.3).
    private static string? AccountNameOf(IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var host = AzureRequestFields.Value(fields, "Host");

        return host?.Split('.', ':')[0];
    }

    // "/" for the account, "/<container>" for a container, "/<container>/<name>" for a blob or path.
    private static string ResourcePathOf(AzureBlobTarget target) =>
        target.RawBlob.Length > 0
            ? $"/{Uri.UnescapeDataString(target.RawContainer)}/{target.BlobName}"
            : "/" + Uri.UnescapeDataString(target.RawContainer);

    private static bool IsListing(AzureBlobTarget target) =>
        QueryValue(target, "comp") == "list" || QueryValue(target, "resource") is "filesystem" or "account";

    private static string? QueryValue(AzureBlobTarget target, string name) =>
        target.QueryParameters.FirstOrDefault(parameter => parameter.Key == name).Value;

    private ContentCaller CallerOf(string? accountName) =>
        string.IsNullOrEmpty(accountName)
            ? ContentCaller.Anonymous(scheme)
            : ContentCaller.ForAccount(accountName, scheme);

    private static bool IsBearer(string? authorization) =>
        authorization is not null && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && authorization[7..].Trim().Length > 0;
}
