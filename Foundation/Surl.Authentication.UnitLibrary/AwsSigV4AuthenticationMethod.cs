using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// HTTP AWS Signature Version 4 (ADR-0032 sections 3 and 4, ADR-0043, ADR-0045): no challenge, since
/// upstream curl's <c>--aws-sigv4</c> signs the first request unasked, and the check of that
/// signature against the account its access key ID names, with the account's password as the
/// secret access key. The request's date must be within <see cref="RequestTimeWindow"/> of the
/// injected <see cref="TimeProvider"/>'s time. The method holds no per-connection state, so
/// every connection shares this one verifier. The steps Cloud Storage's V4 HMAC shares are in
/// <see cref="SignatureV4Check"/>.
/// </summary>
public sealed class AwsSigV4AuthenticationMethod : IHttpAuthenticationMethod, IHttpCredentialVerifier
{
    /// <summary>
    /// How far a request's date may be from the server's time, either way (ADR-0043).
    /// </summary>
    public static readonly TimeSpan RequestTimeWindow = SignatureV4Check.RequestTimeWindow;

    private readonly AccountBook accounts;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Checks signatures against <paramref name="accounts"/> at <paramref name="timeProvider"/>'s time.
    /// </summary>
    /// <param name="accounts">The configured accounts; an access key ID is a user name.</param>
    /// <param name="timeProvider">The clock a request's date is checked against.</param>
    public AwsSigV4AuthenticationMethod(AccountBook accounts, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.accounts = accounts;
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public AuthenticationMethod Method => AuthenticationMethod.AwsSigV4;

    /// <summary>
    /// None: the scheme defines no challenge (ADR-0032, section 4).
    /// </summary>
    /// <returns>An empty list.</returns>
    public IReadOnlyList<string> CreateChallenges() => [];

    /// <inheritdoc/>
    public IHttpCredentialVerifier StartConnection() => this;

    /// <summary>
    /// Checks the signature in <paramref name="credentials"/> over <paramref name="request"/>.
    /// Malformed credentials, a date or host that is not signed, a date outside the window and a
    /// signature that does not match are all refused, never thrown, each with its
    /// <see cref="HttpCredentialCheck.Refusal"/> (ADR-0108 decision 4.4). An unknown access key is
    /// checked against a random secret, so it costs the same. The body is bound (ADR-0045): with
    /// an <c>x-&lt;provider&gt;-content-sha256</c> field, the signature is checked now and the
    /// body must then hash to the field (unless it is <c>UNSIGNED-PAYLOAD</c>); without one, a
    /// request with a body is checked over the body's hash once it is read. A request naming its
    /// <see cref="HttpAuthenticationRequest.AwsSigV4Service"/> must be scoped to that service and
    /// carry the content hash field.
    /// </summary>
    /// <param name="credentials">What followed the <c>&lt;PROVIDER&gt;4-HMAC-SHA256</c> scheme.</param>
    /// <param name="request">The request the field arrived on, whose method, target and signed headers are signed.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>
    /// Accepted as the key's account, refused, or awaiting the body with the check of its
    /// SHA-256; each carries the access key ID as sent.
    /// </returns>
    public ValueTask<HttpCredentialCheck> VerifyAsync(
        string credentials, HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var authorization = AwsSigV4Authorization.TryParse(credentials);
        if (authorization is null)
        {
            return ValueTask.FromResult(new HttpCredentialCheck(
                HttpCredentialOutcome.Refused, null, [], Refusal: HttpAuthenticationRefusal.MalformedCredentials));
        }

        return ValueTask.FromResult(IsScopedForTheService(authorization, request)
            ? Verify(authorization, request)
            : SignatureV4Check.Refused(authorization, HttpAuthenticationRefusal.MalformedCredentials));
    }

    /// <summary>
    /// Checks a presigned URL's signature ("Authenticating Requests: Using Query Parameters";
    /// ADR-0108 decision 4.5) for a request naming its
    /// <see cref="HttpAuthenticationRequest.AwsSigV4Service"/>: the parameters must read, the
    /// algorithm be the scope's provider's, the scope be for the service and <c>host</c> be signed
    /// (else <see cref="HttpAuthenticationRefusal.MalformedCredentials"/>); <c>X-Amz-Expires</c>
    /// must be 1 to 604800 seconds (<see cref="HttpAuthenticationRefusal.ExpiryOutOfRange"/>); the
    /// time must not be past <c>X-Amz-Date</c> plus <c>X-Amz-Expires</c>
    /// (<see cref="HttpAuthenticationRefusal.SignatureExpired"/>) nor more than
    /// <see cref="RequestTimeWindow"/> before <c>X-Amz-Date</c>
    /// (<see cref="HttpAuthenticationRefusal.ClockSkew"/>); then the signature, over
    /// <c>UNSIGNED-PAYLOAD</c> and the query without <c>X-Amz-Signature</c>, must match. No body
    /// is bound.
    /// </summary>
    /// <param name="request">The request whose target carries the parameters.</param>
    /// <returns>Accepted as the key's account, or refused with its reason.</returns>
    public HttpCredentialCheck VerifyQueryString(HttpAuthenticationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = AwsSigV4QueryString.Read(request.Target);
        if (query.Authorization is not { } authorization || !IsWellFormedQueryString(query, authorization, request))
        {
            return new HttpCredentialCheck(
                HttpCredentialOutcome.Refused, null, [], query.Authorization?.AccessKeyId, Refusal: HttpAuthenticationRefusal.MalformedCredentials);
        }

        if (QueryStringTimeRefusal(query) is { } timeRefusal)
        {
            return SignatureV4Check.Refused(authorization, timeRefusal);
        }

        return SignatureV4Check.VerifySignature(
            accounts, authorization, request, query.RequestTime!, SignatureV4Check.UnsignedPayload, false, AwsSigV4QueryString.SignatureParameter);
    }

    private static bool IsWellFormedQueryString(AwsSigV4QueryString query, AwsSigV4Authorization authorization, HttpAuthenticationRequest request) =>
        query.Algorithm == AwsSigV4Provider.AlgorithmOf(authorization.Provider)
        && IsScopedForTheService(authorization, request)
        && authorization.SignedHeaders.Contains("host")
        && query.RequestTime is { } requestTime
        && SignatureV4Check.ReadsAsRequestTime(requestTime)
        && requestTime.StartsWith(authorization.ScopeDate + "T", StringComparison.Ordinal);

    // The validity period: X-Amz-Date to X-Amz-Date plus X-Amz-Expires, with the window's leeway
    // before it for a client clock running ahead.
    private HttpAuthenticationRefusal? QueryStringTimeRefusal(AwsSigV4QueryString query)
    {
        if (query.ExpiresAfter is not { } expiresAfter)
        {
            return HttpAuthenticationRefusal.ExpiryOutOfRange;
        }

        var signedAt = SignatureV4Check.ReadRequestTime(query.RequestTime!);
        var now = timeProvider.GetUtcNow();
        if (now > signedAt + expiresAfter)
        {
            return HttpAuthenticationRefusal.SignatureExpired;
        }

        return now < signedAt - RequestTimeWindow ? HttpAuthenticationRefusal.ClockSkew : null;
    }

    private static bool IsScopedForTheService(AwsSigV4Authorization authorization, HttpAuthenticationRequest request) =>
        request.AwsSigV4Service is null || authorization.Service == request.AwsSigV4Service;

    // Every service but s3 has its path percent-encoded again (ADR-0043).
    private static bool ReencodesPath(AwsSigV4Authorization authorization) =>
        !string.Equals(authorization.Service, "s3", StringComparison.OrdinalIgnoreCase);

    // The date is judged on the head, so a stale request is refused before its body is read;
    // the rest waits for the body when the body's hash is signed and not sent (ADR-0045). A
    // request for a named service must send the hash.
    private HttpCredentialCheck Verify(AwsSigV4Authorization authorization, HttpAuthenticationRequest request)
    {
        var requestTime = FindSignedRequestTime(authorization, request.Fields);
        if (RequestTimeRefusal(authorization, requestTime) is { } timeRefusal)
        {
            return SignatureV4Check.Refused(authorization, timeRefusal);
        }

        var (dateFieldName, dateValue) = requestTime!.Value;
        var sentPayloadHash = SignatureV4Check.FindFieldValue(request.Fields, dateFieldName[..^"date".Length] + "content-sha256");
        if (sentPayloadHash is not null)
        {
            return SignatureV4Check.VerifyWithTheSentHash(
                accounts, authorization, request, dateValue, sentPayloadHash, ReencodesPath(authorization));
        }

        return request.AwsSigV4Service is null
            ? VerifyOverTheBody(authorization, request, dateValue)
            : SignatureV4Check.Refused(authorization, HttpAuthenticationRefusal.MissingPayloadHash);
    }

    // No content hash sent: signed over the body's own hash, known from the head only when there
    // is no body. A head whose canonical request cannot be built is refused before its body is read.
    private HttpCredentialCheck VerifyOverTheBody(
        AwsSigV4Authorization authorization, HttpAuthenticationRequest request, string requestTime)
    {
        var reencodesPath = ReencodesPath(authorization);
        if (!SignatureV4Check.HasBody(request.Fields))
        {
            return SignatureV4Check.VerifySignature(
                accounts, authorization, request, requestTime, SignatureV4Check.EmptyPayloadHash, reencodesPath);
        }

        return AwsSigV4CanonicalRequest.Build(request, authorization, SignatureV4Check.EmptyPayloadHash, reencodesPath) is null
            ? SignatureV4Check.Refused(authorization, HttpAuthenticationRefusal.BadSignature)
            : SignatureV4Check.AwaitBody(authorization, bodySha256 => SignatureV4Check.VerifySignature(
                accounts, authorization, request, requestTime, Convert.ToHexStringLower(bodySha256.Span), reencodesPath));
    }

    // The signed x-<provider>-date field (x-amz-date for curl's aws:amz), with host signed too.
    private static (string FieldName, string Value)? FindSignedRequestTime(
        AwsSigV4Authorization authorization, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var fieldName = authorization.SignedHeaders.FirstOrDefault(IsRequestTimeFieldName);
        var value = fieldName is null ? null : SignatureV4Check.FindFieldValue(fields, fieldName);

        return value is not null && authorization.SignedHeaders.Contains("host")
            ? (fieldName!, value)
            : null;
    }

    private static bool IsRequestTimeFieldName(string name) =>
        name.StartsWith("x-", StringComparison.Ordinal)
        && name.EndsWith("-date", StringComparison.Ordinal)
        && AwsSigV4Provider.IsName(name[2..^5]);

    // A date that is not signed, does not read or is not the scope's is malformed; one outside
    // the window is skewed.
    private HttpAuthenticationRefusal? RequestTimeRefusal(AwsSigV4Authorization authorization, (string FieldName, string Value)? requestTime) =>
        (requestTime is null
            ? null
            : SignatureV4Check.JudgeRequestTime(requestTime.Value.Value, authorization.ScopeDate, timeProvider.GetUtcNow())) switch
        {
            true => null,
            false => HttpAuthenticationRefusal.ClockSkew,
            null => HttpAuthenticationRefusal.MalformedCredentials,
        };
}
