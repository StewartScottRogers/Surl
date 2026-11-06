using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// A proxy authentication policy for one account, <c>u</c> with password <c>p</c>, standing in
/// for <c>Surl.Authentication</c>'s checks (which have their own tests) so the proxy's tests see
/// exactly what the proxy hands the session. It reads <c>Authorization</c> as the session does:
/// Basic is <c>dTpw</c>; a Digest answer passes when its <c>username</c> is <c>u</c> and its
/// <c>uri</c> is the request target the proxy gave; NTLM's NEGOTIATE_MESSAGE is challenged with
/// <see cref="NtlmChallenge"/> and remembered by the connection's session, whose
/// AUTHENTICATE_MESSAGE then passes. Anything else is challenged with <see cref="Challenges"/>.
/// </summary>
internal sealed partial class UnitTestProxyAuthenticationPolicy : IAuthenticationPolicy
{
    public const string DigestChallenge = "Digest realm=\"surl\", qop=\"auth\", algorithm=MD5, nonce=\"abc\"";

    public const string BasicChallenge = "Basic realm=\"surl\", charset=\"UTF-8\"";

    // The CHALLENGE_MESSAGE the recorder sent upstream curl (Fixtures/README.md).
    public const string NtlmChallenge = "NTLM TlRMTVNTUAACAAAAAAAAADgAAAABgggAASNFZ4mrze8AAAAAAAAAAAAAAAA4AAAAAAAAAAAAAAA=";

    public static readonly string[] Challenges = [DigestChallenge, BasicChallenge];

    public List<HttpAuthenticationRequest> JudgedRequests { get; } = [];

    public List<TlsSession?> StartedTlsSessions { get; } = [];

    public HttpAuthenticationOutcome RefusalOutcome { get; init; } = HttpAuthenticationOutcome.Challenge;

    public CheckedLogin? CheckedLogin { get; init; }

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The proxy never checks a password login.");

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession)
    {
        StartedTlsSessions.Add(tlsSession);

        return new Session(this);
    }

    [GeneratedRegex("uri=\"(?<uri>[^\"]*)\"")]
    private static partial Regex DigestUri();

    private sealed class Session(UnitTestProxyAuthenticationPolicy policy) : IHttpAuthenticationSession
    {
        private bool sawNtlmNegotiate;

        public ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            policy.JudgedRequests.Add(request);
            var authorization = request.Fields.FirstOrDefault(field => field.Key == "Authorization").Value ?? string.Empty;
            var sawNegotiate = sawNtlmNegotiate;
            sawNtlmNegotiate = authorization.StartsWith("NTLM TlRMTVNTUAAB", StringComparison.Ordinal);

            return ValueTask.FromResult(Judge(authorization, request.Target, sawNegotiate));
        }

        private HttpAuthenticationVerdict Judge(string authorization, string target, bool sawNegotiate)
        {
            if (authorization == "Basic dTpw"
                || (authorization.StartsWith("Digest username=\"u\"", StringComparison.Ordinal) && DigestUri().Match(authorization).Groups["uri"].Value == target)
                || (sawNegotiate && authorization.StartsWith("NTLM TlRMTVNTUAAD", StringComparison.Ordinal)))
            {
                return new(HttpAuthenticationOutcome.Proceed, [], "u", policy.CheckedLogin);
            }

            return authorization.StartsWith("NTLM TlRMTVNTUAAB", StringComparison.Ordinal)
                ? new(HttpAuthenticationOutcome.Challenge, [NtlmChallenge], null)
                : new(policy.RefusalOutcome, Challenges, null, policy.CheckedLogin);
        }
    }
}
