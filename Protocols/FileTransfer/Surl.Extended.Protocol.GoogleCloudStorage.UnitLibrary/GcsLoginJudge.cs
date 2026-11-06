using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Judges each request's login on one connection (ADR-0110, decisions 3.1 to 3.4): an
/// <c>Authorization</c> that is neither a Bearer token nor <c>GOOG4-HMAC-SHA256</c> is
/// <c>400 InvalidAuthentication</c>; otherwise the connection's authentication session judges, a
/// V4 HMAC signature as a request for the <see cref="SignedService"/> service. No credential
/// refused, or an anonymous write, is <c>401 AuthenticationRequired</c> with the Bearer challenge;
/// a Bearer token refused is <c>401 InvalidAuthentication</c> with the <c>invalid_token</c>
/// challenge; a V4 HMAC refusal is the error decision 3.4 gives its reason (<see cref="ErrorOf"/>).
/// </summary>
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
    /// The <c>Authorization</c> scheme of a V4 HMAC signature (decision 3.3).
    /// </summary>
    public const string SignatureScheme = "GOOG4-HMAC-SHA256";

    /// <summary>
    /// The service a V4 HMAC request is judged for, which picks the session's Cloud Storage check.
    /// </summary>
    public const string SignedService = "storage";

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
        var isSigned = authorization is not null && IsSignature(authorization);
        if (authorization is not null && !isSigned && !IsBearer(authorization))
        {
            return GcsLogin.Refused(reply.Error(GcsError.UnsupportedAuthentication));
        }

        var isWrite = request.Method is not ("GET" or "HEAD");
        var verdict = await authentication.JudgeAsync(
            new HttpAuthenticationRequest(request.Method, request.Target, isWrite, request.Fields, isSigned ? SignedService : null), cancellationToken);
        NoteLogin(verdict);
        if (verdict.Outcome != HttpAuthenticationOutcome.Proceed)
        {
            return GcsLogin.Refused(RefusalOf(verdict, authorization, isSigned, reply));
        }

        return isWrite && verdict.AccountName is null
            ? GcsLogin.Refused(MissingCredential(reply))
            : GcsLogin.As(CallerOf(verdict.AccountName), verdict.BodyCheck);
    }

    /// <summary>
    /// Finishes a check that binds the body, given the body's SHA-256 (ADR-0045).
    /// </summary>
    /// <param name="bodyCheck">The rest of the check, or <see langword="null"/> when the head decided.</param>
    /// <param name="bodySha256">The SHA-256 of the body as received.</param>
    /// <param name="cancellationToken">Cancels the judgement.</param>
    /// <returns><see langword="null"/> when there is no check or the body is the one signed, else the error.</returns>
    public async Task<GcsError?> JudgeBodyAsync(IHttpRequestBodyCheck? bodyCheck, ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken)
    {
        if (bodyCheck is null)
        {
            return null;
        }

        var verdict = await bodyCheck.JudgeBodyAsync(bodySha256, cancellationToken);
        NoteLogin(verdict);

        return verdict.Outcome == HttpAuthenticationOutcome.Proceed ? null : ErrorOf(verdict.Refusal);
    }

    /// <summary>
    /// Reads and discards a body the operation does not use, so a check that binds it can finish.
    /// </summary>
    /// <param name="request">The request whose body is read.</param>
    /// <param name="bodyCheck">The rest of the check.</param>
    /// <param name="cancellationToken">Cancels reading and judging.</param>
    /// <returns><see langword="null"/> when the body is the one signed, else the error.</returns>
    public async Task<GcsError?> JudgeDiscardedBodyAsync(HttpRequest request, IHttpRequestBodyCheck bodyCheck, CancellationToken cancellationToken)
    {
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await request.Body.DiscardAsync(sha256, cancellationToken);

        return await JudgeBodyAsync(bodyCheck, sha256.GetHashAndReset(), cancellationToken);
    }

    /// <summary>
    /// The error each V4 HMAC refusal is answered with (ADR-0110, decision 3.4); a refusal with no
    /// reason the table names is <c>403 AccessDenied</c>.
    /// </summary>
    /// <param name="refusal">Why the session refused the request.</param>
    /// <returns>The error.</returns>
    internal static GcsError ErrorOf(HttpAuthenticationRefusal? refusal) => refusal switch
    {
        HttpAuthenticationRefusal.UnsupportedScheme => GcsError.UnsupportedAuthentication,
        HttpAuthenticationRefusal.MalformedCredentials => GcsError.InvalidArgument,
        HttpAuthenticationRefusal.MissingPayloadHash or HttpAuthenticationRefusal.MissingRequestDate => GcsError.MissingSecurityHeader,
        HttpAuthenticationRefusal.UnknownAccount => GcsError.InvalidAccessKeyId,
        HttpAuthenticationRefusal.BadSignature => GcsError.SignatureDoesNotMatch,
        HttpAuthenticationRefusal.ClockSkew => GcsError.RequestTimeTooSkewed,
        HttpAuthenticationRefusal.PayloadHashMismatch => GcsError.BadDigest,
        _ => GcsError.AccessDenied,
    };

    private static HttpAnswer RefusalOf(HttpAuthenticationVerdict verdict, string? authorization, bool isSigned, GcsReply reply)
    {
        if (isSigned)
        {
            return reply.Error(ErrorOf(verdict.Refusal));
        }

        return authorization is null
            ? MissingCredential(reply)
            : reply.Error(GcsError.InvalidBearerToken, new KeyValuePair<string, string>("WWW-Authenticate", InvalidTokenChallenge));
    }

    private void NoteLogin(HttpAuthenticationVerdict verdict)
    {
        if (verdict.CheckedLogin is { } checkedLogin)
        {
            log.Note(checkedLogin.Note);
        }
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

    // The scheme is the field's first token, compared without case as the session compares it.
    private static bool IsSignature(string authorization) =>
        string.Equals(authorization.Split(' ', 2)[0], SignatureScheme, StringComparison.OrdinalIgnoreCase);
}
