using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// A two-round, connection-bound login standing in for NTLM (ADR-0032, section 6): a request
/// with <c>Authorization: NTLM round-1</c> is challenged with <c>NTLM round-2</c> and
/// remembered by its connection's session; <c>Authorization: NTLM round-3</c> proceeds only on
/// a connection whose session saw round 1. Anything else is challenged with <c>NTLM</c>.
/// </summary>
internal sealed class UnitTestTwoRoundAuthenticationPolicy : IAuthenticationPolicy
{
    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The HTTP server never checks a password login.");

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => new Session();

    private sealed class Session : IHttpAuthenticationSession
    {
        private bool sawFirstRound;

        public ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            var authorization = request.Fields.FirstOrDefault(field => field.Key == "Authorization").Value;
            HttpAuthenticationVerdict verdict = authorization switch
            {
                "NTLM round-1" => new(HttpAuthenticationOutcome.Challenge, ["NTLM round-2"], null),
                "NTLM round-3" when sawFirstRound => new(HttpAuthenticationOutcome.Proceed, [], "tester"),
                _ => new(HttpAuthenticationOutcome.Challenge, ["NTLM"], null),
            };
            sawFirstRound = authorization == "NTLM round-1";

            return ValueTask.FromResult(verdict);
        }
    }
}
