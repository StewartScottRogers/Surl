using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// A hand-written policy for the server's tests, judging as <c>Surl.Authentication</c>'s HTTP
/// session does. Without a service: <c>Bearer secret-token</c> proceeds as <see cref="AccountName"/>
/// (the empty name, as the bearer method answers, unless a test names one) with its checked login,
/// any other credential is challenged, and a request without one proceeds anonymously only when
/// <paramref name="allowsAnonymous"/> - writes included, as the real session does. For the
/// <c>storage</c> service (a <c>GOOG4-HMAC-SHA256</c> request, ADR-0110 decision 3.3): an
/// <c>Authorization</c> listed in <see cref="SignatureRefusals"/> is forbidden with its reason, one
/// listed in <see cref="SignedAccounts"/> proceeds as its account - with a body check refusing any
/// body not hashing to a hex <c>x-goog-content-sha256</c> with
/// <see cref="HttpAuthenticationRefusal.PayloadHashMismatch"/> - and any other is
/// <see cref="HttpAuthenticationRefusal.BadSignature"/>. The signature arithmetic itself is
/// <c>Surl.Authentication</c>'s, tested there; every request and body hash judged is recorded.
/// </summary>
internal sealed class UnitTestGcsAuthenticationPolicy(bool allowsAnonymous) : IAuthenticationPolicy
{
    public const string Token = "secret-token";

    public bool AllowsAnonymous { get; } = allowsAnonymous;

    public string AccountName { get; init; } = string.Empty;

    public Dictionary<string, string> SignedAccounts { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, HttpAuthenticationRefusal> SignatureRefusals { get; } = new(StringComparer.Ordinal);

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

    private static HttpAuthenticationVerdict Forbidden(HttpAuthenticationRefusal refusal) =>
        new(HttpAuthenticationOutcome.Forbidden, [], null, new CheckedLogin("GOOG4-HMAC-SHA256", "refused", IsAccepted: false), Refusal: refusal);

    private HttpAuthenticationVerdict Signed(HttpAuthenticationRequest request)
    {
        var authorization = FieldValue(request, "Authorization")!;
        if (SignatureRefusals.TryGetValue(authorization, out var refusal))
        {
            return Forbidden(refusal);
        }

        if (!SignedAccounts.TryGetValue(authorization, out var account))
        {
            return Forbidden(HttpAuthenticationRefusal.BadSignature);
        }

        var contentSha256 = FieldValue(request, "x-goog-content-sha256");
        var bodyCheck = contentSha256 is null or "UNSIGNED-PAYLOAD" ? null : new BodyCheck(this, account, Convert.FromHexString(contentSha256));

        return new(HttpAuthenticationOutcome.Proceed, [], account, new CheckedLogin("GOOG4-HMAC-SHA256", account, IsAccepted: true), bodyCheck);
    }

    private HttpAuthenticationVerdict Unsigned(HttpAuthenticationRequest request) => FieldValue(request, "Authorization") switch
    {
        null when AllowsAnonymous => new(HttpAuthenticationOutcome.Proceed, [], null),
        "Bearer " + Token => new(HttpAuthenticationOutcome.Proceed, [], AccountName, new CheckedLogin("Bearer", CheckedLogin.BearerTokenUser, IsAccepted: true)),
        _ => new(HttpAuthenticationOutcome.Challenge, ["Bearer realm=\"surl\""], null),
    };

    private sealed class Session(UnitTestGcsAuthenticationPolicy policy) : IHttpAuthenticationSession
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            policy.Requests.Add(request);

            return ValueTask.FromResult(request.AwsSigV4Service == "storage" ? policy.Signed(request) : policy.Unsigned(request));
        }
    }

    private sealed class BodyCheck(UnitTestGcsAuthenticationPolicy policy, string account, byte[] signedSha256) : IHttpRequestBodyCheck
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeBodyAsync(ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken)
        {
            policy.JudgedBodyHashes.Add(bodySha256.ToArray());
            HttpAuthenticationVerdict verdict = CryptographicOperations.FixedTimeEquals(bodySha256.Span, signedSha256)
                ? new(HttpAuthenticationOutcome.Proceed, [], account, new CheckedLogin("GOOG4-HMAC-SHA256", account, IsAccepted: true))
                : Forbidden(HttpAuthenticationRefusal.PayloadHashMismatch);

            return ValueTask.FromResult(verdict);
        }
    }
}
