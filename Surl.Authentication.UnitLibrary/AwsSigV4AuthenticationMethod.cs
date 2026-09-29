using System.Globalization;
using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// HTTP AWS Signature Version 4 (ADR-0032 sections 3 and 4, ADR-0043): no challenge, since
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
    /// Malformed credentials, a date or host that is not signed, a date outside the window, a
    /// body whose hash was not sent, and a signature that does not match are all refused, never
    /// thrown. An unknown access key is checked against a random secret, so it costs the same.
    /// </summary>
    /// <param name="credentials">What followed the <c>&lt;PROVIDER&gt;4-HMAC-SHA256</c> scheme.</param>
    /// <param name="request">The request the field arrived on, whose method, target and signed headers are signed.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Accepted as the key's account, or refused; either carries the access key ID as sent.</returns>
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

    private HttpCredentialCheck Verify(AwsSigV4Authorization authorization, HttpAuthenticationRequest request)
    {
        var account = accounts.FindAwsSigV4Account(authorization.AccessKeyId);
        var requestTime = FindSignedRequestTime(authorization, request.Fields);
        var payloadHash = requestTime is null ? null : FindPayloadHash(requestTime.Value.FieldName, request);
        var canonicalRequest = payloadHash is null ? null : AwsSigV4CanonicalRequest.Build(request, authorization, payloadHash);
        var matches = canonicalRequest is not null
            && IsWithinWindow(requestTime!.Value.Value, authorization.ScopeDate)
            && accounts.SecretComparer.FixedTimeEquals(
                AwsSigV4Calculation.ComputeSignature(authorization, requestTime.Value.Value, canonicalRequest, account.Secret),
                authorization.Signature);

        // Only a configured account's secret can match; the dummy's is random.
        return matches
            ? new HttpCredentialCheck(HttpCredentialOutcome.Accepted, account.AccountName, [], authorization.AccessKeyId)
            : new HttpCredentialCheck(HttpCredentialOutcome.Refused, null, [], authorization.AccessKeyId);
    }

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

    // The x-<provider>-content-sha256 field when sent (always, for s3); otherwise the hash of an
    // empty body, or null for a request with a body, which the policy is not given (ADR-0043).
    private static string? FindPayloadHash(string requestTimeFieldName, HttpAuthenticationRequest request)
    {
        var sent = FindFieldValue(request.Fields, requestTimeFieldName[..^"date".Length] + "content-sha256");
        if (sent is not null)
        {
            return sent;
        }

        var contentLength = FindFieldValue(request.Fields, "Content-Length");
        var hasBody = contentLength is not (null or "0") || FindFieldValue(request.Fields, "Transfer-Encoding") is not null;

        return hasBody ? null : EmptyPayloadHash;
    }

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
