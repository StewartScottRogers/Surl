using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// What <see cref="UnitTestS3AuthenticationPolicy"/> makes of a request with no
/// <c>Authorization</c>.
/// </summary>
internal enum UnitTestUnsignedRequests
{
    /// <summary>
    /// Proceeds as <see cref="UnitTestS3AuthenticationPolicy.AccountName"/>, standing in for a
    /// signed login so the operation tests need not sign their requests.
    /// </summary>
    LoggedIn,

    /// <summary>
    /// Proceeds with no account, as <c>Surl.Authentication</c> does under <c>--allow-anonymous</c>.
    /// </summary>
    Anonymous,

    /// <summary>
    /// <see cref="HttpAuthenticationOutcome.Forbidden"/> with
    /// <see cref="HttpAuthenticationRefusal.MissingCredentials"/>, as by default.
    /// </summary>
    Refused,
}

/// <summary>
/// A hand-written policy for the S3 server's tests, judging as <c>Surl.Authentication</c>'s HTTP
/// session does a request for the <c>s3</c> service (ADR-0108 decision 4.4): an
/// <c>Authorization</c> listed in <see cref="Refusals"/> is forbidden with its reason, any other
/// proceeds as <see cref="AccountName"/>; a signed request with a body and a hex
/// <c>x-amz-content-sha256</c> proceeds with a body check that refuses any other body with
/// <see cref="HttpAuthenticationRefusal.PayloadHashMismatch"/>. Every request and every body hash
/// judged is recorded.
/// </summary>
internal sealed class UnitTestS3AuthenticationPolicy(UnitTestUnsignedRequests unsignedRequests = UnitTestUnsignedRequests.LoggedIn) : IAuthenticationPolicy
{
    /// <summary>
    /// The access key the Amazon S3 API Reference's examples sign with.
    /// </summary>
    public const string ExampleAccessKeyId = "AKIAIOSFODNN7EXAMPLE";

    public string AccountName { get; init; } = ExampleAccessKeyId;

    public Dictionary<string, HttpAuthenticationRefusal> Refusals { get; } = new(StringComparer.Ordinal);

    public List<HttpAuthenticationRequest> Requests { get; } = [];

    public List<byte[]> JudgedBodyHashes { get; } = [];

    public List<TlsSession?> StartedConnections { get; } = [];

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession)
    {
        StartedConnections.Add(tlsSession);

        return new Session(this);
    }

    private static string? FieldValue(HttpAuthenticationRequest request, string name) =>
        request.Fields.FirstOrDefault(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    private static HttpAuthenticationVerdict Forbidden(HttpAuthenticationRefusal refusal, string? note = null) =>
        new(HttpAuthenticationOutcome.Forbidden, [], null, note is null ? null : new CheckedLogin("AWS4-HMAC-SHA256", note, false), Refusal: refusal);

    private HttpAuthenticationVerdict Unsigned() => unsignedRequests switch
    {
        UnitTestUnsignedRequests.LoggedIn => new(HttpAuthenticationOutcome.Proceed, [], AccountName),
        UnitTestUnsignedRequests.Anonymous => new(HttpAuthenticationOutcome.Proceed, [], null),
        _ => Forbidden(HttpAuthenticationRefusal.MissingCredentials),
    };

    private HttpAuthenticationVerdict Signed(HttpAuthenticationRequest request, string authorization)
    {
        if (Refusals.TryGetValue(authorization, out var refusal))
        {
            return Forbidden(refusal, AccountName);
        }

        var contentSha256 = FieldValue(request, "x-amz-content-sha256");
        var hasBody = FieldValue(request, "Content-Length") is not (null or "0");
        var bodyCheck = hasBody && contentSha256 is { Length: 64 } ? new BodyCheck(this, contentSha256) : null;

        // The head's signature is verified, so the account is known while the body is awaited.
        return new HttpAuthenticationVerdict(
            HttpAuthenticationOutcome.Proceed,
            [],
            AccountName,
            bodyCheck is null ? new CheckedLogin("AWS4-HMAC-SHA256", AccountName, true) : null,
            bodyCheck);
    }

    private sealed class Session(UnitTestS3AuthenticationPolicy policy) : IHttpAuthenticationSession
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            policy.Requests.Add(request);
            var authorization = FieldValue(request, "Authorization");

            return ValueTask.FromResult(authorization is null ? policy.Unsigned() : policy.Signed(request, authorization));
        }
    }

    private sealed class BodyCheck(UnitTestS3AuthenticationPolicy policy, string contentSha256) : IHttpRequestBodyCheck
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeBodyAsync(ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken)
        {
            policy.JudgedBodyHashes.Add(bodySha256.ToArray());

            return ValueTask.FromResult(Convert.ToHexStringLower(bodySha256.Span) == contentSha256
                ? new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], policy.AccountName, new CheckedLogin("AWS4-HMAC-SHA256", policy.AccountName, true))
                : Forbidden(HttpAuthenticationRefusal.PayloadHashMismatch, policy.AccountName));
        }
    }

    /// <summary>
    /// The lower-case hex SHA-256 of <paramref name="text"/>'s UTF-8 bytes, as
    /// <c>x-amz-content-sha256</c> carries it.
    /// </summary>
    public static string ContentSha256Of(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
}
