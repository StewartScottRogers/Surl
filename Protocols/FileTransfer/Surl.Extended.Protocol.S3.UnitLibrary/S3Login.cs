using System.Security.Cryptography;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The Signature Version 4 check of S3 requests (ADR-0108, decision 4): each request is judged
/// by the connection's <see cref="IHttpAuthenticationSession"/> as a request for the <c>s3</c>
/// service, and every refusal is answered with the S3 error decision 4.3 gives it. The check
/// itself is <c>Surl.Authentication</c>'s, reached only through the contract.
/// </summary>
/// <param name="authentication">The connection's authentication session.</param>
/// <param name="log">Where each checked login's note is written.</param>
internal sealed class S3Login(IHttpAuthenticationSession authentication, IExchangeLog log)
{
    /// <summary>
    /// The service every S3 request is signed for.
    /// </summary>
    public const string Service = "s3";

    /// <summary>
    /// Judges a request's head: who makes it, or the error refusing it. Without a signature it
    /// is anonymous under <c>--allow-anonymous</c>, and an anonymous write is still
    /// <c>403 AccessDenied</c>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the judgement.</param>
    /// <returns>The caller, with the rest of a check that binds the body, or the refusal.</returns>
    public async Task<S3LoginResult> JudgeHeadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var isWrite = request.Method is not ("GET" or "HEAD");
        var verdict = await authentication.JudgeAsync(
            new HttpAuthenticationRequest(request.Method, request.Target, isWrite, request.Fields, Service), cancellationToken);
        NoteLogin(verdict);
        if (verdict.Outcome != HttpAuthenticationOutcome.Proceed)
        {
            return S3LoginResult.Refused(ErrorOf(verdict.Refusal));
        }

        return isWrite && verdict.AccountName is null
            ? S3LoginResult.Refused(S3Error.AccessDenied)
            : S3LoginResult.As(verdict.AccountName, verdict.BodyCheck) with { ChunkSignatures = verdict.ChunkSignatures };
    }

    /// <summary>
    /// Finishes a check that binds the body, given the body's SHA-256.
    /// </summary>
    /// <param name="bodyCheck">The rest of the check, from <see cref="JudgeHeadAsync"/>.</param>
    /// <param name="bodySha256">The SHA-256 of the body as received.</param>
    /// <param name="cancellationToken">Cancels the judgement.</param>
    /// <returns><see langword="null"/> when the body is the one signed, else the error.</returns>
    public async Task<S3Error?> JudgeBodyAsync(IHttpRequestBodyCheck bodyCheck, ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken)
    {
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
    public async Task<S3Error?> JudgeDiscardedBodyAsync(HttpRequest request, IHttpRequestBodyCheck bodyCheck, CancellationToken cancellationToken)
    {
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await request.Body.DiscardAsync(sha256, cancellationToken);

        return await JudgeBodyAsync(bodyCheck, sha256.GetHashAndReset(), cancellationToken);
    }

    /// <summary>
    /// The S3 error each refusal is answered with (ADR-0108, decision 4.3); a refusal with no
    /// reason is <c>AccessDenied</c>.
    /// </summary>
    /// <param name="refusal">Why the session refused the request.</param>
    /// <returns>The error.</returns>
    internal static S3Error ErrorOf(HttpAuthenticationRefusal? refusal) => refusal switch
    {
        HttpAuthenticationRefusal.UnsupportedScheme => S3Error.UnsupportedAuthorizationType,
        HttpAuthenticationRefusal.MalformedCredentials => S3Error.AuthorizationHeaderMalformed,
        HttpAuthenticationRefusal.MissingPayloadHash => S3Error.MissingContentSha256,
        HttpAuthenticationRefusal.UnknownAccount => S3Error.InvalidAccessKeyId,
        HttpAuthenticationRefusal.BadSignature => S3Error.SignatureDoesNotMatch,
        HttpAuthenticationRefusal.ClockSkew => S3Error.RequestTimeTooSkewed,
        HttpAuthenticationRefusal.PayloadHashMismatch => S3Error.ContentSha256Mismatch,
        HttpAuthenticationRefusal.SignatureExpired => S3Error.RequestExpired,
        HttpAuthenticationRefusal.ExpiryOutOfRange => S3Error.ExpiresOutOfRange,
        HttpAuthenticationRefusal.ConflictingCredentials => S3Error.ConflictingAuthentication,
        _ => S3Error.AccessDenied,
    };

    private void NoteLogin(HttpAuthenticationVerdict verdict)
    {
        if (verdict.CheckedLogin is { } login)
        {
            log.Note(login.Note);
        }
    }
}
