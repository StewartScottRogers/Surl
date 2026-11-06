using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// Cloud Storage's V4 HMAC check (ADR-0110 decision 3.3): an <c>Authorization: GOOG4-HMAC-SHA256
/// Credential=&lt;access id&gt;/&lt;yyyyMMdd&gt;/&lt;location&gt;/storage/goog4_request, SignedHeaders=…,
/// Signature=…</c> field checked against the account its access ID names, with the account's
/// password as the HMAC secret. It shares Signature Version 4's canonical request, string to sign
/// and key chain with <see cref="AwsSigV4AuthenticationMethod"/> (<see cref="SignatureV4Check"/>),
/// the provider <c>goog</c> spelling the algorithm, the key prefix <c>GOOG4</c> and the scope
/// terminator; it differs in that the path is signed as received (encoded once), the date is
/// <c>x-goog-date</c> or else <c>Date</c>, and <c>x-goog-content-sha256</c> is required. A request
/// reaches it by naming <see cref="Service"/> as its
/// <see cref="HttpAuthenticationRequest.AwsSigV4Service"/> with the <see cref="Scheme"/> scheme.
/// </summary>
internal sealed class GoogleV4HmacAuthenticationMethod : IHttpCredentialVerifier
{
    /// <summary>
    /// The <c>Authorization</c> scheme, matched case-insensitively.
    /// </summary>
    public const string Scheme = "GOOG4-HMAC-SHA256";

    /// <summary>
    /// The service the scope names, and the <see cref="HttpAuthenticationRequest.AwsSigV4Service"/>
    /// the Cloud Storage server's requests carry.
    /// </summary>
    public const string Service = "storage";

    private const string Provider = "goog";

    private const string DateFieldName = "x-goog-date";

    private const string FallbackDateFieldName = "Date";

    private const string PayloadHashFieldName = "x-goog-content-sha256";

    private readonly AccountBook accounts;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Checks signatures against <paramref name="accounts"/> at <paramref name="timeProvider"/>'s time.
    /// </summary>
    /// <param name="accounts">The configured accounts; an access ID is a user name.</param>
    /// <param name="timeProvider">The clock a request's date is checked against.</param>
    public GoogleV4HmacAuthenticationMethod(AccountBook accounts, TimeProvider timeProvider)
    {
        this.accounts = accounts;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Checks the signature in <paramref name="credentials"/> over <paramref name="request"/>, each
    /// failure refused, never thrown, with the refusal ADR-0110 decision 3.4 maps to an error code:
    /// credentials that do not parse, a scope not ending <c>/storage/goog4_request</c> or <c>host</c>
    /// not signed are malformed; no date field is a missing date; no content hash is a missing
    /// payload hash; a date that does not read is malformed, and one that is not the scope's day or
    /// lies outside the window skewed; an access ID that is no account is unknown (checked against a random
    /// secret all the same); a signature that does not match is bad. A hex content hash binds the
    /// body once it is read (ADR-0045).
    /// </summary>
    /// <param name="credentials">What followed the <c>GOOG4-HMAC-SHA256</c> scheme.</param>
    /// <param name="request">The request the field arrived on.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Accepted as the access ID's account, refused, or awaiting the body.</returns>
    public ValueTask<HttpCredentialCheck> VerifyAsync(
        string credentials, HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var authorization = AwsSigV4Authorization.TryParse(credentials);
        if (authorization is null)
        {
            return ValueTask.FromResult(new HttpCredentialCheck(
                HttpCredentialOutcome.Refused, null, [], Refusal: HttpAuthenticationRefusal.MalformedCredentials));
        }

        return ValueTask.FromResult(IsWellFormed(authorization)
            ? Verify(authorization, request)
            : SignatureV4Check.Refused(authorization, HttpAuthenticationRefusal.MalformedCredentials));
    }

    private static bool IsWellFormed(AwsSigV4Authorization authorization) =>
        authorization.Provider == Provider
        && authorization.Service == Service
        && authorization.SignedHeaders.Contains("host");

    // The date and the content hash are judged on the head, so a stale or incomplete request is
    // refused before its body is read.
    private HttpCredentialCheck Verify(AwsSigV4Authorization authorization, HttpAuthenticationRequest request)
    {
        var requestTime = SignatureV4Check.FindFieldValue(request.Fields, DateFieldName)
            ?? SignatureV4Check.FindFieldValue(request.Fields, FallbackDateFieldName);
        var sentPayloadHash = SignatureV4Check.FindFieldValue(request.Fields, PayloadHashFieldName);
        var refusal = FindHeadRefusal(authorization, requestTime, sentPayloadHash);

        return refusal is null
            ? SignatureV4Check.VerifyWithTheSentHash(
                accounts, authorization, request, requestTime!, sentPayloadHash!, reencodesPath: false)
            : SignatureV4Check.Refused(authorization, refusal.Value);
    }

    private HttpAuthenticationRefusal? FindHeadRefusal(
        AwsSigV4Authorization authorization, string? requestTime, string? sentPayloadHash)
    {
        if (requestTime is null)
        {
            return HttpAuthenticationRefusal.MissingRequestDate;
        }

        if (sentPayloadHash is null)
        {
            return HttpAuthenticationRefusal.MissingPayloadHash;
        }

        if (!SignatureV4Check.ReadsAsRequestTime(requestTime))
        {
            return HttpAuthenticationRefusal.MalformedCredentials;
        }

        // ADR-0110 decision 3.4 answers a date off the scope's day as skewed, as one outside the window.
        return SignatureV4Check.JudgeRequestTime(requestTime, authorization.ScopeDate, timeProvider.GetUtcNow()) == true
            ? null
            : HttpAuthenticationRefusal.ClockSkew;
    }
}
