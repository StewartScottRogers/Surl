using System.Globalization;
using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// The steps of a Signature Version 4 check that AWS Signature Version 4
/// (<see cref="AwsSigV4AuthenticationMethod"/>, ADR-0043, ADR-0045) and Cloud Storage's V4 HMAC
/// (<see cref="GoogleV4HmacAuthenticationMethod"/>, ADR-0110 decision 3.3) share: the request
/// date's window, the signature over the canonical request against the access key's account, and
/// the binding of the body to a signed content hash.
/// </summary>
internal static class SignatureV4Check
{
    /// <summary>
    /// How far a request's date may be from the server's time, either way (ADR-0043, ADR-0110).
    /// </summary>
    public static readonly TimeSpan RequestTimeWindow = TimeSpan.FromMinutes(15);

    /// <summary>
    /// What a client signs as the payload hash when the body is not bound (ADR-0045).
    /// </summary>
    public const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    /// <summary>
    /// The lower-case hex SHA-256 of the empty body.
    /// </summary>
    public static readonly string EmptyPayloadHash = Convert.ToHexStringLower(SHA256.HashData([]));

    private const string RequestTimeFormat = "yyyyMMdd'T'HHmmss'Z'";

    /// <summary>
    /// Whether <paramref name="requestTime"/> reads as a <c>yyyyMMddTHHmmssZ</c> time.
    /// </summary>
    /// <param name="requestTime">The request's date as sent.</param>
    /// <returns>Whether it reads.</returns>
    public static bool ReadsAsRequestTime(string requestTime) =>
        DateTimeOffset.TryParseExact(
            requestTime, RequestTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);

    /// <summary>
    /// Whether <paramref name="requestTime"/> is within <see cref="RequestTimeWindow"/> of
    /// <paramref name="now"/>.
    /// </summary>
    /// <param name="requestTime">The request's date as sent, <c>yyyyMMddTHHmmssZ</c>.</param>
    /// <param name="scopeDate">The credential scope's <c>yyyyMMdd</c> date.</param>
    /// <param name="now">The server's time.</param>
    /// <returns>
    /// Whether it is within the window, or <see langword="null"/> when it does not read or is not
    /// the scope's date.
    /// </returns>
    public static bool? JudgeRequestTime(string requestTime, string scopeDate, DateTimeOffset now)
    {
        var parsed = DateTimeOffset.TryParseExact(
            requestTime, RequestTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time);

        return parsed && requestTime.StartsWith(scopeDate + "T", StringComparison.Ordinal)
            ? (time - now).Duration() <= RequestTimeWindow
            : null;
    }

    /// <summary>
    /// The signature is over the hash as sent, so it is checked on the head; the body must then
    /// match it, unless it is <see cref="UnsignedPayload"/>.
    /// </summary>
    /// <param name="accounts">The accounts an access key ID names.</param>
    /// <param name="authorization">The credentials.</param>
    /// <param name="request">The request as received.</param>
    /// <param name="requestTime">The request's date as sent.</param>
    /// <param name="sentPayloadHash">The content hash field as sent.</param>
    /// <param name="reencodesPath">Whether the canonical path is the path percent-encoded again.</param>
    /// <returns>Accepted, refused, or awaiting the body.</returns>
    public static HttpCredentialCheck VerifyWithTheSentHash(
        AccountBook accounts,
        AwsSigV4Authorization authorization,
        HttpAuthenticationRequest request,
        string requestTime,
        string sentPayloadHash,
        bool reencodesPath)
    {
        var check = VerifySignature(accounts, authorization, request, requestTime, sentPayloadHash, reencodesPath);

        return check.Outcome == HttpCredentialOutcome.Accepted && sentPayloadHash != UnsignedPayload
            ? BindBodyToSentHash(check, authorization, request.Fields, sentPayloadHash)
            : check;
    }

    /// <summary>
    /// Checks the signature over the canonical request built with <paramref name="payloadHash"/>
    /// against the secret of the account the access key ID names. An unknown key is checked
    /// against a random secret, so it costs the same, and is refused as no account.
    /// </summary>
    /// <param name="accounts">The accounts an access key ID names.</param>
    /// <param name="authorization">The credentials.</param>
    /// <param name="request">The request as received.</param>
    /// <param name="requestTime">The request's date as sent.</param>
    /// <param name="payloadHash">The payload hash the canonical request ends with.</param>
    /// <param name="reencodesPath">Whether the canonical path is the path percent-encoded again.</param>
    /// <returns>Accepted as the key's account, or refused.</returns>
    public static HttpCredentialCheck VerifySignature(
        AccountBook accounts,
        AwsSigV4Authorization authorization,
        HttpAuthenticationRequest request,
        string requestTime,
        string payloadHash,
        bool reencodesPath)
    {
        var account = accounts.FindAwsSigV4Account(authorization.AccessKeyId);
        var canonicalRequest = AwsSigV4CanonicalRequest.Build(request, authorization, payloadHash, reencodesPath);
        var matches = canonicalRequest is not null
            && accounts.SecretComparer.FixedTimeEquals(
                AwsSigV4Calculation.ComputeSignature(authorization, requestTime, canonicalRequest, account.Secret),
                authorization.Signature);

        // Only a configured account's secret can match; the dummy's is random, and its refusal
        // says the key is no account (ADR-0108 decision 4.3).
        if (matches)
        {
            return new HttpCredentialCheck(HttpCredentialOutcome.Accepted, account.AccountName, [], authorization.AccessKeyId);
        }

        return Refused(
            authorization,
            account.AccountName is null ? HttpAuthenticationRefusal.UnknownAccount : HttpAuthenticationRefusal.BadSignature);
    }

    /// <summary>
    /// Waits for the body, then answers <paramref name="checkBody"/> with its SHA-256.
    /// </summary>
    /// <param name="authorization">The credentials, for the access key ID as sent.</param>
    /// <param name="checkBody">The check of the body's SHA-256.</param>
    /// <param name="verifiedAccountName">The account whose signature already matched, if any.</param>
    /// <returns>The check awaiting the body.</returns>
    public static HttpCredentialCheck AwaitBody(
        AwsSigV4Authorization authorization, Func<ReadOnlyMemory<byte>, HttpCredentialCheck> checkBody, string? verifiedAccountName = null) =>
        new(HttpCredentialOutcome.AwaitingBody, verifiedAccountName, [], authorization.AccessKeyId, checkBody);

    /// <summary>
    /// A refusal carrying the access key ID as sent.
    /// </summary>
    /// <param name="authorization">The credentials.</param>
    /// <param name="refusal">Why.</param>
    /// <returns>The refusal.</returns>
    public static HttpCredentialCheck Refused(AwsSigV4Authorization authorization, HttpAuthenticationRefusal refusal) =>
        new(HttpCredentialOutcome.Refused, null, [], authorization.AccessKeyId, Refusal: refusal);

    /// <summary>
    /// Whether the head announces a body: a <c>Transfer-Encoding</c>, or a <c>Content-Length</c>
    /// other than <c>0</c>. A head the HTTP server frames as bodiless all the same
    /// (<c>Content-Length: 00</c>) is hashed as the empty body.
    /// </summary>
    /// <param name="fields">The request's header fields.</param>
    /// <returns><see langword="true"/> when a body follows.</returns>
    public static bool HasBody(IReadOnlyList<KeyValuePair<string, string>> fields) =>
        FindFieldValue(fields, "Content-Length") is not (null or "0")
        || FindFieldValue(fields, "Transfer-Encoding") is not null;

    /// <summary>
    /// The first field named <paramref name="name"/>, matched case-insensitively.
    /// </summary>
    /// <param name="fields">The request's header fields.</param>
    /// <param name="name">The field name.</param>
    /// <returns>Its value, or <see langword="null"/>.</returns>
    public static string? FindFieldValue(IReadOnlyList<KeyValuePair<string, string>> fields, string name) =>
        fields.FirstOrDefault(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

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
                : Refused(authorization, HttpAuthenticationRefusal.PayloadHashMismatch);

        return HasBody(fields)
            ? AwaitBody(authorization, bodySha256 => CheckBody(bodySha256.Span), signatureCheck.AccountName)
            : CheckBody(Convert.FromHexString(EmptyPayloadHash));
    }
}
