using System.Globalization;
using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// HTTP AWS Signature Version 4 (ADR-0032 sections 3 and 4, ADR-0043, ADR-0045): no challenge, since
/// upstream curl's <c>--aws-sigv4</c> signs the first request unasked, and the check of that
/// signature against the account its access key ID names, with the account's password as the
/// secret access key. The request's date must be within <see cref="RequestTimeWindow"/> of the
/// injected <see cref="TimeProvider"/>'s time. The method holds no per-connection state, so
/// every connection shares this one verifier.
/// </summary>
public sealed class AwsSigV4AuthenticationMethod : IHttpAuthenticationMethod, IHttpCredentialVerifier
{
    /// <summary>
    /// How far a request's date may be from the server's time, either way (ADR-0043).
    /// </summary>
    public static readonly TimeSpan RequestTimeWindow = TimeSpan.FromMinutes(15);

    private const string RequestTimeFormat = "yyyyMMdd'T'HHmmss'Z'";

    // What curl signs as the payload hash when told to with -H: the body is not bound (ADR-0045).
    private const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    private static readonly string EmptyPayloadHash = Convert.ToHexStringLower(SHA256.HashData([]));

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
    /// signature that does not match are all refused, never thrown. An unknown access key is
    /// checked against a random secret, so it costs the same. The body is bound (ADR-0045): with
    /// an <c>x-&lt;provider&gt;-content-sha256</c> field, the signature is checked now and the
    /// body must then hash to the field (unless it is <c>UNSIGNED-PAYLOAD</c>); without one, a
    /// request with a body is checked over the body's hash once it is read.
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

        return ValueTask.FromResult(authorization is null
            ? new HttpCredentialCheck(HttpCredentialOutcome.Refused, null, [])
            : Verify(authorization, request));
    }

    // The date is judged on the head, so a stale request is refused before its body is read;
    // the rest waits for the body when the body's hash is signed and not sent (ADR-0045).
    private HttpCredentialCheck Verify(AwsSigV4Authorization authorization, HttpAuthenticationRequest request)
    {
        var requestTime = FindSignedRequestTime(authorization, request.Fields);
        if (requestTime is null || !IsWithinWindow(requestTime.Value.Value, authorization.ScopeDate))
        {
            return Refused(authorization);
        }

        var (dateFieldName, dateValue) = requestTime.Value;
        var sentPayloadHash = FindFieldValue(request.Fields, dateFieldName[..^"date".Length] + "content-sha256");

        return sentPayloadHash is null
            ? VerifyOverTheBody(authorization, request, dateValue)
            : VerifyWithTheSentHash(authorization, request, dateValue, sentPayloadHash);
    }

    // No content hash sent: signed over the body's own hash, known from the head only when there
    // is no body. A head whose canonical request cannot be built is refused before its body is read.
    private HttpCredentialCheck VerifyOverTheBody(
        AwsSigV4Authorization authorization, HttpAuthenticationRequest request, string requestTime)
    {
        if (!HasBody(request.Fields))
        {
            return VerifySignature(authorization, request, requestTime, EmptyPayloadHash);
        }

        return AwsSigV4CanonicalRequest.Build(request, authorization, EmptyPayloadHash) is null
            ? Refused(authorization)
            : AwaitBody(authorization, bodySha256 =>
                VerifySignature(authorization, request, requestTime, Convert.ToHexStringLower(bodySha256.Span)));
    }

    // The signature is over the hash as sent, so it is checked on the head; the body must then
    // match it, unless it is UNSIGNED-PAYLOAD.
    private HttpCredentialCheck VerifyWithTheSentHash(
        AwsSigV4Authorization authorization, HttpAuthenticationRequest request, string requestTime, string sentPayloadHash)
    {
        var check = VerifySignature(authorization, request, requestTime, sentPayloadHash);

        return check.Outcome == HttpCredentialOutcome.Accepted && sentPayloadHash != UnsignedPayload
            ? BindBodyToSentHash(check, authorization, request.Fields, sentPayloadHash)
            : check;
    }

    // A signed content hash the body must match: the empty body's is known from the head, any
    // other body's once it is read. The hash is hex, compared without regard to case.
    private static HttpCredentialCheck BindBodyToSentHash(
        HttpCredentialCheck signatureCheck,
        AwsSigV4Authorization authorization,
        IReadOnlyList<KeyValuePair<string, string>> fields,
        string sentPayloadHash)
    {
        HttpCredentialCheck CheckBody(ReadOnlySpan<byte> bodySha256) =>
            string.Equals(Convert.ToHexStringLower(bodySha256), sentPayloadHash, StringComparison.OrdinalIgnoreCase)
                ? signatureCheck
                : Refused(authorization);

        return HasBody(fields)
            ? AwaitBody(authorization, bodySha256 => CheckBody(bodySha256.Span))
            : CheckBody(Convert.FromHexString(EmptyPayloadHash));
    }

    private HttpCredentialCheck VerifySignature(
        AwsSigV4Authorization authorization, HttpAuthenticationRequest request, string requestTime, string payloadHash)
    {
        var account = accounts.FindAwsSigV4Account(authorization.AccessKeyId);
        var canonicalRequest = AwsSigV4CanonicalRequest.Build(request, authorization, payloadHash);
        var matches = canonicalRequest is not null
            && accounts.SecretComparer.FixedTimeEquals(
                AwsSigV4Calculation.ComputeSignature(authorization, requestTime, canonicalRequest, account.Secret),
                authorization.Signature);

        // Only a configured account's secret can match; the dummy's is random.
        return matches
            ? new HttpCredentialCheck(HttpCredentialOutcome.Accepted, account.AccountName, [], authorization.AccessKeyId)
            : Refused(authorization);
    }

    private static HttpCredentialCheck AwaitBody(
        AwsSigV4Authorization authorization, Func<ReadOnlyMemory<byte>, HttpCredentialCheck> checkBody) =>
        new(HttpCredentialOutcome.AwaitingBody, null, [], authorization.AccessKeyId, checkBody);

    private static HttpCredentialCheck Refused(AwsSigV4Authorization authorization) =>
        new(HttpCredentialOutcome.Refused, null, [], authorization.AccessKeyId);

    // The signed x-<provider>-date field (x-amz-date for curl's aws:amz), with host signed too.
    private static (string FieldName, string Value)? FindSignedRequestTime(
        AwsSigV4Authorization authorization, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var fieldName = authorization.SignedHeaders.FirstOrDefault(IsRequestTimeFieldName);
        var value = fieldName is null ? null : FindFieldValue(fields, fieldName);

        return value is not null && authorization.SignedHeaders.Contains("host")
            ? (fieldName!, value)
            : null;
    }

    private static bool IsRequestTimeFieldName(string name) =>
        name.StartsWith("x-", StringComparison.Ordinal)
        && name.EndsWith("-date", StringComparison.Ordinal)
        && AwsSigV4Provider.IsName(name[2..^5]);

    // A body announced by Transfer-Encoding or a Content-Length other than 0. A head the HTTP
    // server frames as bodiless all the same (Content-Length: 00) is hashed as the empty body.
    private static bool HasBody(IReadOnlyList<KeyValuePair<string, string>> fields) =>
        FindFieldValue(fields, "Content-Length") is not (null or "0")
        || FindFieldValue(fields, "Transfer-Encoding") is not null;

    private static string? FindFieldValue(IReadOnlyList<KeyValuePair<string, string>> fields, string name) =>
        fields.FirstOrDefault(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    private bool IsWithinWindow(string requestTime, string scopeDate)
    {
        var parsed = DateTimeOffset.TryParseExact(
            requestTime, RequestTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time);

        return parsed
            && requestTime.StartsWith(scopeDate + "T", StringComparison.Ordinal)
            && (time - timeProvider.GetUtcNow()).Duration() <= RequestTimeWindow;
    }
}
